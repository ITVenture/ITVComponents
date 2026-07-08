using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers
{
    /// <summary>
    /// Data/operation seam for the billing UI. Resolves the active tenant from the security scope, reads the
    /// plan/add-on catalog and the tenant's subscription, and drives checkout/portal/plan-sync through the
    /// provider service layer.
    /// </summary>
    public interface IBillingHandler
    {
        bool HasPermission(ClaimsPrincipal user, params string[] permissions);

        /// <summary>True if billing management is permitted for the current user (ManageSubscription / admin).</summary>
        bool CanManage(ClaimsPrincipal user);

        /// <summary>True if the current user may author plans (admin).</summary>
        bool CanAdminister(ClaimsPrincipal user);

        Task<SubscriptionOverviewViewModel> GetOverviewAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

        /// <summary>
        /// The tenant-security feature catalog (all defined features), for offering plan/add-on feature grants as a
        /// picker instead of free-text keys. Admin-only data.
        /// </summary>
        Task<IReadOnlyList<FeatureCatalogItemViewModel>> GetFeatureCatalogAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PlanViewModel>> GetActivePlansAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AddOnViewModel>> GetActiveAddOnsAsync(CancellationToken cancellationToken = default);

        /// <summary>Starts a checkout for the current tenant in the given currency and returns the hosted URL to redirect to.</summary>
        Task<string> StartCheckoutAsync(ClaimsPrincipal user, int planId, IReadOnlyCollection<int> addOnIds, string successUrl, string cancelUrl, string? currency = null, CancellationToken cancellationToken = default);

        /// <summary>Opens the customer portal for the current tenant; null if there is no provider customer yet.</summary>
        Task<string?> OpenPortalAsync(ClaimsPrincipal user, string returnUrl, CancellationToken cancellationToken = default);

        // -- admin (plan authoring) --
        Task<IReadOnlyList<PlanViewModel>> GetAllPlansAsync(CancellationToken cancellationToken = default);

        Task<int> SavePlanAsync(PlanViewModel model, CancellationToken cancellationToken = default);

        /// <summary>Pushes a plan to the payment provider (creates/updates Product+Price, stores ids).</summary>
        Task PushPlanAsync(int planId, CancellationToken cancellationToken = default);

        // -- admin (add-on authoring) --
        Task<IReadOnlyList<AddOnViewModel>> GetAllAddOnsAsync(CancellationToken cancellationToken = default);

        Task<int> SaveAddOnAsync(AddOnViewModel model, CancellationToken cancellationToken = default);

        /// <summary>Pushes an add-on to the payment provider (creates/updates Product+Price, stores ids).</summary>
        Task PushAddOnAsync(int addOnId, CancellationToken cancellationToken = default);

        // -- admin (subscription overview) --

        /// <summary>Read-only list of all tenant subscriptions (admin overview across tenants).</summary>
        Task<IReadOnlyList<SubscriptionAdminViewModel>> GetAllSubscriptionsAsync(CancellationToken cancellationToken = default);
    }
}
