using System;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow.EntityFramework;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers.Impl
{
    /// <summary>
    /// Split-faehiger Monitoring-Handler fuer getrennte Deployments und Multi-Tenant: ein Signal wird
    /// <b>store-only</b> zugestellt - die wartenden Tokens werden nebenlaeufigkeits-sicher ueber den
    /// Wartepunkt geschoben (<see cref="ITVComponents.Workflow.WorkflowEngine.ReactivateSignal"/>), OHNE
    /// im Web-Prozess zu advancen. Den nun aktiven Zweig nimmt ein (tenant-uebergreifender) Runner beim
    /// naechsten Poll auf und fuehrt die Aktivitaet dort aus, wo die Plugins liegen.
    /// </summary>
    /// <remarks>
    /// Voraussetzung: Es gibt (mindestens) einen laufenden <c>WorkflowRunner</c> auf derselben Datenbank,
    /// der die reaktivierten Zweige voran treibt (im Multi-Tenant-Fall tenant-uebergreifend: laedt alle
    /// offenen Instanzen und treibt jede unter ihrem Tenant). Ohne Runner bliebe die Instanz aktiv liegen.
    /// Registriert wird dieser Handler ueber <c>WorkflowViewsOptions.SignalDelivery = Runner</c>. Die
    /// hierfuer gebaute Engine reaktiviert nur (fuehrt NICHT aus) - ihr <c>IActivityHost</c> wird nie
    /// aufgerufen und darf trivial sein.
    /// </remarks>
    internal sealed class SplitWorkflowMonitorHandler : WorkflowMonitorHandlerBase
    {
        public SplitWorkflowMonitorHandler(IServiceProvider services,
            IFreshInjectablePlugin<WorkflowContext> freshContext)
            : base(services, freshContext)
        {
        }

        /// <inheritdoc/>
        protected override Task<bool> DeliverSignalAsync(WorkflowOperation op, string instanceId, string signalName)
        {
            // Store-only: nur wecken (Token ueber den Wartepunkt), NICHT ausfuehren. Der Runner advanced.
            // Liefert die Ids der nun aktiven Tokens - mindestens eines = Signal wurde zugestellt.
            return Task.FromResult(op.Engine.ReactivateSignal(instanceId, signalName).Count > 0);
        }
    }
}
