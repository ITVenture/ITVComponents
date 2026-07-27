using System;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow.EntityFramework;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers.Impl
{
    /// <summary>
    /// Standard-Monitoring-Handler (Web-Only, globaler Ein-Kontext-Betrieb): ein Signal wird
    /// <b>inline</b> zugestellt - die Engine advanced die Instanz synchron IM WEB-PROZESS bis zum
    /// naechsten Wartepunkt und fuehrt dabei die folgenden Aktivitaeten hier aus. Ideal, wenn Editor und
    /// Engine im selben Prozess gegen EINEN (globalen) Store laufen.
    /// </summary>
    /// <remarks>
    /// Fuer getrennte Deployments oder Multi-Tenant stattdessen den <see cref="SplitWorkflowMonitorHandler"/>
    /// verwenden (Option <c>WorkflowViewsOptions.SignalDelivery = Runner</c>) - der reaktiviert store-only
    /// und laesst einen (tenant-uebergreifenden) Runner voran treiben, statt im Web auszufuehren. Inline-
    /// Ausfuehrung setzt den globalen Betrieb voraus, weil der Web-Prozess sonst nur den EINEN Tenant der
    /// Anfrage voran treiben koennte - der tenant-uebergreifende Fortschritt gehoert in den Runner.
    /// </remarks>
    internal sealed class WorkflowMonitorHandler : WorkflowMonitorHandlerBase
    {
        public WorkflowMonitorHandler(IServiceProvider services,
            IFreshInjectablePlugin<WorkflowContext> freshContext)
            : base(services, freshContext)
        {
        }

        /// <inheritdoc/>
        protected override Task<bool> DeliverSignalAsync(WorkflowOperation op, string instanceId, string signalName)
        {
            // Inline: advanced synchron im aufrufenden (Web-)Prozess ueber die frisch gebaute Engine.
            return Task.FromResult(op.Engine.SignalWorkflow(instanceId, signalName));
        }
    }
}
