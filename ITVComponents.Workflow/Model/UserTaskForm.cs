using System.Collections.Generic;
using ITVComponents.Workflow.Expressions;

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
        /// <remarks>
        /// Ein <b>Pfad</b> (<c>customer.Ship.Street</c>) greift in den Datensatz hinein - und ist damit
        /// zugleich das <b>Rueckschreibziel</b>, wenn der Wert von einem Wert-Handler kommt. Der Pfad, aus
        /// dem gelesen wird, ist der Pfad, in den geschrieben wird; so steht die Deklaration nur einmal da.
        /// <para>
        /// Diese Eigenschaft <b>ersetzt</b> <see cref="Name"/> in der Pfad-Rolle, sie tritt nicht daneben:
        /// leer heisst „nimm den Feldnamen auch als Pfad". <see cref="Name"/> behaelt daneben seinen
        /// eigenen Job - der Schluessel im Ergebnis, den die Ausgabe-Bindung auf eine Variable abbildet.
        /// </para>
        /// </remarks>
        public string PayloadName { get; set; }

        /// <summary>
        /// Optionaler CScript-Ausdruck, der den <b>angezeigten</b> Wert des Feldes berechnet - statt ihn
        /// aus dem Payload zu lesen. Ausgewertet gegen den Payload der Maske.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Fuer Werte, die es so gar nicht gibt: <c>'System.String'.Format("{0}, {1}", Consultant.LastName,
        /// Consultant.FirstName)</c>. Ein Pfad kann das nicht - er zeigt auf genau ein Member.
        /// </para>
        /// <para>
        /// <b>Ersetzt nur das Lesen.</b> Wohin die Eingabe geht, entscheidet unveraendert die
        /// Ausgabe-Bindung ueber <see cref="Name"/> - ein Feld mit Ausdruck darf also durchaus
        /// bearbeitbar sein. Nur ein Rueckschreiben in einen fremden Datensatz kann es nicht ausloesen:
        /// dafuer braeuchte es einen Pfad, und ein Ausdruck hat keine Umkehrung. Wer beides will, setzt
        /// zusaetzlich einen <see cref="PayloadName"/> - dann wird woanders gelesen als geschrieben, und
        /// der Validator sagt es.
        /// </para>
        /// <para>
        /// Am <b>Start-Knoten</b> ohne Wirkung: dort gibt es noch keinen Payload, gegen den ausgewertet
        /// werden koennte.
        /// </para>
        /// </remarks>
        public string PayloadExpression { get; set; }

        /// <summary>
        /// Wie <see cref="PayloadExpression"/> zu lesen ist: EIN Ausdruck (Standard) oder ein ganzes
        /// Skript mit <c>return</c>.
        /// </summary>
        public ScriptMode PayloadExpressionMode { get; set; } = ScriptMode.Expression;

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
