using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Abstractions;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Impl
{
    /// <summary>
    /// Verifies Stripe webhook events and reconciles the local subscription mirror + feature entitlements.
    /// The tenant id travels in subscription metadata (stamped at checkout).
    /// </summary>
    public class StripeWebhookHandler<TContext> : IStripeWebhookHandler
        where TContext : DbContext, IBillingContext
    {
        private readonly TContext db;
        private readonly IFeatureProvisioner provisioner;
        private readonly StripeOptions options;

        public StripeWebhookHandler(TContext db, IFeatureProvisioner provisioner, IOptions<StripeOptions> options)
        {
            this.db = db;
            this.provisioner = provisioner;
            this.options = options.Value;
        }

        public async Task HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default)
        {
            var stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, options.WebhookSecret);

            switch (stripeEvent.Type)
            {
                case EventTypes.CustomerSubscriptionCreated:
                case EventTypes.CustomerSubscriptionUpdated:
                    if (stripeEvent.Data.Object is Subscription sub)
                    {
                        await UpsertAndProvisionAsync(sub, cancellationToken);
                    }

                    break;
                case EventTypes.CustomerSubscriptionDeleted:
                    if (stripeEvent.Data.Object is Subscription deleted)
                    {
                        await CancelAsync(deleted, cancellationToken);
                    }

                    break;
                case EventTypes.InvoicePaymentFailed:
                    if (stripeEvent.Data.Object is Invoice invoice)
                    {
                        await MarkPastDueAsync(invoice, cancellationToken);
                    }

                    break;
            }
        }

        private async Task UpsertAndProvisionAsync(Subscription sub, CancellationToken ct)
        {
            var tenantId = ParseTenantId(sub.Metadata);
            if (tenantId == null)
            {
                return; // cannot attribute without the tenant marker
            }

            var local = await db.TenantSubscriptions.Include(s => s.Items)
                            .FirstOrDefaultAsync(s => s.ProviderSubscriptionId == sub.Id, ct)
                        ?? await db.TenantSubscriptions.Include(s => s.Items)
                            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, ct);

            if (local == null)
            {
                local = new TenantSubscription { TenantId = tenantId.Value, Created = DateTime.UtcNow };
                db.TenantSubscriptions.Add(local);
            }

            local.ProviderSubscriptionId = sub.Id;
            local.ProviderCustomerId = sub.CustomerId;
            local.Status = StripeMapping.ToSubscriptionStatus(sub.Status);
            local.CancelAtPeriodEnd = sub.CancelAtPeriodEnd;
            var firstItem = sub.Items?.Data?.FirstOrDefault();
            local.CurrentPeriodStart = firstItem?.CurrentPeriodStart;
            local.CurrentPeriodEnd = firstItem?.CurrentPeriodEnd;
            local.Currency = firstItem?.Price?.Currency?.ToUpperInvariant();
            local.Updated = DateTime.UtcNow;

            // Rebuild the line-items and collect the union of entitled feature keys.
            if (local.Items.Count > 0)
            {
                db.TenantSubscriptionItems.RemoveRange(local.Items);
                local.Items.Clear();
            }

            // Provider prices are per-currency rows; map each back to its owning plan/add-on (currency-agnostic).
            var priceIds = (sub.Items?.Data ?? new List<SubscriptionItem>()).Select(i => i.Price.Id).ToList();
            var planPrices = await db.PlanPrices.Include(pp => pp.Plan).ThenInclude(p => p!.Features)
                .Where(pp => pp.ProviderPriceId != null && priceIds.Contains(pp.ProviderPriceId)).ToListAsync(ct);
            var addOnPrices = await db.AddOnPrices.Include(ap => ap.AddOn).ThenInclude(a => a!.Features)
                .Where(ap => ap.ProviderPriceId != null && priceIds.Contains(ap.ProviderPriceId)).ToListAsync(ct);

            var featureKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in sub.Items?.Data ?? new List<SubscriptionItem>())
            {
                var priceId = item.Price.Id;
                var line = new TenantSubscriptionItem
                {
                    ProviderSubscriptionItemId = item.Id,
                    Quantity = (int)item.Quantity
                };

                var plan = planPrices.FirstOrDefault(pp => pp.ProviderPriceId == priceId)?.Plan;
                var addOn = addOnPrices.FirstOrDefault(ap => ap.ProviderPriceId == priceId)?.AddOn;
                if (plan != null)
                {
                    line.PlanId = plan.PlanId;
                    foreach (var f in plan.Features)
                    {
                        featureKeys.Add(f.FeatureKey);
                    }
                }
                else if (addOn != null)
                {
                    line.AddOnId = addOn.AddOnId;
                    foreach (var f in addOn.Features)
                    {
                        featureKeys.Add(f.FeatureKey);
                    }
                }

                local.Items.Add(line);
            }

            await db.SaveChangesAsync(ct);

            // Entitled while the subscription is live; PastDue keeps features until period end (grace).
            var entitled = local.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.PastDue
                ? (IReadOnlyCollection<string>)featureKeys.ToList()
                : Array.Empty<string>();
            await provisioner.SyncAsync(tenantId.Value, entitled, local.CurrentPeriodStart, local.CurrentPeriodEnd, ct);
        }

        private async Task CancelAsync(Subscription sub, CancellationToken ct)
        {
            var local = await db.TenantSubscriptions
                .FirstOrDefaultAsync(s => s.ProviderSubscriptionId == sub.Id, ct);
            var tenantId = local?.TenantId ?? ParseTenantId(sub.Metadata);
            if (local != null)
            {
                local.Status = SubscriptionStatus.Canceled;
                local.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            if (tenantId != null)
            {
                await provisioner.SyncAsync(tenantId.Value, Array.Empty<string>(), null, null, ct);
            }
        }

        private async Task MarkPastDueAsync(Invoice invoice, CancellationToken ct)
        {
            // The invoice→subscription link moved around across API versions; key off the stable customer id.
            if (string.IsNullOrEmpty(invoice.CustomerId))
            {
                return;
            }

            var subs = await db.TenantSubscriptions
                .Where(s => s.ProviderCustomerId == invoice.CustomerId).ToListAsync(ct);
            foreach (var sub in subs)
            {
                sub.Status = SubscriptionStatus.PastDue;
                sub.Updated = DateTime.UtcNow;
            }

            if (subs.Count > 0)
            {
                await db.SaveChangesAsync(ct);
            }
        }

        private static int? ParseTenantId(IDictionary<string, string>? metadata)
        {
            if (metadata != null
                && metadata.TryGetValue("tenantId", out var raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                return id;
            }

            return null;
        }
    }
}
