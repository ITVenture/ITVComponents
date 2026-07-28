using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow.EntityFramework;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks.Handlers.Impl
{
    /// <summary>
    /// Split-faehiger Aufgaben-Handler fuer getrennte Deployments und Multi-Tenant: der Abschluss macht
    /// den Zweig nur wieder aktiv; aufgenommen und ausgefuehrt wird er vom (tenant-uebergreifenden)
    /// Runner beim naechsten Poll.
    /// </summary>
    /// <remarks>
    /// Voraussetzung ist - wie bei der Signal-Zustellung - ein laufender <c>WorkflowRunner</c> auf
    /// derselben Datenbank. Ohne ihn bliebe die Instanz nach dem Abschluss aktiv liegen.
    /// </remarks>
    internal sealed class SplitWorkflowTaskHandler : WorkflowTaskHandlerBase
    {
        public SplitWorkflowTaskHandler(IServiceProvider services,
            IFreshInjectablePlugin<WorkflowContext> freshContext)
            : base(services, freshContext)
        {
        }

        /// <inheritdoc/>
        protected override Task AdvanceAsync(WorkflowOperation op, string instanceId,
            IReadOnlyList<string> tokenIds)
        {
            // Nichts zu tun: CompleteUserTask hat den Zweig bereits store-only aktiviert. Hier NICHT
            // auszufuehren ist der ganze Zweck dieser Variante - der Web-Prozess hat die Aktivitaets-
            // Plugins womoeglich gar nicht.
            return Task.CompletedTask;
        }
    }
}
