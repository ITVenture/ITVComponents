using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Abstractions;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers.Impl
{
    /// <summary>Default <see cref="IBillingHandler"/> over a context that hosts the billing tables and exposes the active tenant.</summary>
    public class BillingHandler<TContext> : IBillingHandler
        where TContext : DbContext, IBillingContext, ITenantScopeContext
    {
        /// <summary>Permission that grants access to the self-service subscription page.</summary>
        public const string ManageSubscriptionPermission = "ManageSubscription";

        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IServiceProvider services;
        private readonly IStripeCheckoutSessionFactory checkout;
        private readonly IStripeBillingPortalFactory portal;
        private readonly IPlanSynchronizer planSynchronizer;

        public BillingHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services, IStripeCheckoutSessionFactory checkout, IStripeBillingPortalFactory portal, IPlanSynchronizer planSynchronizer)
        {
            this.dbFactory = dbFactory;
            this.services = services;
            this.checkout = checkout;
            this.portal = portal;
            this.planSynchronizer = planSynchronizer;
        }

        public bool HasPermission(params string[] permissions) => services.VerifyUserPermissions(permissions);

        public bool CanManage(ClaimsPrincipal user)
            => services.VerifyUserPermissions(new[] { ManageSubscriptionPermission, ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin });

        public bool CanAdminister(ClaimsPrincipal user)
            => services.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });

        public async Task<SubscriptionOverviewViewModel> GetOverviewAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateDbContext();
            var vm = new SubscriptionOverviewViewModel();
            var tenantId = db.CurrentTenantId;
            if (tenantId == null)
            {
                return vm;
            }

            var sub = await db.TenantSubscriptions.AsNoTracking().Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, cancellationToken);
            if (sub == null)
            {
                return vm;
            }

            // Not "a row exists" but "the row mirrors a binding subscription" — see TenantSubscriptionExtensions.IsLive.
            // A started-but-aborted checkout and a canceled subscription both leave a row behind; both must fall back
            // to the plan list, or the tenant can never (re-)subscribe from the UI.
            vm.HasSubscription = sub.IsLive();
            vm.Status = sub.Status;
            vm.Currency = sub.Currency;
            vm.CurrentPeriodEnd = sub.CurrentPeriodEnd;
            vm.CancelAtPeriodEnd = sub.CancelAtPeriodEnd;

            var planIds = sub.Items.Where(i => i.PlanId != null).Select(i => i.PlanId!.Value).ToList();
            var addOnIds = sub.Items.Where(i => i.AddOnId != null).Select(i => i.AddOnId!.Value).ToList();
            var planNames = await db.Plans.Where(p => planIds.Contains(p.PlanId)).ToDictionaryAsync(p => p.PlanId, p => p.Name, cancellationToken);
            var addOnNames = await db.AddOns.Where(a => addOnIds.Contains(a.AddOnId)).ToDictionaryAsync(a => a.AddOnId, a => a.Name, cancellationToken);

            foreach (var item in sub.Items)
            {
                if (item.PlanId != null && planNames.TryGetValue(item.PlanId.Value, out var planName))
                {
                    vm.Items.Add(new SubscriptionItemViewModel { Name = planName, IsAddOn = false });
                }
                else if (item.AddOnId != null && addOnNames.TryGetValue(item.AddOnId.Value, out var addOnName))
                {
                    vm.Items.Add(new SubscriptionItemViewModel { Name = addOnName, IsAddOn = true });
                }
            }

            return vm;
        }

        public async Task<IReadOnlyList<FeatureCatalogItemViewModel>> GetFeatureCatalogAsync(CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateDbContext();
            var features = await db.Set<Feature>().AsNoTracking().OrderBy(f => f.FeatureName).ToListAsync(cancellationToken);
            return features.Select(f => new FeatureCatalogItemViewModel
            {
                Name = f.FeatureName,
                Description = f.FeatureDescription,
                Enabled = f.Enabled
            }).ToList();
        }

        public async Task<IReadOnlyList<PlanViewModel>> GetActivePlansAsync(CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateDbContext();
            return await QueryPlans(db.Plans.Where(p => p.IsActive), cancellationToken);
        }

        public async Task<IReadOnlyList<PlanViewModel>> GetAllPlansAsync(CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateDbContext();
            return await QueryPlans(db.Plans, cancellationToken);
        }

        private static async Task<IReadOnlyList<PlanViewModel>> QueryPlans(IQueryable<Plan> source, CancellationToken cancellationToken)
        {
            var plans = await source.AsNoTracking()
                .Include(p => p.Features)
                .Include(p => p.Prices)
                .Include(p => p.PlanAddOns).ThenInclude(pa => pa.Prices)
                .Include(p => p.PlanAddOns).ThenInclude(pa => pa.AddOn)
                .OrderBy(p => p.Name).ToListAsync(cancellationToken);
            return plans.Select(p => new PlanViewModel
            {
                PlanId = p.PlanId,
                Name = p.Name,
                Description = p.Description,
                BillingInterval = p.BillingInterval,
                TrialDays = p.TrialDays,
                IsActive = p.IsActive,
                Prices = p.Prices.Select(pr => new PriceViewModel { Currency = pr.Currency, Amount = pr.Amount, ProviderPriceId = pr.ProviderPriceId }).ToList(),
                FeatureKeys = p.Features.Select(f => f.FeatureKey).ToList(),
                AddOns = p.PlanAddOns.Where(pa => pa.AddOn != null).Select(pa => new PlanAddOnViewModel
                {
                    AddOnId = pa.AddOnId,
                    Name = pa.AddOn!.Name,
                    Description = pa.AddOn.Description,
                    Selected = true,
                    Prices = pa.Prices.Select(pr => new PriceViewModel { Currency = pr.Currency, Amount = pr.Amount, ProviderPriceId = pr.ProviderPriceId }).ToList()
                }).OrderBy(a => a.Name).ToList()
            }).ToList();
        }

        public async Task<IReadOnlyList<AddOnViewModel>> GetActiveAddOnsAsync(CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateDbContext();
            return await QueryAddOns(db.AddOns.Where(a => a.IsActive), cancellationToken);
        }

        private static async Task<IReadOnlyList<AddOnViewModel>> QueryAddOns(IQueryable<AddOn> source, CancellationToken cancellationToken)
        {
            var addOns = await source.AsNoTracking().Include(a => a.Features).OrderBy(a => a.Name).ToListAsync(cancellationToken);
            return addOns.Select(a => new AddOnViewModel
            {
                AddOnId = a.AddOnId,
                Name = a.Name,
                Description = a.Description,
                IsActive = a.IsActive,
                FeatureKeys = a.Features.Select(f => f.FeatureKey).ToList()
            }).ToList();
        }

        public Task<string> StartCheckoutAsync(ClaimsPrincipal user, int planId, IReadOnlyCollection<int> addOnIds, string successUrl, string cancelUrl, string? currency = null, CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateDbContext();
            var tenantId = db.CurrentTenantId ?? throw new InvalidOperationException("No active tenant scope for checkout.");
            return checkout.CreateCheckoutSessionAsync(tenantId, planId, addOnIds, successUrl, cancelUrl, currency, cancellationToken);
        }

        public Task<string?> OpenPortalAsync(ClaimsPrincipal user, string returnUrl, CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateDbContext();
            var tenantId = db.CurrentTenantId;
            return tenantId == null ? Task.FromResult<string?>(null) : portal.CreatePortalSessionAsync(tenantId.Value, returnUrl, cancellationToken);
        }

        public async Task<int> SavePlanAsync(PlanViewModel model, CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateDbContext();
            Plan plan;
            if (model.PlanId != 0)
            {
                plan = await db.Plans
                           .Include(p => p.Features)
                           .Include(p => p.Prices)
                           .Include(p => p.PlanAddOns).ThenInclude(pa => pa.Prices)
                           .FirstOrDefaultAsync(p => p.PlanId == model.PlanId, cancellationToken)
                       ?? throw new InvalidOperationException($"Plan {model.PlanId} not found.");
            }
            else
            {
                plan = new Plan();
                db.Plans.Add(plan);
            }

            plan.Name = model.Name;
            plan.Description = model.Description;
            plan.BillingInterval = model.BillingInterval;
            plan.TrialDays = model.TrialDays;
            plan.IsActive = model.IsActive;

            // Reconcile feature keys.
            var desired = new HashSet<string>(model.FeatureKeys.Where(k => !string.IsNullOrWhiteSpace(k)), StringComparer.OrdinalIgnoreCase);
            foreach (var stale in plan.Features.Where(f => !desired.Contains(f.FeatureKey)).ToList())
            {
                plan.Features.Remove(stale);
            }

            var existing = new HashSet<string>(plan.Features.Select(f => f.FeatureKey), StringComparer.OrdinalIgnoreCase);
            foreach (var key in desired.Where(k => !existing.Contains(k)))
            {
                plan.Features.Add(new PlanFeature { FeatureKey = key });
            }

            // Reconcile per-currency prices (keyed by currency). Editing an amount keeps the row so the pushed
            // ProviderPriceId is preserved; the synchronizer re-points it only if the amount actually changed.
            var desiredPrices = model.Prices
                .Where(p => !string.IsNullOrWhiteSpace(p.Currency))
                .GroupBy(p => p.Currency.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.Last().Amount);

            foreach (var stalePrice in plan.Prices.Where(p => !desiredPrices.ContainsKey(p.Currency.ToUpperInvariant())).ToList())
            {
                plan.Prices.Remove(stalePrice);
            }

            foreach (var (cur, amount) in desiredPrices)
            {
                var row = plan.Prices.FirstOrDefault(p => string.Equals(p.Currency, cur, StringComparison.OrdinalIgnoreCase));
                if (row == null)
                {
                    plan.Prices.Add(new PlanPrice { Currency = cur, Amount = amount });
                }
                else
                {
                    row.Amount = amount;
                }
            }

            ReconcilePlanAddOns(plan, model);

            await db.SaveChangesAsync(cancellationToken);
            return plan.PlanId;
        }

        /// <summary>
        /// Reconciles the plan's add-on links and their per-currency prices to <paramref name="model"/>. Only
        /// selected add-ons are kept; editing a price amount keeps the row so a pushed ProviderPriceId survives
        /// (the synchronizer re-points it only if the amount actually changed).
        /// </summary>
        private static void ReconcilePlanAddOns(Plan plan, PlanViewModel model)
        {
            var desired = model.AddOns
                .Where(a => a.Selected && a.AddOnId != 0)
                .GroupBy(a => a.AddOnId)
                .ToDictionary(g => g.Key, g => g.Last());

            foreach (var staleLink in plan.PlanAddOns.Where(pa => !desired.ContainsKey(pa.AddOnId)).ToList())
            {
                plan.PlanAddOns.Remove(staleLink);
            }

            foreach (var (addOnId, vm) in desired)
            {
                var link = plan.PlanAddOns.FirstOrDefault(pa => pa.AddOnId == addOnId);
                if (link == null)
                {
                    link = new PlanAddOn { AddOnId = addOnId };
                    plan.PlanAddOns.Add(link);
                }

                var desiredPrices = vm.Prices
                    .Where(p => !string.IsNullOrWhiteSpace(p.Currency))
                    .GroupBy(p => p.Currency.Trim().ToUpperInvariant())
                    .ToDictionary(g => g.Key, g => g.Last().Amount);

                foreach (var stalePrice in link.Prices.Where(p => !desiredPrices.ContainsKey(p.Currency.ToUpperInvariant())).ToList())
                {
                    link.Prices.Remove(stalePrice);
                }

                foreach (var (cur, amount) in desiredPrices)
                {
                    var row = link.Prices.FirstOrDefault(p => string.Equals(p.Currency, cur, StringComparison.OrdinalIgnoreCase));
                    if (row == null)
                    {
                        link.Prices.Add(new PlanAddOnPrice { Currency = cur, Amount = amount });
                    }
                    else
                    {
                        row.Amount = amount;
                    }
                }
            }
        }

        public Task PushPlanAsync(int planId, CancellationToken cancellationToken = default) => planSynchronizer.SyncPlanAsync(planId, cancellationToken);

        public async Task<IReadOnlyList<AddOnViewModel>> GetAllAddOnsAsync(CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateDbContext();
            return await QueryAddOns(db.AddOns, cancellationToken);
        }

        public async Task<int> SaveAddOnAsync(AddOnViewModel model, CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateDbContext();
            AddOn addOn;
            if (model.AddOnId != 0)
            {
                addOn = await db.AddOns.Include(a => a.Features).FirstOrDefaultAsync(a => a.AddOnId == model.AddOnId, cancellationToken)
                        ?? throw new InvalidOperationException($"Add-on {model.AddOnId} not found.");
            }
            else
            {
                addOn = new AddOn();
                db.AddOns.Add(addOn);
            }

            addOn.Name = model.Name;
            addOn.Description = model.Description;
            addOn.IsActive = model.IsActive;

            // Reconcile feature keys. Pricing and plan bookability live on the plan link (see SavePlanAsync),
            // so an add-on is just its identity + features here.
            var desired = new HashSet<string>(model.FeatureKeys.Where(k => !string.IsNullOrWhiteSpace(k)), StringComparer.OrdinalIgnoreCase);
            foreach (var stale in addOn.Features.Where(f => !desired.Contains(f.FeatureKey)).ToList())
            {
                addOn.Features.Remove(stale);
            }

            var existing = new HashSet<string>(addOn.Features.Select(f => f.FeatureKey), StringComparer.OrdinalIgnoreCase);
            foreach (var key in desired.Where(k => !existing.Contains(k)))
            {
                addOn.Features.Add(new AddOnFeature { FeatureKey = key });
            }

            await db.SaveChangesAsync(cancellationToken);
            return addOn.AddOnId;
        }

        public Task PushAddOnAsync(int addOnId, CancellationToken cancellationToken = default) => planSynchronizer.SyncAddOnAsync(addOnId, cancellationToken);

        public async Task<IReadOnlyList<SubscriptionAdminViewModel>> GetAllSubscriptionsAsync(CancellationToken cancellationToken = default)
        {
            using var db = dbFactory.CreateDbContext();
            var subs = await db.TenantSubscriptions.AsNoTracking().Include(s => s.Items)
                .OrderBy(s => s.TenantId).ToListAsync(cancellationToken);
            if (subs.Count == 0)
            {
                return Array.Empty<SubscriptionAdminViewModel>();
            }

            var planIds = subs.SelectMany(s => s.Items).Where(i => i.PlanId != null).Select(i => i.PlanId!.Value).Distinct().ToList();
            var addOnIds = subs.SelectMany(s => s.Items).Where(i => i.AddOnId != null).Select(i => i.AddOnId!.Value).Distinct().ToList();
            var planNames = await db.Plans.Where(p => planIds.Contains(p.PlanId)).ToDictionaryAsync(p => p.PlanId, p => p.Name, cancellationToken);
            var addOnNames = await db.AddOns.Where(a => addOnIds.Contains(a.AddOnId)).ToDictionaryAsync(a => a.AddOnId, a => a.Name, cancellationToken);

            var result = new List<SubscriptionAdminViewModel>(subs.Count);
            foreach (var sub in subs)
            {
                var vm = new SubscriptionAdminViewModel
                {
                    TenantId = sub.TenantId,
                    Status = sub.Status,
                    Currency = sub.Currency,
                    CurrentPeriodStart = sub.CurrentPeriodStart,
                    CurrentPeriodEnd = sub.CurrentPeriodEnd,
                    CancelAtPeriodEnd = sub.CancelAtPeriodEnd
                };

                foreach (var item in sub.Items)
                {
                    if (item.PlanId != null && planNames.TryGetValue(item.PlanId.Value, out var planName))
                    {
                        vm.Items.Add(new SubscriptionItemViewModel { Name = planName, IsAddOn = false });
                    }
                    else if (item.AddOnId != null && addOnNames.TryGetValue(item.AddOnId.Value, out var addOnName))
                    {
                        vm.Items.Add(new SubscriptionItemViewModel { Name = addOnName, IsAddOn = true });
                    }
                }

                result.Add(vm);
            }

            return result;
        }
    }
}
