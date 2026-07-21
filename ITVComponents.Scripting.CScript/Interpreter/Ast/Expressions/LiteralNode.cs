using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Ein konstanter Wert. Zahl, Zeichenkette, Wahrheitswert oder null.
    /// </summary>
    /// <remarks>
    /// Der Wert wird beim Bauen einmal ermittelt (Zahlen ueber OperationsHelper.ParseDecimalValue,
    /// Zeichenketten ueber StringHelper.Parse) und danach nur noch ausgeliefert.
    /// </remarks>
    public sealed class LiteralNode : ExpressionNode
    {
        private readonly object value;

        public LiteralNode(SourcePosition position, object value)
            : base(position)
        {
            this.value = value;
        }

        /// <summary>
        /// Der konstante Wert dieses Knotens.
        /// </summary>
        public object Value => value;

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            return Literal(context, value);
        }
    }
}
