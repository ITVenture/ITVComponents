using System;
using System.Linq;
using ITVComponents.Logging;
using ITVComponents.Workflow.WebWorker.Configuration;
using ITVComponents.WebCoreToolkit.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime
{
    /// <summary>
    /// Loest den (optionalen) Umgebungs-Namen einer View-Operation gegen die
    /// <see cref="WorkflowEnvironmentSettings"/> auf. Bewusst tolerant: ohne Umgebungs-Namen, ohne
    /// konfigurierte Umgebungen oder bei unbekanntem Namen faellt alles auf das <b>bisherige Verhalten</b>
    /// zurueck (die einzelne, per DI registrierte Umgebung) - so bleibt der Ein-Umgebungs-Betrieb ohne
    /// jede Einstellung unveraendert.
    /// </summary>
    internal static class WorkflowEnvironmentResolver
    {
        /// <summary>
        /// Die konfigurierte Umgebung mit dem angegebenen Namen, oder null (kein Name, keine Settings oder
        /// nicht gefunden). Ein angeforderter, aber nicht konfigurierter Name wird protokolliert.
        /// </summary>
        public static WorkflowEnvironment? Resolve(IServiceProvider services, string? environmentName)
        {
            if (services == null || string.IsNullOrWhiteSpace(environmentName))
            {
                return null;
            }

            // Optional aufloesen: ist die Settings-Infrastruktur nicht verdrahtet, gibt es keine Umgebungen.
            WorkflowEnvironmentSettings? settings =
                services.GetService<IHierarchySettings<WorkflowEnvironmentSettings>>()?.ValueOrDefault;

            WorkflowEnvironment? env = settings?.Environments?
                .FirstOrDefault(e => string.Equals(e.Name, environmentName, StringComparison.OrdinalIgnoreCase));

            if (env == null)
            {
                LogEnvironment.LogEvent(
                    $"Workflow-Umgebung '{environmentName}' ist nicht konfiguriert - es wird die Standard-Umgebung " +
                    "(per DI registrierter Store) verwendet.", LogSeverity.Warning);
            }

            return env;
        }

        /// <summary>
        /// Der Name der scope-owned <c>WorkflowContext</c>-Dependency (Store-Plugin) fuer die gewaehlte
        /// Umgebung, oder null fuer den Standard-Store. Wird direkt an <see cref="WorkflowOperation"/>
        /// gereicht.
        /// </summary>
        public static string? StoreDependencyName(IServiceProvider services, string? environmentName)
        {
            string? name = Resolve(services, environmentName)?.WorkflowStorePluginName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        /// <summary>
        /// Der Name des <c>ActivityCatalog</c>-Plugins der Instanz (Worker), deren <c>Name</c> dem
        /// <paramref name="executionTarget"/> der Aktivitaet entspricht - oder null (kein Ziel, keine Umgebung,
        /// keine passende Instanz oder kein Katalog-Name gesetzt). Null bedeutet: den per DI registrierten
        /// Default-Katalog verwenden (Fallback).
        /// </summary>
        public static string? ActivityCatalogPluginName(IServiceProvider services, string? environmentName,
            string? executionTarget)
        {
            if (string.IsNullOrWhiteSpace(executionTarget))
            {
                return null;
            }

            WorkflowEnvironmentInstance? instance = Resolve(services, environmentName)?.Instances?
                .FirstOrDefault(i => string.Equals(i.Name, executionTarget, StringComparison.OrdinalIgnoreCase));

            string? name = instance?.ActivityCatalogPluginName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }
}
