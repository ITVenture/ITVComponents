using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.ViewModels;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers
{
    /// <summary>
    /// Backend fuer die Monitoring-Views: liest Workflow-Instanzen und fuehrt operative Aktionen aus.
    /// </summary>
    public interface IWorkflowMonitorHandler
    {
        /// <summary>Prueft, ob der aktuelle Benutzer eine der Berechtigungen hat.</summary>
        bool HasPermission(ClaimsPrincipal user, params string[] permissions);

        /// <summary>
        /// Liefert eine Seite der Instanz-Uebersicht. <paramref name="environment"/> waehlt - falls
        /// konfiguriert - die Workflow-Umgebung (Store); null = die Standard-Umgebung.
        /// </summary>
        Task<PagedResult<WorkflowInstanceListItem>> ListInstancesAsync(ClaimsPrincipal user, ListQuery query,
            string? environment = null);

        /// <summary>Laedt eine Instanz vollstaendig (Variablen, Tokens, Protokoll), oder null.</summary>
        Task<WorkflowInstance?> GetInstanceAsync(ClaimsPrincipal user, string instanceId, string? environment = null);

        /// <summary>
        /// Laedt die Definition zu Id+Version - fuer die Graph-Ansicht des Instanz-Details, in der die
        /// aktuellen Token-Positionen ueberlagert werden. Liefert null, wenn nichts gefunden wird.
        /// </summary>
        Task<WorkflowDefinition?> GetDefinitionAsync(ClaimsPrincipal user, string definitionId, int version,
            string? environment = null);

        /// <summary>
        /// Liefert ein Signal an eine wartende Instanz (Operate). Liefert false ohne Berechtigung
        /// oder wenn kein Token darauf wartet.
        /// </summary>
        Task<bool> SignalAsync(ClaimsPrincipal user, string instanceId, string signalName, string? environment = null);

        /// <summary>Bricht eine Instanz ab (Operate). Liefert false ohne Berechtigung.</summary>
        Task<bool> CancelAsync(ClaimsPrincipal user, string instanceId, string? environment = null);
    }
}
