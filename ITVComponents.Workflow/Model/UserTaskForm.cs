using System.Collections.Generic;

namespace ITVComponents.Workflow.Model
{
    /// <summary>
    /// Die Art eines Feldes der generischen Aufgaben-Maske. Bewusst eine kleine, geschlossene Liste:
    /// alles, was darueber hinausgeht, ist eine eigene Komponente
    /// (<see cref="UserActivityNode.ViewKey"/>) - nicht ein weiterer Feldtyp.
    /// </summary>
    public enum UserTaskFieldKind
    {
        /// <summary>Einzeiliger Text.</summary>
        Text,

        /// <summary>Mehrzeiliger Text (Begruendung, Kommentar).</summary>
        MultilineText,

        /// <summary>Zahl.</summary>
        Number,

        /// <summary>Ja/Nein.</summary>
        Boolean,

        /// <summary>Datum (ohne Zeitanteil).</summary>
        Date,

        /// <summary>Auswahl aus <see cref="UserTaskField.Choices"/>.</summary>
        Choice,

        /// <summary>
        /// Datum <b>und</b> Uhrzeit - fuer die Faelle, in denen die Minute zaehlt und ein blosses Datum
        /// eine Auskunft schuldig bliebe (Termin, Stichzeit, Schnittzeitpunkt).
        /// </summary>
        /// <remarks>
        /// Steht ans Ende angehaengt und nicht neben <see cref="Date"/>: Definitionen werden mit den
        /// ZAHLEN dieser Aufzaehlung gespeichert (<c>WorkflowJson</c> fuehrt keinen String-Konverter).
        /// Ein Eintrag in der Mitte haette aus jedem gespeicherten <see cref="Choice"/> still ein
        /// <see cref="Date"/> gemacht.
        /// </remarks>
        DateTime
    }

    /// <summary>
    /// Ein Feld der generischen Aufgaben-Maske. Der einfache Fall ("fuenf Felder ausfuellen") braucht
    /// damit keinen Code - dieselbe Maschinerie fuer Abschluss und Rechte wie bei einer eigenen
    /// Komponente.
    /// </summary>
    public class UserTaskField
    {
        /// <summary>
        /// Der Name des Feldes. Er ist der Schluessel im Ergebnis der Maske und damit der
        /// <see cref="ActivityOutputBinding.Parameter"/>, ueber den der Wert in eine Variable geht.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Die Beschriftung. Klartext ODER JSON-Objekt nach Kultur
        /// (<c>{"de":"Betrag","fr":"Montant"}</c>) - dieselbe Konvention wie bei
        /// <see cref="UserActivityNode.Title"/>. Leer = <see cref="Name"/> wird angezeigt.
        /// </summary>
        public string Label { get; set; }

        /// <summary>Die Art des Feldes (Standard: einzeiliger Text).</summary>
        public UserTaskFieldKind Kind { get; set; } = UserTaskFieldKind.Text;

        /// <summary>Muss das Feld ausgefuellt sein, damit die Aufgabe abgeschlossen werden kann?</summary>
        public bool Required { get; set; }

        /// <summary>
        /// Nur anzeigen, nicht erfassen: das Feld zeigt den Wert aus dem Payload
        /// (<see cref="UserActivityNode.Inputs"/>) und geht nicht ins Ergebnis ein.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Optionaler Name im Payload, aus dem das Feld vorbelegt wird (bzw. bei
        /// <see cref="ReadOnly"/> den anzuzeigenden Wert bezieht). Leer = <see cref="Name"/>.
        /// </summary>
        public string PayloadName { get; set; }

        /// <summary>Optionaler Hinweistext unter dem Feld. Klartext oder Kultur-JSON.</summary>
        public string HelpText { get; set; }

        /// <summary>Die Auswahlmoeglichkeiten - nur bei <see cref="UserTaskFieldKind.Choice"/>.</summary>
        public List<UserTaskChoice> Choices { get; set; } = new List<UserTaskChoice>();
    }

    /// <summary>Eine Auswahlmoeglichkeit eines <see cref="UserTaskFieldKind.Choice"/>-Feldes.</summary>
    public class UserTaskChoice
    {
        /// <summary>Der Wert, der ins Ergebnis geht.</summary>
        public string Value { get; set; }

        /// <summary>Die Beschriftung. Klartext oder Kultur-JSON; leer = <see cref="Value"/>.</summary>
        public string Label { get; set; }
    }
}
