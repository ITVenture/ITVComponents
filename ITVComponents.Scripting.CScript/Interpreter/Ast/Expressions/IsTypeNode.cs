using System;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Typpruefung: x is 'System.Int32'.
    /// </summary>
    public sealed class IsTypeNode : ExpressionNode
    {
        private readonly IExpressionNode value;
        private readonly IExpressionNode type;

        public IsTypeNode(SourcePosition position, IExpressionNode value, IExpressionNode type)
            : base(position)
        {
            this.value = value ?? throw new ArgumentNullException(nameof(value));
            this.type = type ?? throw new ArgumentNullException(nameof(type));
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            object sample = value.Evaluate(context).GetValue(null, context.Policy);
            if (sample == null)
            {
                // null ist von keinem Typ - die rechte Seite wird dann gar nicht ausgewertet.
                return Literal(context, false);
            }

            object typeValue = type.Evaluate(context).GetValue(null, context.Policy);
            if (!(typeValue is Type targetType))
            {
                throw new ScriptException($"Type expected at {Position.Line}/{Position.Column}");
            }

            return Literal(context, targetType.IsInstanceOfType(sample));
        }
    }
}
