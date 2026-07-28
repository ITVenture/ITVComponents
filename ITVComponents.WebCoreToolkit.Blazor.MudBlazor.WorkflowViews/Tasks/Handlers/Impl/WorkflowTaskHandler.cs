using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow.EntityFramework;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks.Handlers.Impl
{
    /// <summary>
    /// Standard-Aufgaben-Handler (Web-Only, globaler Ein-Kontext-Betrieb): nach dem Abschluss laeuft der
    /// Zweig <b>inline</b> im Web-Prozess weiter, bis zur naechsten Barriere.
    /// </summary>
    /// <remarks>
    /// Der Abschluss selbst ist in beiden Varianten identisch (nebenlaeufigkeits-sicher ueber
    /// <c>CompleteUserTask</c>); nur das Vorantreiben unterscheidet sich - genau wie bei der
    /// Signal-Zustellung im Monitoring.
    /// </remarks>
    internal sealed class WorkflowTaskHandler : WorkflowTaskHandlerBase
    {
        public WorkflowTaskHandler(IServiceProvider services,
            IFreshInjectablePlugin<WorkflowContext> freshContext)
            : base(services, freshContext)
        {
        }

        /// <inheritdoc/>
        protected override Task AdvanceAsync(WorkflowOperation op, string instanceId,
            IReadOnlyList<string> tokenIds)
        {
            foreach (string tokenId in tokenIds)
            {
                // Zweig-weise vorantreiben (nicht die ganze Instanz): das ist der nebenlaeufigkeits-sichere
                // Weg und deckt zugleich den Fall ab, dass der Abschluss mehrere Zweige geoeffnet hat.
                op.Engine.RunBranch(instanceId, tokenId);
            }

            return Task.CompletedTask;
        }
    }
}
