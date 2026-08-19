using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ITVComponents.Workflow;

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

        /// <summary>
        /// <b>Nachbereitung:</b> die Aufgabe ist abgeschlossen - jetzt darf die Maske ihre eigenen
        /// Wirkungen festschreiben. Optional; ohne eigene Umsetzung passiert nichts.
        /// </summary>
        /// <param name="result">
        /// wie der Abschluss ausgegangen ist. <b>Nicht ignorieren:</b> bei
        /// <see cref="UserTaskCompletionStatus.AlreadyCompleted"/> hat ein anderer abgeschlossen, und die
        /// Maske darf dann NICHT auch noch schreiben - sonst entstehen genau die Doubletten, gegen die
        /// dieser Haken gedacht ist.
        /// </param>
        /// <returns>
        /// <see cref="UserTaskPostResult.Ok"/>, oder <see cref="UserTaskPostResult.Failed"/> mit einer
        /// Meldung fuer den Benutzer.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <b>Wofuer:</b> <see cref="ResolveActivityAsync"/> laeuft VOR dem Abschluss. Wer dort Fachdaten
        /// schreibt, schreibt sie auch dann, wenn der Abschluss anschliessend scheitert (Versionskonflikt,
        /// "war schon erledigt") oder der Benutzer den Dialog einfach schliesst - und muss die Doublette
        /// danach selbst wieder einfangen. Hier passiert es erst, wenn feststeht, dass es etwas zu
        /// begleiten gibt.
        /// </para>
        /// <para>
        /// <b>Wofuer NICHT:</b> als Ersatz fuer eine eigene Aktivitaet im Prozess. Wenn ein spaeterer
        /// Schritt die Daten liest oder eine Verzweigung von ihnen abhaengt, gehoert das Schreiben in den
        /// Prozess - nur dort greifen dessen Wiederholung und Fehlerbehandlung. Dieser Haken ist fuer
        /// Wirkungen, die am Abschluss haengen, nicht fuer Schritte, ueber die der Prozess nachdenkt.
        /// </para>
        /// <para>
        /// <b>Ein Scheitern hier haelt nichts auf</b> - die Aufgabe bleibt erledigt und der Prozess laeuft
        /// weiter; das ist nicht mehr rueckgaengig zu machen. Es wird dem Benutzer aber ausdruecklich
        /// angezeigt ("abgeschlossen, aber X konnte nicht gespeichert werden"): sonst taeuscht man eine
        /// sichtbare Doublette gegen eine unsichtbare Luecke ein, und das waere der schlechtere Fehler.
        /// </para>
        /// <para>
        /// <b>Default-Methode und kein Pflichtpunkt:</b> der Vertrag wird beim Uebersetzen erzwungen - ein
        /// weiterer Pflicht-Member braeche jede bestehende Maske, obwohl die weit ueberwiegende Mehrheit
        /// ihn nie braucht.
        /// </para>
        /// </remarks>
        Task<UserTaskPostResult> PostResolveActivityAsync(UserTaskCompletionResult result)
            => Task.FromResult(UserTaskPostResult.Ok());
    }

    /// <summary>
    /// Die Antwort einer Maske auf die Nachbereitung: durchgelaufen, oder gescheitert mit einer Meldung,
    /// die der Benutzer sehen muss.
    /// </summary>
    /// <remarks>
    /// Ein Ergebnistyp und keine Exception - aus demselben Grund wie bei
    /// <see cref="UserTaskViewResult"/>: ein fachliches "hat nicht geklappt" ist kein Ausnahmefall,
    /// sondern ein erwarteter Ausgang. Der Dialog faengt trotzdem zusaetzlich ab, was geworfen wird; eine
    /// Maske kann immer werfen.
    /// </remarks>
    public sealed class UserTaskPostResult
    {
        private UserTaskPostResult(bool success, string? message)
        {
            Success = success;
            Message = message;
        }

        /// <summary>Ob die Nachbereitung durchgelaufen ist.</summary>
        public bool Success { get; }

        /// <summary>
        /// Bei einem Fehlschlag: was dem Benutzer zu sagen ist. Leer laesst den Dialog eine allgemeine
        /// Meldung zeigen - aber schweigen tut er nicht.
        /// </summary>
        public string? Message { get; }

        /// <summary>Alles erledigt.</summary>
        public static UserTaskPostResult Ok() => new UserTaskPostResult(true, null);

        /// <summary>Die Nachbereitung ist gescheitert - die Aufgabe bleibt trotzdem abgeschlossen.</summary>
        /// <param name="message">was dem Benutzer zu sagen ist</param>
        public static UserTaskPostResult Failed(string? message = null)
            => new UserTaskPostResult(false, message);
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
