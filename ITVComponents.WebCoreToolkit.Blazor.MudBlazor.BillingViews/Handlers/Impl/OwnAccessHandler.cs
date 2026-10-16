using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers.Impl
{
    /// <summary>
    /// Die eigenen Anbieter-Zugänge über einen Kontext, der die Zahlungstabellen führt und den aktiven Mandanten
    /// kennt.
    /// </summary>
    /// <remarks>
    /// Dieselben Rechte wie die Geräte-Verwaltung: wer Terminals verwalten darf, darf auch das Konto anbinden, über
    /// das sie kassieren. Ein eigenes Recht hiesse, dass jemand ein Terminal anlegen kann, das an einem Konto hängt,
    /// das er nicht sehen darf.
    /// </remarks>
    public class OwnAccessHandler<TContext> : IOwnAccessHandler
        where TContext : DbContext, IPaymentsContext, ITenantScopeContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IServiceProvider services;
        private readonly IProviderAccessAdministration? administration;
        private readonly IGlobalSettings<TenantPaymentsOptions> settings;
        private readonly PaymentsRuntime runtime;

        /// <summary>Initializes a new instance of the <see cref="OwnAccessHandler{TContext}"/> class.</summary>
        public OwnAccessHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services,
            IGlobalSettings<TenantPaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates,
            IProviderAccessAdministration? administration = null)
        {
            this.dbFactory = dbFactory;
            this.services = services;
            this.settings = settings;
            this.administration = administration;
            runtime = new PaymentsRuntime(settings, featureGates.FirstOrDefault());
        }

        /// <inheritdoc />
        public bool IsEnabled() => settings.Value.Enabled;

        /// <inheritdoc />
        public bool IsAvailable() => administration != null && administration.GetProviders().Count != 0;

        /// <inheritdoc />
        public bool CanView() => services.VerifyUserPermissions(
            [TerminalsHandler<TContext>.ViewPermission, TerminalsHandler<TContext>.ManagePermission,
                ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin]);

        /// <inheritdoc />
        public bool CanManage() => services.VerifyUserPermissions(
            [TerminalsHandler<TContext>.ManagePermission, ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin]);

        /// <inheritdoc />
        public IReadOnlyList<string> GetProviders()
        {
            EnsureCatalogAccess();
            return administration?.GetProviders() ?? [];
        }

        /// <inheritdoc />
        public IReadOnlyList<OwnAccessField> DescribeFields(string provider)
        {
            EnsureCatalogAccess();
            return Administration().DescribeFields(provider);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<OwnAccessInfo>> GetAsync(CancellationToken cancellationToken = default)
        {
            var tenantId = await EnsureAsync(CanView(), cancellationToken);
            return administration == null ? [] : await administration.GetAsync(tenantId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> CreateAsync(string provider, string displayName, IReadOnlyDictionary<string, string?> values,
            CancellationToken cancellationToken = default)
            => await Administration().CreateAsync(await EnsureAsync(CanManage(), cancellationToken), provider, displayName,
                values, cancellationToken);

        /// <inheritdoc />
        public async Task RenewSecretsAsync(int accessId, IReadOnlyDictionary<string, string?> values,
            CancellationToken cancellationToken = default)
            => await Administration().RenewSecretsAsync(await EnsureAsync(CanManage(), cancellationToken), accessId, values,
                cancellationToken);

        /// <inheritdoc />
        public async Task SetDisabledAsync(int accessId, bool disabled, CancellationToken cancellationToken = default)
            => await Administration().SetDisabledAsync(await EnsureAsync(CanManage(), cancellationToken), accessId, disabled,
                cancellationToken);

        /// <inheritdoc />
        /// <remarks>Verwaltungsrecht: die Prüfung redet mit dem Konto des Mandanten und schreibt das Ergebnis an die Zeile.</remarks>
        public async Task<OwnAccessCheck> VerifyAsync(int accessId, CancellationToken cancellationToken = default)
            => await Administration().VerifyAsync(await EnsureAsync(CanManage(), cancellationToken), accessId, cancellationToken);

        /// <inheritdoc />
        public async Task<OwnAccessCheck> SetUpWebhooksAsync(int accessId, string publicBaseUrl,
            CancellationToken cancellationToken = default)
            => await Administration().SetUpWebhooksAsync(await EnsureAsync(CanManage(), cancellationToken), accessId,
                publicBaseUrl, cancellationToken);

        private IProviderAccessAdministration Administration()
            => administration ?? throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                "Own provider accesses are not available: no IProviderAccessAdministration is registered (it comes with the provider packages).");

        private async Task<int> EnsureAsync(bool permitted, CancellationToken cancellationToken)
        {
            EnsurePermitted(permitted);
            runtime.EnsureEnabled();
            var tenantId = await CurrentTenantAsync(cancellationToken);
            await runtime.EnsureFeatureAsync(tenantId, cancellationToken);
            return tenantId;
        }

        private void EnsureCatalogAccess()
        {
            EnsurePermitted(CanView());
            runtime.EnsureEnabled();
        }

        private static void EnsurePermitted(bool permitted)
        {
            if (!permitted)
            {
                throw new TenantPaymentException(PaymentErrorCodes.NotPermitted,
                    "The acting user may not manage the provider accesses of this tenant.");
            }
        }

        private async Task<int> CurrentTenantAsync(CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            return db.CurrentTenantId
                   ?? throw new TenantPaymentException(PaymentErrorCodes.NoAccount,
                       "There is no tenant in scope. Provider accesses belong to a tenant, so this page cannot be used outside one.");
        }
    }
}
