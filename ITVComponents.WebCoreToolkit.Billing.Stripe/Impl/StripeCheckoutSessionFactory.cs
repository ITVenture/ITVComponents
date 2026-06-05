using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
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
                var addOns = await db.AddOns.Include(a => a.Prices)
                    .Where(a => addOnIds.Contains(a.AddOnId))
                    .ToListAsync(cancellationToken);

                foreach (var addOn in addOns)
                {
                    var addOnPrice = addOn.Prices.FirstOrDefault(p => string.Equals(p.Currency, cur, StringComparison.OrdinalIgnoreCase));
                    if (addOnPrice?.ProviderPriceId is not { Length: > 0 })
                    {
                        // All items of one subscription must share the currency — fail loudly rather than silently dropping the add-on.
                        throw new InvalidOperationException($"Add-on {addOn.AddOnId} ('{addOn.Name}') has no provider price in currency '{cur}'.");
                    }

                    lineItems.Add(new SessionLineItemOptions { Price = addOnPrice.ProviderPriceId, Quantity = 1 });
                }
            }

            var existing = await db.TenantSubscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                LineItems = lineItems,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                ClientReferenceId = tenantId.ToString(),
                Customer = string.IsNullOrEmpty(existing?.ProviderCustomerId) ? null : existing!.ProviderCustomerId,
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
