using ITVComponents.Scripting.CScript.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Ast.Expressions
{
    /// <summary>
    /// Die Schalter, die ein Script ueber ein Zeichenketten-Literal umlegen kann.
    /// </summary>
    public enum PragmaKind
    {
        /// <summary>Typpruefung bei Operationen (@@TYPESAFETY).</summary>
        TypeSafety,

        /// <summary>Abkuerzen von Aufrufen ueber den Inline-Cache (@@LAZYINVOKATION).</summary>
        LazyInvokation,

        /// <summary>Kompatibilitaetspruefung des Inline-Cache ueberspringen (@@LAZYINVOKATIONSTATICBIND).</summary>
        StaticBind
    }

    /// <summary>
    /// Ein Zeichenketten-Literal, das einen Ausfuehrungsschalter umlegt.
    /// </summary>
    /// <remarks>
    /// Erkannt wird der Schalter beim Bauen - der Text ist konstant, ein Zeichenkettenvergleich
    /// pro Auswertung waere Verschwendung. Gesetzt wird er erst beim Auswerten, denn er gilt ab
    /// seiner Stelle im Ablauf und nicht fuer das ganze Script: ein Pragma in einem if-Zweig
    /// wirkt nur, wenn dieser Zweig auch laeuft.
    ///
    /// Das Literal liefert weiterhin seinen Text als Wert - der Schalter ist ein Seiteneffekt,
    /// kein Ersatz. So macht es der ScriptVisitor, und Scripts koennen den Text verwenden.
    /// </remarks>
    public sealed class PragmaNode : ExpressionNode
    {
        private readonly string text;
        private readonly PragmaKind kind;
        private readonly bool enabled;

        private PragmaNode(SourcePosition position, string text, PragmaKind kind, bool enabled)
            : base(position)
        {
            this.text = text;
            this.kind = kind;
            this.enabled = enabled;
        }

        /// <summary>
        /// Erkennt ein Pragma in einem Zeichenketten-Literal.
        /// </summary>
        /// <param name="position">die Quellposition</param>
        /// <param name="text">der Inhalt des Literals</param>
        /// <returns>der Pragma-Knoten, oder null wenn der Text kein Pragma ist</returns>
        public static PragmaNode TryCreate(SourcePosition position, string text)
        {
            switch (text?.ToUpperInvariant())
            {
                case "@@TYPESAFETY ON":
                    return new PragmaNode(position, text, PragmaKind.TypeSafety, true);
                case "@@TYPESAFETY OFF":
                    return new PragmaNode(position, text, PragmaKind.TypeSafety, false);
                case "@@LAZYINVOKATION ON":
                    return new PragmaNode(position, text, PragmaKind.LazyInvokation, true);
                case "@@LAZYINVOKATION OFF":
                    return new PragmaNode(position, text, PragmaKind.LazyInvokation, false);
                case "@@LAZYINVOKATIONSTATICBIND ON":
                    return new PragmaNode(position, text, PragmaKind.StaticBind, true);
                case "@@LAZYINVOKATIONSTATICBIND OFF":
                    // Der abgeloeste ExpressionExecutorBuilder setzte hier ebenfalls true
                    // (Copy-Paste aus dem ON-Zweig) - der Schalter liess sich damit nicht
                    // wieder abschalten. ScriptVisitor macht es richtig.
                    return new PragmaNode(position, text, PragmaKind.StaticBind, false);
                default:
                    return null;
            }
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            switch (kind)
            {
                case PragmaKind.TypeSafety:
                    context.TypeSafety = enabled;
                    break;
                case PragmaKind.LazyInvokation:
                    context.LazyInvokation = enabled;
                    break;
                case PragmaKind.StaticBind:
                    context.BypassCompatibilityOnLazyInvokation = enabled;
                    break;
            }

            return Literal(context, text);
        }
    }
}
