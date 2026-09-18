using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.ViewModels;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers.Impl
{
    /// <summary>
    /// Default <see cref="IPaymentsHandler"/> over a context that hosts the payments tables and exposes the
    /// active tenant.
    /// </summary>
    public class PaymentsHandler<TContext, TTenant> : IPaymentsHandler
        where TContext : DbContext, IPaymentsContext, ITenantScopeContext
        where TTenant : Tenant
    {
        /// <summary>See the own payout account and the own sales.</summary>
        public const string ViewPermission = "TenantPayments.View";

        /// <summary>Set up the payout account, open the provider dashboard.</summary>
        public const string ManagePermission = "TenantPayments.Manage";

        /// <summary>Issue refunds.</summary>
        public const string RefundPermission = "TenantPayments.Refund";

        /// <summary>Platform-wide overview of all connected accounts.</summary>
        public const string AdminPermission = "TenantPayments.Admin";

        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IServiceProvider services;
        private readonly ITenantPaymentAccountService accountService;
        private readonly ITenantSaleService saleService;
        private readonly IGlobalSettings<TenantPaymentsOptions> settings;

        /// <summary>
        /// Master switch and tenant feature — the same bracket every sale passes through.
        /// </summary>
        private readonly PaymentsRuntime runtime;

        public PaymentsHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services,
            ITenantPaymentAccountService accountService, ITenantSaleService saleService,
            IGlobalSettings<TenantPaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates)
        {
            this.dbFactory = dbFactory;
            this.services = services;
            this.accountService = accountService;
            this.saleService = saleService;
            this.settings = settings;
            // With no gate registered the answer is NO — the same fail-closed direction the sale path takes.
            runtime = new PaymentsRuntime(settings, featureGates.FirstOrDefault());
        }

        /// <inheritdoc />
        public string DefaultCountry => settings.Value.DefaultCountry;

        /// <inheritdoc />
        public bool IsEnabled() => settings.Value.Enabled;

        /// <inheritdoc />
        public bool CanView() => services.VerifyUserPermissions(new[] { ViewPermission, ManagePermission, AdminPermission, ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin });

        /// <inheritdoc />
        public bool CanManage() => services.VerifyUserPermissions(new[] { ManagePermission, ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin });

        /// <inheritdoc />
        public bool CanRefund() => services.VerifyUserPermissions(new[] { RefundPermission, ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin });

        /// <inheritdoc />
        public bool CanAdminister() => services.VerifyUserPermissions(new[] { AdminPermission, ToolkitPermission.Sysadmin });

        /// <inheritdoc />
        public async Task<PaymentAccountViewModel> GetAccountAsync(CancellationToken cancellationToken = default)
        {
            var tenantId = await AuthorizeAsync(CanView(), "see the payout account of this tenant", cancellationToken);
            if (tenantId == null)
            {
                return new PaymentAccountViewModel();
            }

            return ToViewModel(await accountService.GetStatusAsync(tenantId.Value, cancellationToken));
        }

        /// <inheritdoc />
        public async Task<string> StartOnboardingAsync(string returnUrl, string refreshUrl, string? country, string? email, CancellationToken cancellationToken = default)
        {
            var tenantId = await RequireTenantAsync(CanManage(), "set up the payout account of this tenant", cancellationToken);
            return await accountService.StartOnboardingAsync(tenantId, returnUrl, refreshUrl, email, country, cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Deliberately VIEW and not MANAGE: this re-reads the tenant's own account from the provider and updates
        /// the local mirror — it changes nothing the tenant owns. The page runs it on the way back from the
        /// onboarding, and that return URL is a plain query marker anybody on the page can carry.
        /// </remarks>
        public async Task<PaymentAccountViewModel> RefreshAccountAsync(CancellationToken cancellationToken = default)
        {
            var tenantId = await AuthorizeAsync(CanView(), "re-read the payout account of this tenant", cancellationToken);
            if (tenantId == null)
            {
                return new PaymentAccountViewModel();
            }

            return ToViewModel(await accountService.RefreshAsync(tenantId.Value, cancellationToken));
        }

        /// <inheritdoc />
        /// <remarks>
        /// The ADMIN overload: it takes the tenant from the CALLER instead of the security scope, so it is the one
        /// method here that can touch a foreign tenant's account by nothing more than a guessed id. It is therefore
        /// gated by the platform permission rather than by the tenant's own one.
        /// </remarks>
        public async Task RefreshAccountAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            EnsureAdministration("re-read the payout account of another tenant");
            await accountService.RefreshAsync(tenantId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<string?> OpenDashboardAsync(CancellationToken cancellationToken = default)
        {
            var tenantId = await AuthorizeAsync(CanManage(), "open the provider dashboard of this tenant", cancellationToken);
            return tenantId == null ? null : await accountService.CreateDashboardLinkAsync(tenantId.Value, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<SalesOverviewViewModel> GetSalesAsync(DateTime? fromUtc, DateTime? toUtc, TenantSaleStatus? status, CancellationToken cancellationToken = default)
        {
            EnsurePermitted(CanView(), "see the sales of this tenant");
            runtime.EnsureEnabled();
            var overview = new SalesOverviewViewModel();
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var tenantId = db.CurrentTenantId;
            if (tenantId == null)
            {
                return overview;
            }

            await runtime.EnsureFeatureAsync(tenantId.Value, cancellationToken);

            var query = db.TenantSales.AsNoTracking().Include(s => s.Refunds).Where(s => s.TenantId == tenantId.Value);
            if (fromUtc != null)
            {
                query = query.Where(s => s.Created >= fromUtc.Value);
            }

            if (toUtc != null)
            {
                query = query.Where(s => s.Created < toUtc.Value);
            }

            if (status != null)
            {
                query = query.Where(s => s.Status == status.Value);
            }

            var sales = await query.OrderByDescending(s => s.Created).ToListAsync(cancellationToken);
            var items = sales.Select(s => new SaleListItemViewModel
            {
                TenantSaleId = s.TenantSaleId,
                Created = s.Created,
                PaidUtc = s.PaidUtc,
                ExternalReference = s.ExternalReference,
                Description = s.Description,
                Currency = s.Currency,
                AmountMinor = s.AmountMinor,
                ApplicationFeeMinor = s.ApplicationFeeMinor,
                RefundedMinor = s.Refunds.Sum(r => r.AmountMinor),
                ApplicationFeeRefundedMinor = s.Refunds.Sum(r => r.ApplicationFeeRefundedMinor),
                Status = s.Status,
                CustomerEmail = s.CustomerEmail,
                ProviderSessionId = s.ProviderSessionId,
                ProviderPaymentIntentId = s.ProviderPaymentIntentId,
                ProviderChargeId = s.ProviderChargeId
            }).ToList();

            overview.Items = items;
            // Only paid sales count towards the totals — a pending or expired one is not turnover, and showing it
            // as such is how a tenant ends up reconciling against a number that was never money.
            var settled = items.Where(i => i.Status is TenantSaleStatus.Paid or TenantSaleStatus.Refunded or TenantSaleStatus.PartiallyRefunded).ToList();
            overview.Currency = settled.Select(i => i.Currency).FirstOrDefault() ?? settings.Value.DefaultCurrency;
            overview.TotalAmountMinor = settled.Sum(i => i.AmountMinor);
            overview.TotalFeeMinor = settled.Sum(i => i.ApplicationFeeMinor - i.ApplicationFeeRefundedMinor);
            overview.TotalRefundedMinor = settled.Sum(i => i.RefundedMinor);
            overview.TotalNetMinor = settled.Sum(i => i.NetMinor);
            overview.Waiver = await BuildWaiverProgressAsync(db, tenantId.Value, cancellationToken);
            return overview;
        }

        /// <inheritdoc />
        public async Task RefundAsync(int tenantSaleId, long? amountMinor, string? reason, bool? refundApplicationFee, CancellationToken cancellationToken = default)
        {
            var tenantId = await RequireTenantAsync(CanRefund(), "refund sales of this tenant", cancellationToken);

            // The sale is re-checked against the ACTIVE tenant before it is handed to the service. The service
            // addresses sales by their own id and knows nothing about the current scope, so without this a
            // tenant could refund another tenant's sale by guessing an id.
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var belongs = await db.TenantSales.AsNoTracking()
                .AnyAsync(s => s.TenantSaleId == tenantSaleId && s.TenantId == tenantId, cancellationToken);
            if (!belongs)
            {
                throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound, $"Sale {tenantSaleId} does not belong to tenant {tenantId}.");
            }

            await saleService.RefundSaleAsync(tenantSaleId, amountMinor, reason, refundApplicationFee, cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The platform view over ALL connected accounts, and therefore the most expensive method here to leave
        /// unguarded: it reads past the tenant scope on purpose. The permission is checked HERE and not only on
        /// the page, because a second caller would otherwise read every tenant's turnover.
        /// </remarks>
        public async Task<IReadOnlyList<PaymentAccountAdminViewModel>> GetAdminOverviewAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
        {
            EnsureAdministration("see the payment overview across all tenants");
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var accounts = await db.TenantPaymentAccounts.AsNoTracking().OrderBy(a => a.TenantId).ToListAsync(cancellationToken);
            if (accounts.Count == 0)
            {
                return Array.Empty<PaymentAccountAdminViewModel>();
            }

            var tenantIds = accounts.Select(a => a.TenantId).ToList();
            var salesQuery = db.TenantSales.AsNoTracking().Include(s => s.Refunds)
                .Where(s => tenantIds.Contains(s.TenantId) && s.PaidUtc != null);
            if (fromUtc != null)
            {
                salesQuery = salesQuery.Where(s => s.PaidUtc >= fromUtc.Value);
            }

            if (toUtc != null)
            {
                salesQuery = salesQuery.Where(s => s.PaidUtc < toUtc.Value);
            }

            var sales = await salesQuery.ToListAsync(cancellationToken);
            var byTenant = sales.GroupBy(s => s.TenantId).ToDictionary(g => g.Key, g => g.ToList());

            // Tenant names come from the security model. Query filters are ignored: this is the platform view,
            // and it is gated by TenantPayments.Admin (checked at the top of this method) rather than by the
            // current tenant scope.
            var names = await db.Set<TTenant>().AsNoTracking().IgnoreQueryFilters()
                .Where(t => tenantIds.Contains(t.TenantId))
                .ToDictionaryAsync(t => t.TenantId, t => t.DisplayName ?? t.TenantName, cancellationToken);

            return accounts.Select(a =>
            {
                byTenant.TryGetValue(a.TenantId, out var tenantSales);
                tenantSales ??= new List<TenantSale>();
                return new PaymentAccountAdminViewModel
                {
                    TenantId = a.TenantId,
                    TenantName = names.TryGetValue(a.TenantId, out var name) ? name : null,
                    ProviderAccountId = a.ProviderAccountId,
                    AccountType = a.DashboardType,
                    Country = a.Country,
                    ChargesEnabled = a.ChargesEnabled,
                    PayoutsEnabled = a.PayoutsEnabled,
                    DetailsSubmitted = a.DetailsSubmitted,
                    Disconnected = a.Disconnected,
                    DisabledReason = a.DisabledReason,
                    OpenRequirements = CountRequirements(a.RequirementsJson),
                    Currency = tenantSales.Select(s => s.Currency).FirstOrDefault() ?? a.DefaultCurrency,
                    SalesCount = tenantSales.Count,
                    VolumeMinor = tenantSales.Sum(s => s.AmountMinor),
                    FeeMinor = tenantSales.Sum(s => s.ApplicationFeeMinor),
                    RefundedMinor = tenantSales.Sum(s => s.Refunds.Sum(r => r.AmountMinor)),
                    FeeRefundedMinor = tenantSales.Sum(s => s.Refunds.Sum(r => r.ApplicationFeeRefundedMinor)),
                    Updated = a.Updated
                };
            }).ToList();
        }

        /// <summary>
        /// Progress towards the waived base fee for the period CURRENTLY running. Deliberately the running one
        /// and not the closed one: what the tenant wants to know is whether the next invoice will be free.
        /// </summary>
        private async Task<WaiverProgressViewModel?> BuildWaiverProgressAsync(TContext db, int tenantId, CancellationToken cancellationToken)
        {
            var options = settings.Value;
            if (!options.VolumeWaiver.Enabled)
            {
                return null;
            }

            var currency = options.DefaultCurrency?.ToUpperInvariant();
            var waiver = VolumeWaiver.Resolve(options.VolumeWaiver, currency);
            if (waiver.ThresholdMinor <= 0)
            {
                return null;
            }

            // Calendar month as the display window. The real measurement follows the subscription period, which
            // the invoice defines — but that period is not known here, and an approximate progress bar is worth
            // more than none. The wording on the page says "next month", which is where the offset shows.
            var now = DateTime.UtcNow;
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);

            var paid = await db.TenantSales
                .Where(s => s.TenantId == tenantId && s.Currency == currency && s.PaidUtc != null && s.PaidUtc >= start && s.PaidUtc < end)
                .SumAsync(s => (long?)s.AmountMinor, cancellationToken) ?? 0;
            var refunded = await db.TenantSaleRefunds
                .Where(r => r.Sale != null && r.Sale.TenantId == tenantId && r.Sale.Currency == currency && r.Created >= start && r.Created < end)
                .SumAsync(r => (long?)r.AmountMinor, cancellationToken) ?? 0;

            return new WaiverProgressViewModel
            {
                Currency = currency ?? string.Empty,
                // Clamped: with many refunds the net turnover can go negative, and a negative progress bar helps
                // nobody.
                CurrentVolumeMinor = Math.Max(0, paid - refunded),
                ThresholdMinor = waiver.ThresholdMinor,
                PeriodStartUtc = start,
                PeriodEndUtc = end
            };
        }

        /// <summary>
        /// Checks permission, master switch and tenant feature, and returns the tenant in scope — null when there
        /// is none.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Here and not only in the view.</b> The page hides what somebody may not do; that guards the DISPLAY,
        /// not the operation. This handler is registered in DI and thus reachable from any component — without the
        /// check at this point, security would rest on nobody ever writing a second caller.
        /// </para>
        /// <para>
        /// The feature is checked ALONG WITH the permission: whoever had the payments module withdrawn should stop
        /// seeing its data too, not just stop selling. The sale path gets this from <c>PaymentsRuntime</c>; the
        /// read paths had no such net.
        /// </para>
        /// </remarks>
        private async Task<int?> AuthorizeAsync(bool permitted, string what, CancellationToken cancellationToken)
        {
            EnsurePermitted(permitted, what);
            runtime.EnsureEnabled();
            var tenantId = await CurrentTenantAsync(cancellationToken);
            if (tenantId != null)
            {
                await runtime.EnsureFeatureAsync(tenantId.Value, cancellationToken);
            }

            return tenantId;
        }

        /// <summary>
        /// Same as <see cref="AuthorizeAsync"/> for the paths that cannot fall back to an empty view model: without
        /// a tenant there is no account to onboard and no sale to refund.
        /// </summary>
        private async Task<int> RequireTenantAsync(bool permitted, string what, CancellationToken cancellationToken)
            => await AuthorizeAsync(permitted, what, cancellationToken)
               ?? throw new TenantPaymentException(PaymentErrorCodes.NoAccount,
                   $"There is no tenant in scope, so the acting user cannot {what}.");

        /// <summary>
        /// The platform paths. They deliberately do NOT check the current tenant's feature: an administrator may
        /// well be working from a tenant that never bought the payments module.
        /// </summary>
        private void EnsureAdministration(string what)
        {
            EnsurePermitted(CanAdminister(), what);
            runtime.EnsureEnabled();
        }

        private static void EnsurePermitted(bool permitted, string what)
        {
            if (!permitted)
            {
                throw new TenantPaymentException(PaymentErrorCodes.NotPermitted,
                    $"The acting user may not {what}.");
            }
        }

        private async Task<int?> CurrentTenantAsync(CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            return db.CurrentTenantId;
        }

        private static PaymentAccountViewModel ToViewModel(TenantPaymentAccountStatus? status)
        {
            if (status == null)
            {
                return new PaymentAccountViewModel();
            }

            return new PaymentAccountViewModel
            {
                HasAccount = true,
                ProviderAccountId = status.ProviderAccountId,
                AccountType = status.AccountType,
                Country = status.Country,
                DefaultCurrency = status.DefaultCurrency,
                ChargesEnabled = status.ChargesEnabled,
                PayoutsEnabled = status.PayoutsEnabled,
                DetailsSubmitted = status.DetailsSubmitted,
                Disconnected = status.Disconnected,
                DisabledReason = status.DisabledReason,
                CurrentlyDue = status.CurrentlyDue,
                PastDue = status.PastDue,
                PendingVerification = status.PendingVerification,
                CurrentDeadline = status.CurrentDeadline,
                CanSell = status.CanSell,
                Updated = status.Updated
            };
        }

        /// <summary>Rough count of outstanding requirements for the admin list — the detail lives on the tenant's page.</summary>
        private static int CountRequirements(string? requirementsJson)
        {
            if (string.IsNullOrWhiteSpace(requirementsJson))
            {
                return 0;
            }

            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(requirementsJson);
                var count = 0;
                foreach (var property in new[] { "currentlyDue", "pastDue" })
                {
                    if (document.RootElement.TryGetProperty(property, out var array) && array.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        count += array.GetArrayLength();
                    }
                }

                return count;
            }
            catch (System.Text.Json.JsonException ex)
            {
                // Showing "0 open" for an unreadable mirror would be a lie in the reassuring direction, so it is
                // logged rather than passed over.
                ITVComponents.Logging.LogEnvironment.LogEvent(
                    $"Could not read the mirrored connect requirements for the admin overview; the account is listed with 0 open items: {ITVComponents.Helpers.ExceptionHelper.OutlineException(ex)}",
                    ITVComponents.Logging.LogSeverity.Warning, "TenantPayments");
                return 0;
            }
        }
    }
}
