using System;
using System.Collections.Generic;
using ITVComponents.Workflow.Model;

namespace ITVComponents.Workflow
{
    /// <summary>
    /// Wie der Abschluss einer Benutzer-Aufgabe ausgegangen ist.
    /// </summary>
    public enum UserTaskCompletionStatus
    {
        /// <summary>Die Aufgabe gibt es nicht (mehr) - weder wartend noch erledigt.</summary>
        NotFound,

        /// <summary>
        /// Die Aufgabe war bereits erledigt. Der erwartete Ausgang des Rennens zweier Bearbeiter - kein
        /// Fehler, aber auch kein Erfolg: die Oberflaeche muss es sagen, statt "gespeichert" zu melden.
        /// </summary>
        AlreadyCompleted,

        /// <summary>Die Aufgabe wurde abgeschlossen, der Zweig laeuft weiter.</summary>
        Completed,

        /// <summary>Der Abschluss hat die Instanz auf Faulted laufen lassen (z.B. fehlende Ausgangskante).</summary>
        Faulted
    }

    /// <summary>Das Ergebnis von <see cref="WorkflowEngine.CompleteUserTask"/>.</summary>
    public class UserTaskCompletionResult
    {
        /// <summary>Erzeugt ein Ergebnis.</summary>
        public UserTaskCompletionResult(UserTaskCompletionStatus status, IReadOnlyList<string> activatedTokenIds,
            bool endsAssistant = false)
        {
            Status = status;
            ActivatedTokenIds = activatedTokenIds ?? Array.Empty<string>();
            EndsAssistant = endsAssistant;
        }

        /// <summary>Der Ausgang des Abschlusses.</summary>
        public UserTaskCompletionStatus Status { get; }

        /// <summary>
        /// Die Ids der dadurch aktiv gewordenen Tokens - der Runner reiht sie als Zweig-Tasks ein.
        /// </summary>
        public IReadOnlyList<string> ActivatedTokenIds { get; }

        /// <summary>
        /// True, wenn mit dieser Aufgabe der <b>gefuehrte Teil</b> des Vorgangs endet
        /// (<see cref="UserActivityNode.EndsAssistant"/>): eine Oberflaeche, die einen Assistenten fuehrt,
        /// schliesst danach, statt auf den naechsten Schritt zu warten.
        /// </summary>
        /// <remarks>
        /// Nur beim Ausgang <see cref="UserTaskCompletionStatus.Completed"/> aussagekraeftig - sonst hat
        /// dieser Aufruf den Zweig gar nicht bewegt.
        /// </remarks>
        public bool EndsAssistant { get; }

        /// <summary>Kurzform fuer <see cref="UserTaskCompletionStatus.Completed"/>.</summary>
        public bool Success => Status == UserTaskCompletionStatus.Completed;
    }

    /// <summary>
    /// Alles, was die Oberflaeche braucht, um EINE wartende Aufgabe darzustellen: die aufgeloesten
    /// Eingabewerte, die Deklaration der generischen Maske und die Schluessel, ueber die eine eigene
    /// Komponente gefunden wird.
    /// </summary>
    /// <remarks>
    /// Titel und Beschriftungen kommen <b>unaufgeloest</b> heraus (Klartext oder Kultur-JSON) - uebersetzt
    /// wird in der Anzeige, die die Kultur des Lesers kennt.
    /// </remarks>
    public class UserTaskDescriptor
    {
        /// <summary>Die Instanz, zu der die Aufgabe gehoert.</summary>
        public string InstanceId { get; set; }

        /// <summary>Das wartende Token - die Aufgabe selbst.</summary>
        public string TokenId { get; set; }

        /// <summary>Die Definition, aus der die Aufgabe stammt.</summary>
        public string DefinitionId { get; set; }

        /// <summary>Der Knoten der Aufgabe.</summary>
        public string NodeId { get; set; }

        /// <summary>Die Aufgabenart.</summary>
        public string TaskKey { get; set; }

        /// <summary>Der Schluessel der Oberflaechen-Komponente, falls einer deklariert ist.</summary>
        public string ViewKey { get; set; }

        /// <summary>Die Permission, die diese Aufgabe verlangt (null = nur das allgemeine Aufgaben-Recht).</summary>
        public string RequiredPermission { get; set; }

        /// <summary>Der Zustaendige, oder null fuer eine Pool-Aufgabe.</summary>
        public string AssignedTo { get; set; }

        /// <summary>Der Titel - Klartext oder Kultur-JSON.</summary>
        public string Title { get; set; }

        /// <summary>Die Beschreibung/Arbeitsanweisung - Klartext oder Kultur-JSON.</summary>
        public string Description { get; set; }

        /// <summary>
        /// Das aufgeloeste Datenobjekt fuer die Formatierung von <see cref="Title"/> und
        /// <see cref="Description"/> (aus <see cref="UserActivityNode.FormatData"/>, ausgewertet beim OEFFNEN
        /// ueber den aktuellen Variablen-Stack), oder null. Die Anzeige wendet damit die Prototyp-Formatierung
        /// auf die uebersetzten Texte an. Bleibt bewusst ein <c>object</c> - der Descriptor wird in-process an
        /// die Maske gereicht, nicht serialisiert.
        /// </summary>
        public object FormatData { get; set; }

        /// <summary>Wann die Aufgabe entstanden ist (UTC).</summary>
        public DateTime? CreatedUtc { get; set; }

        /// <summary>Die Frist (UTC), falls eine gesetzt ist.</summary>
        public DateTime? DueUtc { get; set; }

        /// <summary>
        /// Die aufgeloesten Eingabewerte (<see cref="UserActivityNode.Inputs"/>) - das, was die Maske zu
        /// sehen bekommt. Aufgeloest beim Oeffnen, nicht beim Parken: die Maske zeigt damit den aktuellen
        /// Stand.
        /// </summary>
        public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();

        /// <summary>Die Deklaration der generischen Maske (leer = reine Bestaetigung).</summary>
        public List<UserTaskField> FormFields { get; set; } = new List<UserTaskField>();

        /// <summary>
        /// Gehoert diese Aufgabe zu einem <b>gefuehrten Abschnitt</b>
        /// (<see cref="UserActivityNode.RunsInAssistant"/>)? Dann fuehrt eine Oberflaeche, die das kann,
        /// nach dem Abschluss gleich zum naechsten Schritt weiter.
        /// </summary>
        /// <remarks>
        /// Steht hier und nicht nur am Knoten, weil die Oberflaeche es beim OEFFNEN wissen muss - und zwar
        /// unabhaengig davon, wo der Benutzer eingestiegen ist. Genau deshalb hat auch die Arbeitsliste
        /// nichts weiter zu tun: sie oeffnet wie immer, und der Vorgang bringt seinen Charakter selbst mit.
        /// </remarks>
        public bool RunsInAssistant { get; set; }
    }
}
