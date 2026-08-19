using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks
{
    /// <summary>
    /// Ein Vorschlag fuer die Zuweisung einer Aufgabe.
    /// </summary>
    public sealed class WorkflowAssignee
    {
        /// <summary>
        /// Der <b>Benutzername</b> - genau der Wert, der als Zustaendiger an der Aufgabe landet und gegen
        /// den die Arbeitsliste filtert. Er muss dem entsprechen, was der Anmeldename des Benutzers ist;
        /// eine Anzeige-Kennung taugt nicht.
        /// </summary>
        public string UserName { get; init; } = string.Empty;

        /// <summary>Was der Auswaehlende liest ("Anna Muster, Buchhaltung"); leer = der Benutzername.</summary>
        public string? DisplayName { get; init; }
    }

    /// <summary>
    /// Woher die Vorschlagsliste beim Umtragen einer Aufgabe kommt. <b>Optional</b>: ist keine
    /// Umsetzung registriert, wird der Zustaendige als Freitext eingegeben.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es gibt diese Abstraktion, weil das Workflow-Modul das Identitaetsmodell des Hosts nicht kennt und
    /// nicht kennen soll - es referenziert keinerlei Security-Projekt. Wer seine Benutzer anbieten will,
    /// registriert eine Umsetzung; wer eine Fremdverwaltung (LDAP, ein Personalsystem) befragt, ebenso.
    /// </para>
    /// <para>
    /// Die Suche ist bewusst der einzige Weg und liefert nie "alle": eine Anlage mit zehntausend Benutzern
    /// soll keine Liste aufbauen, die niemand durchsieht.
    /// </para>
    /// </remarks>
    public interface IWorkflowAssigneeSource
    {
        /// <summary>
        /// Sucht moegliche Zustaendige. <paramref name="requiredPermission"/> ist die Permission der
        /// Aufgabe - wer sie liefern kann, sollte auf Benutzer einschraenken, die sie tatsaechlich haben;
        /// sonst bietet die Auswahl Leute an, bei denen die Aufgabe anschliessend unsichtbar liegt.
        /// </summary>
        /// <param name="term">der Suchbegriff (Teil von Name oder Anzeigename); leer = die ersten N</param>
        /// <param name="requiredPermission">die Permission der Aufgabe, oder null</param>
        /// <param name="max">wie viele Vorschlaege hoechstens</param>
        /// <param name="cancellation">Abbruch (die Suche laeuft an der Tastatur des Benutzers)</param>
        /// <returns>die Vorschlaege - nie null</returns>
        Task<IReadOnlyList<WorkflowAssignee>> SearchAsync(string? term, string? requiredPermission,
            int max, CancellationToken cancellation = default);
    }
}
