namespace ITVComponents.AgentServices.Abstractions
{
    /// <summary>
    /// Welche Art Eingabe eine Einstellung erwartet.
    /// </summary>
    /// <remarks>
    /// <b>Dieselbe Reihenfolge wie <c>DeclaredFieldKind</c> im Blazor-Toolkit</b>, und das ist keine
    /// Kosmetik: die Maske dort rendert gegen jene Aufzählung, die Werte werden numerisch abgebildet,
    /// und eine abweichende Reihenfolge machte aus einer Auswahlliste stillschweigend ein Datumsfeld.
    /// Neue Arten gehören ans Ende — hier wie dort. (Hiess früher <c>TerminalSettingKind</c>.)
    /// </remarks>
    public enum SettingKind
    {
        /// <summary>einzeiliger Text</summary>
        Text,

        /// <summary>mehrzeiliger Text</summary>
        MultilineText,

        /// <summary>Zahl</summary>
        Number,

        /// <summary>ja/nein</summary>
        Boolean,

        /// <summary>Datum</summary>
        Date,

        /// <summary>Auswahl aus <see cref="SettingDescriptor.Choices"/> oder <see cref="SettingDescriptor.ChoiceSource"/></summary>
        Choice,

        /// <summary>Datum mit Uhrzeit</summary>
        DateTime
    }

    /// <summary>
    /// Ein Eintrag einer Auswahlliste.
    /// </summary>
    public class SettingChoice
    {
        /// <summary>Der Wert, der gespeichert wird.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>Wie er in der Auswahl heisst; leer = <see cref="Value"/>.</summary>
        public string? Label { get; set; }
    }

    /// <summary>
    /// Beschreibt eine Einstellung so, dass daraus eine Maske entstehen kann.
    /// </summary>
    /// <remarks>
    /// Das gemeinsame Feldmodell für alles, was sich selbst beschreibt — Zahlterminals, Geräte am Agenten.
    /// Wer mehr braucht (etwa, wohin ein Wert gespeichert wird), leitet ab.
    /// </remarks>
    public class SettingDescriptor
    {
        /// <summary>Der Schlüssel, unter dem der Wert übertragen wird.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Die Beschriftung; leer = <see cref="Name"/>.</summary>
        public string? Label { get; set; }

        /// <summary>Die Art der Eingabe.</summary>
        public SettingKind Kind { get; set; } = SettingKind.Text;

        /// <summary>Ob ein Wert gesetzt sein muss.</summary>
        public bool Required { get; set; }

        /// <summary>Ein erklärender Satz unter dem Feld.</summary>
        public string? HelpText { get; set; }

        /// <summary>Die Vorbelegung, als Text.</summary>
        public string? DefaultValue { get; set; }

        /// <summary>Eine feste Auswahl.</summary>
        public IReadOnlyList<SettingChoice>? Choices { get; set; }

        /// <summary>
        /// Der Name einer Quelle, die die Auswahl erst zur Laufzeit liefert. Gesetzt, wenn
        /// <see cref="Choices"/> nicht reicht — etwa, weil nur der Rechner vor Ort die Werte kennt.
        /// </summary>
        public string? ChoiceSource { get; set; }

        /// <summary>
        /// Der Name eines anderen Felds, von dessen Wert die Auswahl abhängt. Ändert sich jenes, wird
        /// die Auswahl neu geholt.
        /// </summary>
        public string? DependsOn { get; set; }
    }
}
