using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks
{
    /// <summary>
    /// Der Vertrag jeder Aufgaben-Maske - der generischen wie einer eigenen: sie zeigt an und liefert
    /// auf Zuruf ihre <b>Ausgabewerte</b>. Mehr nicht.
    /// </summary>
    /// <remarks>
    /// Der Rahmen gehoert dem Mantel: Titelzeile, Fusszeile mit „Erledigen"/„Schliessen", weiche Sperre,
    /// Doppel-Klick-Schutz, Versionskonflikt und „war schon erledigt". Fuer JEDE Maske gleich. Eine Maske,
    /// die ihre eigenen Knoepfe mitbraechte, laege damit im scrollenden Inhalt statt in der angehefteten
    /// Fusszeile - auf einem schmalen Geraet waere sie damit unter Umstaenden gar nicht sichtbar, und der
    /// Benutzer koennte die Aufgabe nicht abschliessen.
    /// <para>
    /// Was die Maske liefert, sind die Ausgabewerte der Aufgabe. Wohin die wandern, entscheidet das
    /// <c>Outputs</c>-Mapping des Knotens - die Maske muss die Variablen des Prozesses nicht kennen.
    /// </para>
    /// </remarks>
    public interface IUserTaskView
    {
        /// <summary>
        /// Der Benutzer hat „Erledigen" gedrueckt: die Maske prueft ihre Eingaben und liefert die
        /// Ausgabewerte - oder sagt, dass es noch nicht so weit ist.
        /// </summary>
        /// <returns>
        /// <see cref="UserTaskViewResult.Complete"/> mit den Ausgabewerten, oder
        /// <see cref="UserTaskViewResult.Incomplete"/>, wenn noch etwas fehlt.
        /// </returns>
        Task<UserTaskViewResult> ResolveActivityAsync();
    }

    /// <summary>
    /// Die Antwort einer Maske auf „Erledigen": entweder die Ausgabewerte, oder die Auskunft, dass noch
    /// etwas fehlt.
    /// </summary>
    /// <remarks>
    /// Bewusst kein blosses <c>Dictionary?</c> mit „null heisst nicht bereit": null ist ein voellig
    /// gueltiges Ergebnis (eine Aufgabe, die nur bestaetigt wird, hat keine Ausgabewerte). Die beiden
    /// Faelle muessen unterscheidbar bleiben, sonst schluepft ein unvollstaendiges Formular als
    /// „bestaetigt" durch.
    /// </remarks>
    public sealed class UserTaskViewResult
    {
        private UserTaskViewResult(bool canComplete, IDictionary<string, object>? outputs, string? message)
        {
            CanComplete = canComplete;
            Outputs = outputs;
            Message = message;
        }

        /// <summary>Ob die Aufgabe jetzt abgeschlossen werden darf.</summary>
        public bool CanComplete { get; }

        /// <summary>
        /// Die Ausgabewerte der Maske (Schluessel = die am Knoten deklarierten Ausgabeparameter), oder
        /// null fuer eine Aufgabe, die nur bestaetigt wird.
        /// </summary>
        public IDictionary<string, object>? Outputs { get; }

        /// <summary>
        /// Optionale Meldung fuer den Benutzer, wenn noch etwas fehlt. Null, wenn die Maske die
        /// fehlenden Stellen bereits selbst markiert hat - dann waere eine zusaetzliche Einblendung nur
        /// Laerm.
        /// </summary>
        public string? Message { get; }

        /// <summary>Alles beisammen - die Aufgabe kann mit diesen Ausgabewerten abgeschlossen werden.</summary>
        /// <param name="outputs">die Ausgabewerte, oder null (reine Bestaetigung)</param>
        public static UserTaskViewResult Complete(IDictionary<string, object>? outputs = null)
            => new UserTaskViewResult(true, outputs, null);

        /// <summary>Noch nicht so weit - die Aufgabe bleibt offen.</summary>
        /// <param name="message">
        /// was fehlt, falls die Maske es nicht ohnehin an den Feldern zeigt; sonst null
        /// </param>
        public static UserTaskViewResult Incomplete(string? message = null)
            => new UserTaskViewResult(false, null, message);
    }
}
