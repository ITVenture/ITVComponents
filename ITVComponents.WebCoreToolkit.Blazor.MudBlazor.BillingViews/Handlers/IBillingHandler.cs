using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers
{
    /// <summary>
    /// Die Bereiche der Plattform-Verwaltung. Jeder hat ein eigenes Paar Rechte
    /// (<c>Billing.&lt;Bereich&gt;.View</c> / <c>.Write</c>).
    /// </summary>
    /// <remarks>
    /// Ein Aufzaehlungstyp statt sechs Methoden: die Bereiche verhalten sich gleich, und ein siebter kommt
    /// dann mit einem Wert und einem Zweig aus, statt mit zwei weiteren Methoden im Vertrag.
    /// </remarks>
    public enum BillingArea
    {
        /// <summary>Die Plaene (<c>/Billing/Plans</c>).</summary>
        Plans,

        /// <summary>Die Zusatzleistungen (<c>/Billing/AddOns</c>).</summary>
        AddOns,

        /// <summary>Die Abo-Liste ueber alle Mandanten (<c>/Billing/Subscriptions</c>).</summary>
        Subscriptions
    }

    /// <summary>
    /// Data/operation seam for the billing UI. Resolves the active tenant from the security scope, reads the
    /// plan/add-on catalog and the tenant's subscription, and drives checkout/portal/plan-sync through the
    /// provider service layer.
    /// <para>
    /// The <c>Can*</c> members are for the VIEW, so it can hide what is not permitted. The operations check their
    /// own permission again and throw when it is missing — an implementation of this interface is reachable from
    /// any component, and plan authoring writes prices.
    /// </para>
    /// </summary>
    public interface IBillingHandler
    {
        bool HasPermission(params string[] permissions);

        /// <summary>True if billing management is permitted for the current user (ManageSubscription / admin).</summary>
        bool CanManage(ClaimsPrincipal user);

        /// <summary>
        /// Darf der Handelnde diesen Bereich SEHEN? Schreibrecht schliesst Lesen ein, Sysadmin beides.
        /// </summary>
        /// <remarks>
        /// Loest <c>CanAdminister</c> ab, das ausschliesslich auf <c>Sysadmin</c> prueft und die
        /// <c>Billing.*</c>-Rechte damit zu toten Buchstaben machte: sie oeffneten die Seite, und dahinter
        /// war alles zu. <b>Breaking</b> fuer Konsumenten, die den alten Namen rufen.
        /// </remarks>
        bool CanView(BillingArea area);

        /// <summary>Darf der Handelnde in diesem Bereich SCHREIBEN?</summary>
        bool CanWrite(BillingArea area);

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
