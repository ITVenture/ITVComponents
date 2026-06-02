using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using Microsoft.EntityFrameworkCore;
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

        public StripeCheckoutSessionFactory(TContext db, IStripeClient client)
        {
            this.db = db;
            this.client = client;
        }

        public async Task<string> CreateCheckoutSessionAsync(int tenantId, int planId, IReadOnlyCollection<int> addOnIds, string successUrl, string cancelUrl, CancellationToken cancellationToken = default)
        {
            var plan = await db.Plans.FirstOrDefaultAsync(p => p.PlanId == planId, cancellationToken)
                       ?? throw new InvalidOperationException($"Plan {planId} not found.");
            if (string.IsNullOrEmpty(plan.ProviderPriceId))
            {
                throw new InvalidOperationException($"Plan {planId} has no provider price id — push it to the provider first.");
            }

            var lineItems = new List<SessionLineItemOptions>
            {
                new() { Price = plan.ProviderPriceId, Quantity = 1 }
            };

            if (addOnIds is { Count: > 0 })
            {
                var addOns = await db.AddOns
                    .Where(a => addOnIds.Contains(a.AddOnId) && a.ProviderPriceId != null)
                    .ToListAsync(cancellationToken);
                lineItems.AddRange(addOns.Select(a => new SessionLineItemOptions { Price = a.ProviderPriceId, Quantity = 1 }));
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
