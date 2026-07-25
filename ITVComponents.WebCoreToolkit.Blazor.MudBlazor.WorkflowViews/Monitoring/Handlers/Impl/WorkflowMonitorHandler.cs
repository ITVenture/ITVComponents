using System;
using System.Threading.Tasks;
using ITVComponents.Workflow;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Stores;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers.Impl
{
    /// <summary>
    /// Standard-Monitoring-Handler (Web-Only): ein Signal wird <b>inline</b> zugestellt - die Engine
    /// advanced die Instanz synchron IM WEB-PROZESS bis zum naechsten Wartepunkt und fuehrt dabei die
    /// folgenden Aktivitaeten hier aus. Ideal, wenn Editor und Engine im selben Prozess laufen.
    /// </summary>
    /// <remarks>
    /// Fuer getrennte Deployments (Engine/Runner in einem Backend-Dienst) stattdessen den
    /// <see cref="SplitWorkflowMonitorHandler"/> verwenden (Option
    /// <c>WorkflowViewsOptions.SignalDelivery = Runner</c>) - der reaktiviert store-only und laesst den
    /// Runner voran treiben, statt im Web auszufuehren.
    /// </remarks>
    internal sealed class WorkflowMonitorHandler : WorkflowMonitorHandlerBase
    {
        public WorkflowMonitorHandler(IServiceProvider services, IDbContextFactory<WorkflowContext> dbFactory,
            IWorkflowStore store, WorkflowEngine engine)
            : base(services, dbFactory, store, engine)
        {
        }

        /// <inheritdoc/>
        protected override Task<bool> DeliverSignalAsync(string instanceId, string signalName)
        {
            // Inline: advanced synchron im aufrufenden (Web-)Prozess.
            return Task.FromResult(Engine.SignalWorkflow(instanceId, signalName));
        }
    }
}
