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

        private readonly TContext db;
        private readonly IServiceProvider services;
        private readonly IStripeCheckoutSessionFactory checkout;
        private readonly IStripeBillingPortalFactory portal;
        private readonly IPlanSynchronizer planSynchronizer;

        public BillingHandler(TContext db, IServiceProvider services, IStripeCheckoutSessionFactory checkout, IStripeBillingPortalFactory portal, IPlanSynchronizer planSynchronizer)
        {
            this.db = db;
            this.services = services;
            this.checkout = checkout;
            this.portal = portal;
            this.planSynchronizer = planSynchronizer;
        }

        public bool HasPermission(ClaimsPrincipal user, params string[] permissions) => services.VerifyUserPermissions(permissions);

        public bool CanManage(ClaimsPrincipal user)
            => services.VerifyUserPermissions(new[] { ManageSubscriptionPermission, ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin });

        public bool CanAdminister(ClaimsPrincipal user)
            => services.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });

        public async Task<SubscriptionOverviewViewModel> GetOverviewAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
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

            vm.HasSubscription = true;
            vm.Status = sub.Status;
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

        public async Task<IReadOnlyList<PlanViewModel>> GetActivePlansAsync(CancellationToken cancellationToken = default)
            => await QueryPlans(db.Plans.Where(p => p.IsActive), cancellationToken);

        public async Task<IReadOnlyList<PlanViewModel>> GetAllPlansAsync(CancellationToken cancellationToken = default)
            => await QueryPlans(db.Plans, cancellationToken);

        private static async Task<IReadOnlyList<PlanViewModel>> QueryPlans(IQueryable<Plan> source, CancellationToken cancellationToken)
        {
            var plans = await source.AsNoTracking().Include(p => p.Features).OrderBy(p => p.Amount).ToListAsync(cancellationToken);
            return plans.Select(p => new PlanViewModel
            {
                PlanId = p.PlanId,
                Name = p.Name,
                Description = p.Description,
                Amount = p.Amount,
                Currency = p.Currency,
                BillingInterval = p.BillingInterval,
                TrialDays = p.TrialDays,
                IsActive = p.IsActive,
                ProviderPriceId = p.ProviderPriceId,
                FeatureKeys = p.Features.Select(f => f.FeatureKey).ToList()
            }).ToList();
        }

        public async Task<IReadOnlyList<AddOnViewModel>> GetActiveAddOnsAsync(CancellationToken cancellationToken = default)
        {
            var addOns = await db.AddOns.AsNoTracking().Where(a => a.IsActive).Include(a => a.Features).OrderBy(a => a.Amount).ToListAsync(cancellationToken);
            return addOns.Select(a => new AddOnViewModel
            {
                AddOnId = a.AddOnId,
                Name = a.Name,
                Description = a.Description,
                Amount = a.Amount,
                Currency = a.Currency,
                BillingInterval = a.BillingInterval,
                IsActive = a.IsActive,
                ProviderPriceId = a.ProviderPriceId,
                FeatureKeys = a.Features.Select(f => f.FeatureKey).ToList()
            }).ToList();
        }

        public Task<string> StartCheckoutAsync(ClaimsPrincipal user, int planId, IReadOnlyCollection<int> addOnIds, string successUrl, string cancelUrl, CancellationToken cancellationToken = default)
        {
            var tenantId = db.CurrentTenantId ?? throw new InvalidOperationException("No active tenant scope for checkout.");
            return checkout.CreateCheckoutSessionAsync(tenantId, planId, addOnIds, successUrl, cancelUrl, cancellationToken);
        }

        public Task<string?> OpenPortalAsync(ClaimsPrincipal user, string returnUrl, CancellationToken cancellationToken = default)
        {
            var tenantId = db.CurrentTenantId;
            return tenantId == null ? Task.FromResult<string?>(null) : portal.CreatePortalSessionAsync(tenantId.Value, returnUrl, cancellationToken);
        }

        public async Task<int> SavePlanAsync(PlanViewModel model, CancellationToken cancellationToken = default)
        {
            Plan plan;
            if (model.PlanId != 0)
            {
                plan = await db.Plans.Include(p => p.Features).FirstOrDefaultAsync(p => p.PlanId == model.PlanId, cancellationToken)
                       ?? throw new InvalidOperationException($"Plan {model.PlanId} not found.");
            }
            else
            {
                plan = new Plan();
                db.Plans.Add(plan);
            }

            plan.Name = model.Name;
            plan.Description = model.Description;
            plan.Amount = model.Amount;
            plan.Currency = model.Currency;
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

            await db.SaveChangesAsync(cancellationToken);
            return plan.PlanId;
        }

        public Task PushPlanAsync(int planId, CancellationToken cancellationToken = default) => planSynchronizer.SyncPlanAsync(planId, cancellationToken);
    }
}
