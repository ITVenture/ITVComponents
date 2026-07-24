using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.ViewModels;
using ITVComponents.Workflow.Instances;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers
{
    /// <summary>
    /// Backend fuer die Monitoring-Views: liest Workflow-Instanzen und fuehrt operative Aktionen aus.
    /// </summary>
    public interface IWorkflowMonitorHandler
    {
        /// <summary>Prueft, ob der aktuelle Benutzer eine der Berechtigungen hat.</summary>
        bool HasPermission(ClaimsPrincipal user, params string[] permissions);

        /// <summary>Liefert eine Seite der Instanz-Uebersicht.</summary>
        Task<PagedResult<WorkflowInstanceListItem>> ListInstancesAsync(ClaimsPrincipal user, ListQuery query);

        /// <summary>Laedt eine Instanz vollstaendig (Variablen, Tokens, Protokoll), oder null.</summary>
        Task<WorkflowInstance?> GetInstanceAsync(ClaimsPrincipal user, string instanceId);

        /// <summary>
        /// Liefert ein Signal an eine wartende Instanz (Operate). Liefert false ohne Berechtigung
        /// oder wenn kein Token darauf wartet.
        /// </summary>
        Task<bool> SignalAsync(ClaimsPrincipal user, string instanceId, string signalName);

        /// <summary>Bricht eine Instanz ab (Operate). Liefert false ohne Berechtigung.</summary>
        Task<bool> CancelAsync(ClaimsPrincipal user, string instanceId);
    }
}
