using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks.ViewModels;
using ITVComponents.Workflow;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks.Handlers
{
    /// <summary>
    /// Backend der Arbeitsliste ("Meine Aufgaben"): findet die Aufgaben, die der aktuelle Benutzer sehen
    /// darf, und schliesst sie ab.
    /// </summary>
    /// <remarks>
    /// <b>Jede</b> Methode prueft die Berechtigung serverseitig - nicht nur der Razor-Wrapper. Die
    /// Aufgaben-Ansicht ist eine Benutzer-Oberflaeche mit fachlichen Daten in der Maske; eine erratene
    /// Token-Id darf nicht genuegen.
    /// </remarks>
    public interface IWorkflowTaskHandler
    {
        /// <summary>Prueft, ob der aktuelle Benutzer eine der Berechtigungen hat.</summary>
        bool HasPermission(ClaimsPrincipal user, params string[] permissions);

        /// <summary>
        /// Liefert eine Seite der Arbeitsliste. Enthaelt ausschliesslich Aufgaben, deren
        /// <c>RequiredPermission</c> der Benutzer hat (oder die keine verlangen). <paramref name="environment"/>
        /// waehlt - falls konfiguriert - die Workflow-Umgebung (Store); null = die Standard-Umgebung.
        /// </summary>
        Task<PagedResult<UserTaskListItem>> ListTasksAsync(ClaimsPrincipal user, UserTaskListQuery query,
            string? environment = null);

        /// <summary>
        /// Die Aufgabenarten, die in der Arbeitsliste dieses Benutzers vorkommen - fuer den Filter.
        /// </summary>
        Task<IReadOnlyList<string>> ListTaskKeysAsync(ClaimsPrincipal user, string? environment = null);

        /// <summary>
        /// Laedt EINE Aufgabe samt aufgeloestem Payload und Masken-Deklaration. Liefert null, wenn es sie
        /// nicht (mehr) gibt oder der Benutzer sie nicht sehen darf.
        /// </summary>
        Task<UserTaskDescriptor?> GetTaskAsync(ClaimsPrincipal user, string instanceId, string tokenId,
            string? environment = null);

        /// <summary>
        /// Setzt die <b>weiche Sperre</b>: markiert die Aufgabe als "wird gerade bearbeitet". Sie blockiert
        /// niemanden - die harte Entscheidung faellt am Abschluss - sondern warnt den zweiten Bearbeiter,
        /// bevor er die Arbeit doppelt macht. Liefert den bisherigen Inhaber, wenn ein FREMDER Claim
        /// besteht, sonst null.
        /// </summary>
        Task<string?> ClaimAsync(ClaimsPrincipal user, string instanceId, string tokenId, TimeSpan duration,
            string? environment = null);

        /// <summary>Gibt die eigene weiche Sperre wieder frei (Dialog geschlossen).</summary>
        Task ReleaseClaimAsync(ClaimsPrincipal user, string instanceId, string tokenId, string? environment = null);

        /// <summary>
        /// Schliesst die Aufgabe mit dem Ergebnis der Maske ab. Der Ausgang unterscheidet "erledigt" von
        /// "war schon erledigt" - der zweite Klick darf nicht wie ein Erfolg aussehen.
        /// </summary>
        Task<UserTaskCompletionResult> CompleteAsync(ClaimsPrincipal user, string instanceId, string tokenId,
            IDictionary<string, object>? result, string? environment = null);
    }
}
