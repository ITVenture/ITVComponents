using System.Collections.Generic;
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

        /// <summary>
        /// Setzt die Dringlichkeit einer Instanz neu (Operate) - kleinere Zahl = wichtiger. Der Griff fuer
        /// den Fall "dieser eine Hintergrund-Lauf ist jetzt doch eilig" (oder umgekehrt: "der darf warten,
        /// er blockiert gerade alles"). Liefert false ohne Berechtigung, bei unbekannter Instanz oder wenn
        /// ein gleichzeitig laufender Zweig das Rennen um den Commit gewonnen hat (dann wiederholen).
        /// </summary>
        Task<bool> SetPriorityAsync(ClaimsPrincipal user, string instanceId, int priority,
            string? environment = null);

        /// <summary>
        /// Liefert alles, was die Retry-Maske einer fehlgeschlagenen Instanz braucht: Fehlermeldung,
        /// Wiederaufsatzpunkt und die Variablen im Scope des fehlgeschlagenen Schritts. Null ohne
        /// Berechtigung (<see cref="WorkflowSecurity.Operate"/>) oder wenn es die Instanz nicht gibt.
        /// </summary>
        Task<WorkflowRetryInfo?> GetRetryInfoAsync(ClaimsPrincipal user, string instanceId,
            string? environment = null);

        /// <summary>
        /// Nimmt eine fehlgeschlagene Instanz an ihren Fehlerstellen wieder auf (Operate) - optional mit
        /// korrigierten Variablen <b>je Zweig</b> (Schluessel = <c>WorkflowRetryBranch.TokenId</c>, weil
        /// nach einem Split jeder Zweig seinen eigenen Scope hat). Der Rueckgabewert traegt im Fehlerfall
        /// den anzeigbaren Grund.
        /// </summary>
        Task<WorkflowRetryResult> RetryAsync(ClaimsPrincipal user, string instanceId,
            IDictionary<string, IDictionary<string, object?>>? branchUpdates = null,
            string? environment = null);

        /// <summary>
        /// Liefert die Definitionen, die von Hand gestartet werden koennen - je Id die hoechste Version,
        /// ohne die fuer den Start gesperrten und die ohne Start-Knoten. Leer ohne Berechtigung
        /// (<see cref="WorkflowSecurity.Start"/>).
        /// </summary>
        Task<IReadOnlyList<WorkflowStartableDefinition>> ListStartableDefinitionsAsync(ClaimsPrincipal user,
            string? environment = null);

        /// <summary>
        /// Liefert die Start-Maske einer Definition (Felder, Anleitung, Signatur-Hinweise) oder null, wenn
        /// es die Definition nicht gibt bzw. die Berechtigung fehlt.
        /// </summary>
        Task<WorkflowStartForm?> GetStartFormAsync(ClaimsPrincipal user, string definitionId,
            string? environment = null);

        /// <summary>
        /// Startet eine neue Instanz (Start). Der Rueckgabewert traegt im Fehlerfall den anzeigbaren Grund -
        /// eine Definition kann fuer den Start gesperrt sein oder Start-Parameter haben, die sich nicht
        /// aufloesen lassen.
        /// </summary>
        Task<WorkflowStartResult> StartInstanceAsync(ClaimsPrincipal user, WorkflowStartRequest request,
            string? environment = null);
    }
}
