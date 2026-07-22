using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Ast.Expressions
{
    /// <summary>
    /// Ein Array-Literal: [a, b, c].
    /// </summary>
    public sealed class ArrayLiteralNode : ExpressionNode
    {
        private readonly IReadOnlyList<IExpressionNode> elements;

        public ArrayLiteralNode(SourcePosition position, IReadOnlyList<IExpressionNode> elements)
            : base(position)
        {
            this.elements = elements ?? throw new ArgumentNullException(nameof(elements));
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            if (elements.Count == 0)
            {
                return Literal(context, Array.Empty<object>());
            }

            var values = new object[elements.Count];
            for (int i = 0; i < elements.Count; i++)
            {
                values[i] = elements[i].Evaluate(context).GetValue(null, context.Policy);
            }

            return Literal(context, values);
        }
    }
}
