using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Runtime
{
    /// <summary>
    /// Art, auf die eine Anweisung geendet hat.
    /// </summary>
    public enum CompletionKind
    {
        /// <summary>Die Anweisung lief normal durch.</summary>
        Normal,

        /// <summary>Eine Schleife soll abgebrochen werden.</summary>
        Break,

        /// <summary>Eine Schleife soll mit dem naechsten Durchlauf fortfahren.</summary>
        Continue,

        /// <summary>Die aktuelle Funktion soll verlassen werden.</summary>
        Return,

        /// <summary>Im Script wurde geworfen. Laeuft als Signal, nicht als CLR-Exception.</summary>
        Throw
    }

    /// <summary>
    /// Ergebnis einer Anweisung. Ersetzt die Sentinel-Werte (Break, Continue, ReturnValue,
    /// Throw als IPassThroughValue) des ScriptVisitors: die Kontrollfluss-Absicht ist damit
    /// im Typsystem sichtbar und laesst sich nicht mehr versehentlich als Wert weiterreichen.
    /// </summary>
    public readonly struct Completion
    {
        /// <summary>
        /// Eine normal beendete Anweisung ohne Wert.
        /// </summary>
        public static readonly Completion Normal = default;

        private Completion(CompletionKind kind, ScriptValue value, string label)
        {
            Kind = kind;
            Value = value;
            Label = label;
        }

        /// <summary>Wie die Anweisung geendet hat.</summary>
        public CompletionKind Kind { get; }

        /// <summary>
        /// Der mitgefuehrte Wert: der Rueckgabewert bei <see cref="CompletionKind.Return"/>,
        /// das geworfene Objekt bei <see cref="CompletionKind.Throw"/>, sonst null.
        /// </summary>
        public ScriptValue Value { get; }

        /// <summary>
        /// Ziel-Label eines break/continue, falls angegeben; sonst null.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gibt an, ob die Anweisung normal durchlief. Nur dann darf die umgebende
        /// Anweisungsfolge einfach weiterlaufen.
        /// </summary>
        public bool IsNormal => Kind == CompletionKind.Normal;

        /// <summary>
        /// Gibt an, ob dieses Ergebnis nach aussen durchgereicht werden muss, weil die
        /// aktuelle Anweisungsfolge es nicht selbst behandelt.
        /// </summary>
        public bool IsAbrupt => Kind != CompletionKind.Normal;

        public static Completion Return(ScriptValue value)
        {
            return new Completion(CompletionKind.Return, value, null);
        }

        public static Completion Break(string label = null)
        {
            return new Completion(CompletionKind.Break, null, label);
        }

        public static Completion Continue(string label = null)
        {
            return new Completion(CompletionKind.Continue, null, label);
        }

        public static Completion Throw(ScriptValue thrown)
        {
            return new Completion(CompletionKind.Throw, thrown, null);
        }

        public override string ToString()
        {
            return Label != null ? $"{Kind} {Label}" : Kind.ToString();
        }
    }
}
