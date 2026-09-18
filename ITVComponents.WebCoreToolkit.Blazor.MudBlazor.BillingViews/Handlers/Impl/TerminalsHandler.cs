using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
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
    /// Die Geräte-Verwaltung über einen Kontext, der die Zahlungstabellen führt und den aktiven
    /// Mandanten kennt.
    /// </summary>
    public class TerminalsHandler<TContext> : ITerminalsHandler
        where TContext : DbContext, IPaymentsContext, ITenantScopeContext
    {
        /// <summary>Die Geräte sehen.</summary>
        public const string ViewPermission = "TenantPayments.View";

        /// <summary>Geräte anlegen, ändern, abschalten.</summary>
        public const string ManagePermission = "TenantPayments.Manage";

        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IServiceProvider services;
        private readonly ITerminalAdministration administration;
        private readonly ITerminalProviderCatalog catalog;
        private readonly ITerminalPaymentService terminals;
        private readonly IEnumerable<ITerminalChoiceProvider> choiceProviders;
        private readonly IGlobalSettings<TenantPaymentsOptions> settings;

        /// <summary>Initializes a new instance of the <see cref="TerminalsHandler{TContext}"/> class.</summary>
        public TerminalsHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services,
            ITerminalAdministration administration, ITerminalProviderCatalog catalog,
            ITerminalPaymentService terminals, IEnumerable<ITerminalChoiceProvider> choiceProviders,
            IGlobalSettings<TenantPaymentsOptions> settings)
        {
            this.dbFactory = dbFactory;
            this.services = services;
            this.administration = administration;
            this.catalog = catalog;
            this.terminals = terminals;
            this.choiceProviders = choiceProviders;
            this.settings = settings;
        }

        /// <inheritdoc />
        public bool IsEnabled() => settings.Value.Enabled;

        /// <inheritdoc />
        public bool CanView() => services.VerifyUserPermissions(
            [ViewPermission, ManagePermission, ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin]);

        /// <inheritdoc />
        public bool CanManage() => services.VerifyUserPermissions(
            [ManagePermission, ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin]);

        /// <inheritdoc />
        public async Task<IReadOnlyList<TerminalDefinition>> GetTerminalsAsync(
            CancellationToken cancellationToken = default)
            => await administration.GetAsync(await CurrentTenantAsync(cancellationToken), cancellationToken);

        /// <inheritdoc />
        public IReadOnlyList<TerminalProviderInfo> GetProviders() => catalog.GetProviders();

        /// <inheritdoc />
        public IReadOnlyList<TerminalSettingDescriptor> DescribeSettings(string providerKey)
            => catalog.DescribeSettings(providerKey);

        /// <inheritdoc />
        public Task<IReadOnlyList<TerminalSettingDescriptor>> DescribeDeviceSettingsAsync(string providerKey,
            string? configurationJson, CancellationToken cancellationToken = default)
            => catalog.DescribeDeviceSettingsAsync(providerKey, configurationJson, cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<TerminalSettingChoice>> GetChoicesAsync(string source,
            string? dependsOnValue, CancellationToken cancellationToken = default)
        {
            foreach (var provider in choiceProviders)
            {
                if (provider.Handles(source))
                {
                    return await provider.GetChoicesAsync(source, dependsOnValue, cancellationToken);
                }
            }

            // Kein Anbieter fuer diese Quelle. Kein Fehler - die Maske zeichnet dann ein Textfeld.
            return [];
        }

        /// <inheritdoc />
        /// <remarks>
        /// Der Mandant kommt aus dem Sicherheitsbereich, <b>nicht</b> aus der Anfrage: sonst liesse sich
        /// ein Gerät für einen fremden Mandanten anlegen, indem man die Id austauscht.
        /// </remarks>
        public async Task<int> SaveAsync(TerminalDefinition definition, CancellationToken cancellationToken = default)
        {
            definition.TenantId = await CurrentTenantAsync(cancellationToken);
            return await administration.SaveAsync(definition, cancellationToken);
        }

        /// <inheritdoc />
        public async Task SetEnabledAsync(int terminalId, bool enabled, CancellationToken cancellationToken = default)
            => await administration.SetEnabledAsync(await CurrentTenantAsync(cancellationToken), terminalId, enabled,
                cancellationToken);

        /// <inheritdoc />
        public async Task<TerminalStatus> GetStatusAsync(int terminalId, CancellationToken cancellationToken = default)
            => await terminals.GetTerminalStatusAsync(await CurrentTenantAsync(cancellationToken), terminalId,
                cancellationToken);

        /// <summary>
        /// Der Mandant, in dessen Namen gearbeitet wird.
        /// </summary>
        /// <remarks>
        /// Steht keiner im Bereich, wird abgebrochen statt auf 0 auszuweichen. Eine 0 waere eine Zahl
        /// wie jede andere: die Abfragen liefen durch, zeigten nichts an, und ein gespeichertes Geraet
        /// gehoerte niemandem. Ein Satz, der sagt was fehlt, ist die deutlich billigere Variante.
        /// </remarks>
        private async Task<int> CurrentTenantAsync(CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            return db.CurrentTenantId
                   ?? throw new TenantPaymentException(PaymentErrorCodes.NoAccount,
                       "There is no tenant in scope. Payment terminals belong to a tenant, so this page cannot be used outside one.");
        }
    }

    /// <summary>
    /// Füllt die Auswahlliste der Client-Anwendungen eines Mandanten.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der gespeicherte Wert ist der <c>ClientKey</c>: unter ihm meldet sich eine Anwendung am
    /// Dienst-Verteiler an, und sein eindeutiger Index gilt systemweit. Angezeigt wird der
    /// Anzeigename, weil „Kasse 1" lesbarer ist als eine Zeichenfolge.
    /// </para>
    /// <para>
    /// <typeparamref name="TClientApp"/> muss der Host nennen — das WebPart bindet Typparameter nach
    /// NAMEN aus dem Sicherheitskontext, und <c>TClientApp</c> ist einer von dessen eigenen Namen.
    /// </para>
    /// </remarks>
    public class ClientAppTerminalChoiceProvider<TContext, TClientApp> : ITerminalChoiceProvider
        where TContext : DbContext, ITenantScopeContext
        where TClientApp : class
    {
        private readonly IDbContextFactory<TContext> dbFactory;

        /// <summary>Initializes a new instance of the <see cref="ClientAppTerminalChoiceProvider{TContext, TClientApp}"/> class.</summary>
        public ClientAppTerminalChoiceProvider(IDbContextFactory<TContext> dbFactory)
        {
            this.dbFactory = dbFactory;
        }

        /// <inheritdoc />
        public bool Handles(string source)
            => string.Equals(source, TerminalChoiceSources.ClientApps, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        /// <remarks>
        /// Die Felder werden über <c>EF.Property</c> gelesen und nicht über eine Eigenschaft: die
        /// Basisklasse der Client-Anwendungen trägt neunzehn Typparameter, und ein Zugriff über ein
        /// schmales Interface wäre in einem EF-Ausdruck nicht übersetzbar — dieselbe Falle wie beim
        /// Mandanten-Interceptor.
        /// </remarks>
        public async Task<IReadOnlyList<TerminalSettingChoice>> GetChoicesAsync(string source,
            string? dependsOnValue, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            if (db.CurrentTenantId is not { } tenantId)
            {
                // Ohne Mandant keine Liste - und ganz sicher nicht die Kassen ALLER Mandanten.
                return [];
            }

            var rows = await db.Set<TClientApp>().AsNoTracking()
                .Where(a => EF.Property<int>(a, "TenantId") == tenantId)
                .OrderBy(a => EF.Property<string>(a, "ClientName"))
                .Select(a => new
                {
                    Key = EF.Property<string>(a, "ClientKey"),
                    Name = EF.Property<string>(a, "ClientName")
                })
                .ToListAsync(cancellationToken);

            return [.. rows.Select(r => new TerminalSettingChoice { Value = r.Key, Label = r.Name })];
        }
    }
}
