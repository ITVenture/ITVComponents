using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Findet das Gerät über den <see cref="IAgentConnector"/>: Dienst = <see cref="TenantPaymentTerminal.Route"/>,
    /// Objekt = <see cref="AgentTerminals.ObjectNameSetting"/> aus der Konfiguration.
    /// </summary>
    /// <remarks>
    /// Der Mandant der Geräte-Zeile geht mit in die Auflösung. Zeigt die Route auf die Kasse eines anderen
    /// Mandanten, gibt es dieses Gerät schlicht nicht.
    /// </remarks>
    public class AgentConnectorTerminalLocator : ITerminalAgentLocator
    {
        private readonly IAgentConnector connector;

        /// <summary>Initializes a new instance of the <see cref="AgentConnectorTerminalLocator"/> class.</summary>
        public AgentConnectorTerminalLocator(IAgentConnector connector)
        {
            this.connector = connector;
        }

        /// <inheritdoc />
        public async Task<ITerminalDevice> GetDeviceAsync(TenantPaymentTerminal terminal,
            CancellationToken cancellationToken = default)
        {
            var (route, objectName) = Address(terminal.Route, terminal.ConfigurationJson, terminal.DisplayName);
            try
            {
                return await connector.GetObjectAsync<ITerminalDevice>(terminal.TenantId, route, objectName,
                    cancellationToken);
            }
            catch (AgentConnectionException ex)
            {
                throw new TerminalDeviceException($"The agent of terminal '{terminal.DisplayName}' cannot be reached: {ex.Message}", ex);
            }
        }

        /// <summary>Dienst und Objekt eines Agent-Terminals, geprüft.</summary>
        /// <exception cref="TerminalDeviceException">wenn eines davon fehlt</exception>
        internal static (string Route, string ObjectName) Address(string? route, string? configurationJson, string? displayName)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                throw new TerminalDeviceException($"Terminal '{displayName}' names no till (route), so there is no agent to ask.");
            }

            var objectName = AgentTerminals.ObjectNameOf(configurationJson);
            if (string.IsNullOrWhiteSpace(objectName))
            {
                throw new TerminalDeviceException($"Terminal '{displayName}' names no device object on its agent.");
            }

            return (route, objectName);
        }
    }
}
