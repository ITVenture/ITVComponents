using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Runtime
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
        Throw,

        /// <summary>
        /// Ein throw ohne Ausdruck: die gerade behandelte Ausnahme wird erneut geworfen.
        /// </summary>
        /// <remarks>
        /// Bewusst eine eigene Art und keine Unterform von Throw. Beim ScriptVisitor erbte
        /// ReThrow von Throw, wodurch jedes "is Throw" auch ReThrow traf - unter anderem
        /// deshalb verlor ein ReThrow im Script-Pfad seine Nutzlast und lief oben als
        /// NullReferenceException auf.
        /// </remarks>
        ReThrow
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

        private Completion(CompletionKind kind, ScriptValue value, object thrown, bool catchable)
        {
            Kind = kind;
            Value = value;
            Thrown = thrown;
            Catchable = catchable;
        }

        /// <summary>Wie die Anweisung geendet hat.</summary>
        public CompletionKind Kind { get; }

        /// <summary>
        /// Der Rueckgabewert bei <see cref="CompletionKind.Return"/>, sonst null.
        /// </summary>
        public ScriptValue Value { get; }

        /// <summary>
        /// Das geworfene Objekt bei <see cref="CompletionKind.Throw"/>: bei einem Script-throw
        /// der geworfene Wert, bei einer durchgereichten CLR-Ausnahme diese selbst.
        /// </summary>
        public object Thrown { get; }

        /// <summary>
        /// Gibt an, ob ein catch im Script diesen Fehler abfangen darf.
        /// </summary>
        /// <remarks>
        /// Interne Fehler des Interpreters - etwa ein Ausdruck, der keinen Wahrheitswert
        /// liefert - sind bewusst nicht fangbar. Nur was das Script selbst geworfen hat und
        /// durchgereichte CLR-Ausnahmen sind es.
        /// </remarks>
        public bool Catchable { get; }

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
            return new Completion(CompletionKind.Return, value, null, false);
        }

        public static readonly Completion Break = new Completion(CompletionKind.Break, null, null, false);

        public static readonly Completion Continue = new Completion(CompletionKind.Continue, null, null, false);

        public static readonly Completion ReThrow = new Completion(CompletionKind.ReThrow, null, null, false);

        /// <summary>
        /// Ein Fehler, den das Script mit catch abfangen darf.
        /// </summary>
        public static Completion Throw(object thrown)
        {
            return new Completion(CompletionKind.Throw, null, thrown, true);
        }

        /// <summary>
        /// Ein Fehler des Interpreters selbst, den kein catch im Script abfangen darf.
        /// </summary>
        public static Completion Fail(string message)
        {
            return new Completion(CompletionKind.Throw, null, message, false);
        }

        public override string ToString()
        {
            return Kind == CompletionKind.Throw ? $"Throw({Thrown})" : Kind.ToString();
        }
    }
}
