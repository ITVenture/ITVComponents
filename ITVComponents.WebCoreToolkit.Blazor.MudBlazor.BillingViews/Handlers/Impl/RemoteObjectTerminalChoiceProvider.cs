using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.InterProcessExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers.Impl
{
    /// <summary>
    /// Füllt die Auswahl „Geräte-Objekt auf dieser Kasse": die Terminals, die der Agent der gewählten Kasse
    /// anbietet.
    /// </summary>
    /// <remarks>
    /// Fragt einen <see cref="IRemoteObjectCatalog"/> — mitgeliefert vom AgentServices-Webpaket. Ist keiner
    /// registriert oder der Agent nicht erreichbar, bleibt die Liste leer, und die Maske zeichnet ein
    /// Textfeld. Gesagt wird beides im Protokoll.
    /// </remarks>
    public class RemoteObjectTerminalChoiceProvider : ITerminalChoiceProvider
    {
        private readonly IServiceProvider services;
        private readonly ILogger<RemoteObjectTerminalChoiceProvider> logger;

        /// <summary>Initializes a new instance of the <see cref="RemoteObjectTerminalChoiceProvider"/> class.</summary>
        public RemoteObjectTerminalChoiceProvider(IServiceProvider services, ILogger<RemoteObjectTerminalChoiceProvider> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        /// <inheritdoc />
        public bool Handles(string source)
            => string.Equals(source, TerminalChoiceSources.RemoteObjects, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        /// <param name="source">immer <see cref="TerminalChoiceSources.RemoteObjects"/></param>
        /// <param name="dependsOnValue">die gewählte Kasse — ihr <c>ClientKey</c></param>
        /// <param name="cancellationToken">bricht die Abfrage ab</param>
        public async Task<IReadOnlyList<SettingChoice>> GetChoicesAsync(string source, string? dependsOnValue,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dependsOnValue))
            {
                // Noch keine Kasse gewaehlt - nichts zu fragen.
                return [];
            }

            var catalog = services.GetService<IRemoteObjectCatalog>();
            if (catalog == null)
            {
                logger.LogInformation("No IRemoteObjectCatalog is registered; the terminal objects of {ClientKey} are entered as text.", dependsOnValue);
                return [];
            }

            try
            {
                var objects = await catalog.GetObjectsAsync(dependsOnValue, typeof(ITerminalDevice), cancellationToken);
                return [.. objects.Select(o => new SettingChoice { Value = o.Name, Label = o.DisplayName ?? o.Name })];
            }
            catch (Exception ex)
            {
                // Agent aus, Kasse unbekannt: die Maske bleibt bedienbar (Textfeld), der Grund steht im Protokoll.
                logger.LogWarning(ex, "The terminal objects of agent {ClientKey} could not be listed.", dependsOnValue);
                return [];
            }
        }
    }
}
