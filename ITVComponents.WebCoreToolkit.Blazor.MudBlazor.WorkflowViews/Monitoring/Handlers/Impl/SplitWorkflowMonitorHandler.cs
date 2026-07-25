using System;
using System.Threading.Tasks;
using ITVComponents.Workflow;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Stores;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers.Impl
{
    /// <summary>
    /// Split-faehiger Monitoring-Handler fuer getrennte Deployments (Engine/Runner in einem
    /// Backend-Dienst): ein Signal wird <b>store-only</b> zugestellt - die wartenden Tokens werden
    /// nebenlaeufigkeits-sicher ueber den Wartepunkt geschoben (<see cref="WorkflowEngine.ReactivateSignal"/>),
    /// OHNE im Web-Prozess zu advancen. Den nun aktiven Zweig nimmt der (Backend-)Runner beim naechsten
    /// Poll auf und fuehrt die Aktivitaet dort aus, wo die Plugins liegen.
    /// </summary>
    /// <remarks>
    /// Voraussetzung: Es gibt (mindestens) einen laufenden <c>WorkflowRunner</c> auf derselben Datenbank,
    /// der die reaktivierten Zweige voran treibt. Ohne Runner bliebe die Instanz aktiv liegen. Registriert
    /// wird dieser Handler ueber <c>WorkflowViewsOptions.SignalDelivery = Runner</c>.
    /// </remarks>
    internal sealed class SplitWorkflowMonitorHandler : WorkflowMonitorHandlerBase
    {
        public SplitWorkflowMonitorHandler(IServiceProvider services, IDbContextFactory<WorkflowContext> dbFactory,
            IWorkflowStore store, WorkflowEngine engine)
            : base(services, dbFactory, store, engine)
        {
        }

        /// <inheritdoc/>
        protected override Task<bool> DeliverSignalAsync(string instanceId, string signalName)
        {
            // Store-only: nur wecken (Token ueber den Wartepunkt), NICHT ausfuehren. Der Runner advanced.
            // Liefert die Ids der nun aktiven Tokens - mindestens eines = Signal wurde zugestellt.
            return Task.FromResult(Engine.ReactivateSignal(instanceId, signalName).Count > 0);
        }
    }
}
