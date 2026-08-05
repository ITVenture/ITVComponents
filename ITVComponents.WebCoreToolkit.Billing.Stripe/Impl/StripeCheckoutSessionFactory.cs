using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Impl
{
    /// <summary>Creates Stripe Checkout sessions for a base plan + optional add-ons (one subscription, N items).</summary>
    public class StripeCheckoutSessionFactory<TContext> : IStripeCheckoutSessionFactory
        where TContext : DbContext, IBillingContext
    {
        private readonly TContext db;
        private readonly IStripeClient client;
        private readonly BillingProviderOptions billingOptions;

        public StripeCheckoutSessionFactory(TContext db, IStripeClient client, IOptions<BillingProviderOptions> billingOptions)
        {
            this.db = db;
            this.client = client;
            this.billingOptions = billingOptions.Value;
        }

        public async Task<string> CreateCheckoutSessionAsync(int tenantId, int planId, IReadOnlyCollection<int> addOnIds, string successUrl, string cancelUrl, string? currency = null, CancellationToken cancellationToken = default)
        {
            var cur = string.IsNullOrWhiteSpace(currency) ? billingOptions.DefaultCurrency : currency;

            var plan = await db.Plans.Include(p => p.Prices).FirstOrDefaultAsync(p => p.PlanId == planId, cancellationToken)
                       ?? throw new InvalidOperationException($"Plan {planId} not found.");

            // Single-currency subscription: pick the plan's price row for the requested currency.
            var planPrice = plan.Prices.FirstOrDefault(p => string.Equals(p.Currency, cur, StringComparison.OrdinalIgnoreCase));
            if (planPrice == null)
            {
                throw new InvalidOperationException($"Plan {planId} has no price in currency '{cur}'.");
            }

            if (string.IsNullOrEmpty(planPrice.ProviderPriceId))
            {
                throw new InvalidOperationException($"Plan {planId} price in '{cur}' has no provider price id — push it to the provider first.");
            }

            var lineItems = new List<SessionLineItemOptions>
            {
                new() { Price = planPrice.ProviderPriceId, Quantity = 1 }
            };

            if (addOnIds is { Count: > 0 })
            {
                // Add-ons are only bookable via a plan link; validate the requested ids against this plan's links
                // (an add-on offered on a different plan must not slip in) and price them from the link.
                var links = await db.PlanAddOns.Include(pa => pa.Prices).Include(pa => pa.AddOn)
                    .Where(pa => pa.PlanId == planId && addOnIds.Contains(pa.AddOnId))
                    .ToListAsync(cancellationToken);

                foreach (var addOnId in addOnIds)
                {
                    var link = links.FirstOrDefault(l => l.AddOnId == addOnId)
                               ?? throw new InvalidOperationException($"Add-on {addOnId} is not bookable for plan {planId}.");

                    var addOnPrice = link.Prices.FirstOrDefault(p => string.Equals(p.Currency, cur, StringComparison.OrdinalIgnoreCase));
                    if (addOnPrice?.ProviderPriceId is not { Length: > 0 })
                    {
                        // All items of one subscription must share the currency — fail loudly rather than silently dropping the add-on.
                        throw new InvalidOperationException($"Add-on {addOnId} ('{link.AddOn?.Name}') has no provider price in currency '{cur}' for plan {planId}.");
                    }

                    lineItems.Add(new SessionLineItemOptions { Price = addOnPrice.ProviderPriceId, Quantity = 1 });
                }
            }

            var existing = await db.TenantSubscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

            // Guard against double-subscribing: a second subscription-mode checkout would create a parallel
            // Stripe subscription and corrupt the single-subscription-per-tenant mirror. Plan/add-on changes
            // for an active subscription go through the billing portal instead. Shares IsLive() with the
            // subscription page on purpose — a guard that is stricter or laxer than what the page shows either
            // strands the tenant or lets the parallel subscription through anyway.
            if (existing.IsLive())
            {
                throw new InvalidOperationException($"Tenant {tenantId} already has an active subscription — use the billing portal to change it.");
            }

            // Pin a dedicated Stripe customer to THIS tenant up front and hand it to the session. Without an
            // explicit customer, Checkout may attach the session to a customer Stripe recognises by e-mail/Link —
            // so a person who owns several tenants would keep billing under the first tenant's customer, and a
            // shared customer id then makes MarkPastDueAsync (keyed on customer id) hit every tenant that shares
            // it. The tenantId metadata makes the customer traceable in the Stripe dashboard and via the API.
            var customerId = await EnsureTenantCustomerAsync(tenantId, existing, cancellationToken);

            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                LineItems = lineItems,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                ClientReferenceId = tenantId.ToString(),
                Customer = customerId,
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string> { ["tenantId"] = tenantId.ToString() }
                }
            };

            if (plan.TrialDays is > 0)
            {
                options.SubscriptionData.TrialPeriodDays = plan.TrialDays;
            }

            var session = await new SessionService(client).CreateAsync(options, cancellationToken: cancellationToken);
            return session.Url;
        }

        /// <summary>
        /// Returns the Stripe customer id dedicated to <paramref name="tenantId"/>, creating one (stamped with the
        /// tenant id) and persisting it on the subscription mirror when none exists yet. Reused by later checkouts
        /// and by the billing portal, so a tenant always maps to exactly one Stripe customer.
        /// </summary>
        private async Task<string> EnsureTenantCustomerAsync(int tenantId, TenantSubscription? existing, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(existing?.ProviderCustomerId))
            {
                return existing!.ProviderCustomerId!;
            }

            var customer = await new CustomerService(client).CreateAsync(new CustomerCreateOptions
            {
                Description = $"Tenant {tenantId}",
                Metadata = new Dictionary<string, string> { ["tenantId"] = tenantId.ToString() }
            }, cancellationToken: cancellationToken);

            // Persist immediately so a failed checkout does not orphan the customer: the next attempt reuses this
            // row instead of creating a second customer. The webhook later fills in the subscription id on the
            // same row (matched by tenant id).
            var row = existing;
            if (row == null)
            {
                row = new TenantSubscription { TenantId = tenantId, Created = DateTime.UtcNow };
                db.TenantSubscriptions.Add(row);
            }

            row.ProviderCustomerId = customer.Id;
            row.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            return customer.Id;
        }
    }

    /// <summary>Creates Stripe customer-portal sessions.</summary>
    public class StripeBillingPortalFactory<TContext> : IStripeBillingPortalFactory
        where TContext : DbContext, IBillingContext
    {
        private readonly TContext db;
        private readonly IStripeClient client;

        public StripeBillingPortalFactory(TContext db, IStripeClient client)
        {
            this.db = db;
            this.client = client;
        }

        public async Task<string?> CreatePortalSessionAsync(int tenantId, string returnUrl, CancellationToken cancellationToken = default)
        {
            var subscription = await db.TenantSubscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
            if (subscription?.ProviderCustomerId is not { Length: > 0 } customerId)
            {
                return null;
            }

            var session = await new global::Stripe.BillingPortal.SessionService(client).CreateAsync(
                new global::Stripe.BillingPortal.SessionCreateOptions { Customer = customerId, ReturnUrl = returnUrl },
                cancellationToken: cancellationToken);
            return session.Url;
        }
    }
}
