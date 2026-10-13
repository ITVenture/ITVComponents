using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Kassieren über ein Terminal, das an einem Kassen-PC hängt statt in der Cloud eines Anbieters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der vierte Weg, und der einzige, bei dem wir nicht selbst mit dem Anbieter reden. Gebraucht wird
    /// er für Geräte, die nur lokal ansprechbar sind — wallee über die Local Till Interface etwa, wo
    /// Kasse und Terminal im selben Netz hängen und die Cloud nichts davon weiss.
    /// </para>
    /// <para>
    /// <b>Hier steht auffällig wenig, und das ist der Punkt.</b> Diese Klasse reicht durch und bucht
    /// nichts selbst: die Buchführung liegt in der Basis, also am Server. Was der Agent tut, ist mit dem
    /// Gerät zu reden — mehr darf ihm nicht anvertraut werden, weil er der Rechner ist, der mitten in
    /// einer Zahlung neu startet.
    /// </para>
    /// <para>
    /// Das Gerät findet der <see cref="ITerminalAgentLocator"/> (mitgeliefert: über den
    /// <see cref="IAgentConnector"/>); eingerichtet wird es über den Katalog des Agenten. <b>Die Einstellungen
    /// des Geräts liegen am Agenten</b>, nicht in der Geräte-Zeile — die Aufrufe tragen nur das Was.
    /// </para>
    /// </remarks>
    public class AgentTerminalPaymentService<TContext> : TerminalPaymentServiceBase<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        /// <summary>Der Name, unter dem dieser Weg an einem Gerät eingetragen wird.</summary>
        public const string Key = "agent";

        private readonly ITerminalAgentLocator locator;

        private readonly IAgentConnector connector;

        /// <summary>Initializes a new instance of the <see cref="AgentTerminalPaymentService{TContext}"/> class.</summary>
        public AgentTerminalPaymentService(IDbContextFactory<TContext> dbFactory, PaymentsRuntime runtime,
            TenantSaleWebhookSink<TContext> sink, ITerminalAgentLocator locator, IAgentConnector connector)
            : base(dbFactory, runtime, sink)
        {
            this.locator = locator;
            this.connector = connector;
        }

        /// <inheritdoc />
        protected override string ProviderKey => Key;

        /// <inheritdoc />
        /// <remarks>
        /// Der erste Schritt fragt nur, WO das Gerät hängt. Was es selbst braucht, weiss nur es —
        /// danach fragt der zweite Schritt.
        /// </remarks>
        public override IReadOnlyList<TerminalSettingDescriptor> DescribeSettings() =>
        [
            new()
            {
                Name = "route",
                Label = "Kasse",
                Kind = SettingKind.Choice,
                // Die Client-Anwendungen des Mandanten. Der gespeicherte Wert ist ihr ClientKey - der
                // traegt einen systemweit eindeutigen Index und ist damit als Dienstname auch ueber
                // Mandantengrenzen kollisionsfrei, anders als ein Anzeigename.
                ChoiceSource = TerminalChoiceSources.ClientApps,
                StoredIn = TerminalSettingTarget.Route,
                Required = true,
                HelpText = "Der Rechner, an dem das Terminal hängt. Er meldet sich am Dienst-Verteiler unter seinem ClientKey an."
            },
            new()
            {
                Name = AgentTerminals.ObjectNameSetting,
                Label = "Geräte-Objekt auf dieser Kasse",
                Kind = SettingKind.Choice,
                ChoiceSource = TerminalChoiceSources.RemoteObjects,
                // Ohne diese Angabe zeigte die Maske weiter die Objekte der zuvor gewaehlten Kasse.
                DependsOn = "route",
                Required = true,
                HelpText = "Die Terminal-Anbindung, die dort bereitsteht. Unter diesem Namen entsteht der Proxy."
            }
        ];

        /// <inheritdoc />
        public override bool HasDeviceSettings => true;

        /// <inheritdoc />
        /// <remarks>
        /// <b>Hier wird der Agent gefragt — aber nicht das Gerät.</b> Der Katalog des Agenten beschreibt die
        /// Einstellungen über den Typ; ein Terminal, dessen Konstruktor die Verbindung öffnet, springt nicht
        /// deshalb an, weil jemand die Maske aufklappt.
        /// </remarks>
        public override async Task<IReadOnlyList<SettingDescriptor>> DescribeDeviceSettingsAsync(
            TerminalDefinition draft, CancellationToken cancellationToken = default)
        {
            var (catalog, objectName) = await CatalogAsync(draft, cancellationToken);
            return await catalog.DescribeSettingsAsync(objectName);
        }

        /// <inheritdoc />
        public override async Task<string?> GetDeviceSettingsAsync(TerminalDefinition draft,
            CancellationToken cancellationToken = default)
        {
            var (catalog, objectName) = await CatalogAsync(draft, cancellationToken);
            return await catalog.GetSettingsAsync(objectName);
        }

        /// <inheritdoc />
        public override async Task<IReadOnlyList<SettingChoice>> GetDeviceChoicesAsync(TerminalDefinition draft,
            string settingName, string? draftJson, CancellationToken cancellationToken = default)
        {
            var (catalog, objectName) = await CatalogAsync(draft, cancellationToken);
            return await catalog.GetChoicesAsync(objectName, settingName, draftJson);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Die Werte gehen an den Agenten und liegen danach dort — nicht in der Geräte-Zeile. Geprüft wird am
        /// Agenten gegen den Einstellungstyp des Geräts.
        /// </remarks>
        public override async Task<ApplySettingsResult> ApplyDeviceSettingsAsync(TerminalDefinition draft,
            string settingsJson, CancellationToken cancellationToken = default)
        {
            var (catalog, objectName) = await CatalogAsync(draft, cancellationToken);
            return await catalog.ApplySettingsAsync(objectName, settingsJson);
        }

        /// <summary>Der Katalog des Agenten, auf den der Entwurf zeigt, und das Objekt dort.</summary>
        private async Task<(IAgentObjectCatalog Catalog, string ObjectName)> CatalogAsync(TerminalDefinition draft,
            CancellationToken cancellationToken)
        {
            var (route, objectName) = AgentConnectorTerminalLocator.Address(draft.Route, draft.ConfigurationJson,
                draft.DisplayName);
            try
            {
                return (await connector.GetCatalogAsync(draft.TenantId, route, cancellationToken), objectName);
            }
            catch (AgentConnectionException ex)
            {
                throw new TerminalDeviceException($"The agent '{route}' cannot be reached: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Das Ziel für das Gerät: nur die Kennung. Die Einstellungen hält das Gerät selbst.
        /// </summary>
        private static TerminalTarget AgentTargetOf(TenantPaymentTerminal terminal)
            => new() { TerminalId = terminal.ProviderTerminalId };

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> StartAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, TerminalPaymentCommand command, CancellationToken cancellationToken)
        {
            var device = await locator.GetDeviceAsync(terminal, cancellationToken);
            return await device.StartPaymentAsync(AgentTargetOf(terminal), command, cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> QueryDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
        {
            var device = await locator.GetDeviceAsync(terminal, cancellationToken);
            return await device.GetPaymentAsync(AgentTargetOf(terminal), operationId, cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> CancelAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
        {
            var device = await locator.GetDeviceAsync(terminal, cancellationToken);
            return await device.CancelPaymentAsync(AgentTargetOf(terminal), operationId, cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task<TerminalStatus> QueryStatusAsync(TenantPaymentTerminal terminal,
            CancellationToken cancellationToken)
        {
            var device = await locator.GetDeviceAsync(terminal, cancellationToken);
            return await device.GetStatusAsync(AgentTargetOf(terminal), cancellationToken);
        }
    }
}
