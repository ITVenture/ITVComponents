using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Impl
{
    /// <summary>Pushes <see cref="Plan"/>/<see cref="AddOn"/> definitions to Stripe (Product + Price).</summary>
    public class PlanSynchronizer<TContext> : IPlanSynchronizer
        where TContext : DbContext, IBillingContext
    {
        private readonly TContext db;
        private readonly IStripeClient client;

        public PlanSynchronizer(TContext db, IStripeClient client)
        {
            this.db = db;
            this.client = client;
        }

        public async Task SyncPlanAsync(int planId, CancellationToken cancellationToken = default)
        {
            var plan = await db.Plans.FirstOrDefaultAsync(p => p.PlanId == planId, cancellationToken);
            if (plan == null)
            {
                return;
            }

            plan.ProviderProductId = await EnsureProductAsync(plan.ProviderProductId, plan.Name, plan.Description, plan.IsActive, cancellationToken);
            plan.ProviderPriceId = await EnsurePriceAsync(plan.ProviderProductId!, plan.ProviderPriceId, plan.Amount, plan.Currency, plan.BillingInterval, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task SyncAddOnAsync(int addOnId, CancellationToken cancellationToken = default)
        {
            var addOn = await db.AddOns.FirstOrDefaultAsync(a => a.AddOnId == addOnId, cancellationToken);
            if (addOn == null)
            {
                return;
            }

            addOn.ProviderProductId = await EnsureProductAsync(addOn.ProviderProductId, addOn.Name, addOn.Description, addOn.IsActive, cancellationToken);
            addOn.ProviderPriceId = await EnsurePriceAsync(addOn.ProviderProductId!, addOn.ProviderPriceId, addOn.Amount, addOn.Currency, addOn.BillingInterval, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        private async Task<string> EnsureProductAsync(string? productId, string name, string? description, bool active, CancellationToken ct)
        {
            var service = new ProductService(client);
            if (string.IsNullOrEmpty(productId))
            {
                var created = await service.CreateAsync(new ProductCreateOptions { Name = name, Description = description, Active = active }, cancellationToken: ct);
                return created.Id;
            }

            await service.UpdateAsync(productId, new ProductUpdateOptions { Name = name, Description = description, Active = active }, cancellationToken: ct);
            return productId;
        }

        /// <summary>
        /// Returns a Stripe price id that matches the desired amount/currency/interval. Stripe prices are
        /// immutable, so when the existing price differs a new one is created and the old one archived.
        /// </summary>
        private async Task<string> EnsurePriceAsync(string productId, string? priceId, decimal amount, string? currency, BillingInterval interval, CancellationToken ct)
        {
            var service = new PriceService(client);
            var minor = StripeMapping.ToMinorUnits(amount);
            var stripeInterval = StripeMapping.ToStripeInterval(interval);
            var cur = (currency ?? "eur").ToLowerInvariant();

            if (!string.IsNullOrEmpty(priceId))
            {
                var existing = await service.GetAsync(priceId, cancellationToken: ct);
                var sameInterval = existing.Recurring?.Interval == stripeInterval;
                if (existing.UnitAmount == minor && existing.Currency == cur && sameInterval && existing.Active)
                {
                    return priceId;
                }

                // Immutable: archive the stale price, fall through to create a new one.
                await service.UpdateAsync(priceId, new PriceUpdateOptions { Active = false }, cancellationToken: ct);
            }

            var createOptions = new PriceCreateOptions
            {
                Product = productId,
                Currency = cur,
                UnitAmount = minor
            };
            if (stripeInterval != null)
            {
                createOptions.Recurring = new PriceRecurringOptions { Interval = stripeInterval };
            }

            var price = await service.CreateAsync(createOptions, cancellationToken: ct);
            return price.Id;
        }
    }
}
