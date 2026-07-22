using System;
using ITVComponents.Scripting.CScript.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Ast.Expressions
{
    /// <summary>
    /// Der Bedingungsoperator: c ? a : b.
    /// </summary>
    /// <remarks>
    /// Gibt den gewaehlten Zweig unveraendert zurueck, statt ihn in einen Literalwert zu
    /// verpacken - der Ausdruck bleibt damit gegebenenfalls schreibbar.
    /// </remarks>
    public sealed class TernaryNode : ExpressionNode
    {
        private readonly IExpressionNode condition;
        private readonly IExpressionNode whenTrue;
        private readonly IExpressionNode whenFalse;

        public TernaryNode(SourcePosition position, IExpressionNode condition, IExpressionNode whenTrue,
            IExpressionNode whenFalse)
            : base(position)
        {
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.whenTrue = whenTrue ?? throw new ArgumentNullException(nameof(whenTrue));
            this.whenFalse = whenFalse ?? throw new ArgumentNullException(nameof(whenFalse));
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            return ValueHelper.IsTrue(condition.Evaluate(context), context)
                ? whenTrue.Evaluate(context)
                : whenFalse.Evaluate(context);
        }
    }

    /// <summary>
    /// Der Null-Zusammenfuehrungsoperator: a ?? b.
    /// </summary>
    public sealed class CoalesceNode : ExpressionNode
    {
        private readonly IExpressionNode value;
        private readonly IExpressionNode fallback;

        public CoalesceNode(SourcePosition position, IExpressionNode value, IExpressionNode fallback)
            : base(position)
        {
            this.value = value ?? throw new ArgumentNullException(nameof(value));
            this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Schliesst kurz - die rechte Seite wird nur ausgewertet, wenn die linke null ist.
        /// Der ScriptVisitor wertet an dieser Stelle beide Seiten aus (Core/ScriptVisitor.cs:2318);
        /// bei einer rechten Seite mit Seiteneffekt ist das sichtbar. Hier ist bewusst die
        /// Semantik gewaehlt, die der Operator in C# hat und die Scripts erwarten.
        /// </remarks>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            object left = value.Evaluate(context).GetValue(null, context.Policy);
            return Literal(context, left ?? fallback.Evaluate(context).GetValue(null, context.Policy));
        }
    }
}
