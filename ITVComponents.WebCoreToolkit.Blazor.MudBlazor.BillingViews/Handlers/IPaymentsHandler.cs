using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers
{
    /// <summary>
    /// Data/operation seam for the payments UI (axis B). Resolves the active tenant from the security scope and
    /// drives onboarding, sales and refunds through the provider service layer.
    /// <para>
    /// The permission checks here guard the DISPLAY. The service layer checks again by tenant id — it also
    /// serves callers that have no security scope at all.
    /// </para>
    /// </summary>
    public interface IPaymentsHandler
    {
        /// <summary>May see the payout account and the own sales.</summary>
        bool CanView();

        /// <summary>May set up the payout account and open the provider dashboard.</summary>
        bool CanManage();

        /// <summary>May issue refunds.</summary>
        bool CanRefund();

        /// <summary>May see the platform-wide overview of all connected accounts.</summary>
        bool CanAdminister();

        /// <summary>True when the module is switched on for this deployment at all.</summary>
        bool IsEnabled();

        /// <summary>State of the current tenant's payout account.</summary>
        Task<PaymentAccountViewModel> GetAccountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates or continues the hosted onboarding and returns the URL to redirect to. Both URLs must be
        /// ABSOLUTE and carry the tenant path prefix — the prefix middleware strips it before routing, so a
        /// root-absolute URL would bring the tenant back into the wrong tenant.
        /// </summary>
        /// <param name="returnUrl">Where the provider sends the tenant after the onboarding.</param>
        /// <param name="refreshUrl">Where the provider sends the tenant when the link has expired.</param>
        /// <param name="country">
        /// ISO-3166 country for a NEW account, confirmed by the tenant. The provider fixes it at creation and
        /// never allows a change, so it is asked rather than quietly taken from the configured default.
        /// </param>
        /// <param name="email">E-mail seeded into the onboarding form; usually the acting user's.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<string> StartOnboardingAsync(string returnUrl, string refreshUrl, string? country, string? email, CancellationToken cancellationToken = default);

        /// <summary>The configured default country, offered as the pre-selection on the setup form.</summary>
        string DefaultCountry { get; }

        /// <summary>Re-reads the account from the provider. Used on the return from the onboarding.</summary>
        Task<PaymentAccountViewModel> RefreshAccountAsync(CancellationToken cancellationToken = default);

        /// <summary>One-time link into the provider dashboard; null for account types that have their own login.</summary>
        Task<string?> OpenDashboardAsync(CancellationToken cancellationToken = default);

        /// <summary>The tenant's sales in a window, optionally filtered by status.</summary>
        Task<SalesOverviewViewModel> GetSalesAsync(DateTime? fromUtc, DateTime? toUtc, TenantSaleStatus? status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refunds a sale. <paramref name="amountMinor"/> null refunds the remainder,
        /// <paramref name="refundApplicationFee"/> null takes the configured default.
        /// </summary>
        Task RefundAsync(int tenantSaleId, long? amountMinor, string? reason, bool? refundApplicationFee, CancellationToken cancellationToken = default);

        /// <summary>All connected accounts with their turnover — the platform view.</summary>
        Task<IReadOnlyList<PaymentAccountAdminViewModel>> GetAdminOverviewAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);

        /// <summary>Re-reads one tenant's account from the provider (admin action).</summary>
        Task RefreshAccountAsync(int tenantId, CancellationToken cancellationToken = default);
    }
}
