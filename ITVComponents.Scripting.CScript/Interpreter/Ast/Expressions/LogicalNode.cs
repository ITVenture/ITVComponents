using System;
using ITVComponents.Scripting.CScript.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Ast.Expressions
{
    /// <summary>
    /// Die kurzschliessenden logischen Operatoren.
    /// </summary>
    public sealed class LogicalNode : ExpressionNode
    {
        private readonly IExpressionNode left;
        private readonly IExpressionNode right;
        private readonly bool isAnd;

        public LogicalNode(SourcePosition position, IExpressionNode left, IExpressionNode right, bool isAnd)
            : base(position)
        {
            this.left = left ?? throw new ArgumentNullException(nameof(left));
            this.right = right ?? throw new ArgumentNullException(nameof(right));
            this.isAnd = isAnd;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Kurzschluss ist hier nicht bloss eine Optimierung, sondern Semantik: in
        /// "1 + 2 &gt; 3 &amp;&amp; 3 / 0 != 5" darf die Division nie ausgefuehrt werden.
        /// Deshalb ein eigener Knoten und kein BinaryOpNode.
        /// </remarks>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            bool leftTrue = ValueHelper.IsTrue(left.Evaluate(context), context);
            if (isAnd)
            {
                if (!leftTrue)
                {
                    return Literal(context, false);
                }

                return Literal(context, ValueHelper.IsTrue(right.Evaluate(context), context));
            }

            if (leftTrue)
            {
                return Literal(context, true);
            }

            return Literal(context, ValueHelper.IsTrue(right.Evaluate(context), context));
        }
    }
}
