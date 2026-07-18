using System;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Eine Zuweisung: a = b, oder mit Operator a += b.
    /// </summary>
    public sealed class AssignNode : ExpressionNode
    {
        private readonly IExpressionNode target;
        private readonly IExpressionNode source;
        private readonly BinaryOperator? compoundOperator;

        /// <param name="compoundOperator">
        /// bei einer zusammengesetzten Zuweisung der anzuwendende Operator, sonst null.
        /// </param>
        public AssignNode(SourcePosition position, IExpressionNode target, IExpressionNode source,
            BinaryOperator? compoundOperator = null)
            : base(position)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.compoundOperator = compoundOperator;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Die Zielseite wird genau einmal ausgewertet - auch bei zusammengesetzter Zuweisung.
        /// Der abgeloeste Builder besuchte sie zweimal, was bei einem Ziel mit Seiteneffekt
        /// (a[i++] += 1) den Index doppelt hochzaehlte.
        /// </remarks>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            ScriptValue targetValue = target.Evaluate(context);
            if (!targetValue.Writable)
            {
                throw new ScriptException($"Target is not writable at {Position.Line}/{Position.Column}");
            }

            object newValue;
            if (compoundOperator == null)
            {
                newValue = source.Evaluate(context).GetValue(null, context.Policy);
            }
            else
            {
                var operation = new BinaryOpNode(Position, new PreEvaluated(Position, targetValue), source,
                    compoundOperator.Value);
                newValue = operation.Evaluate(context).GetValue(null, context.Policy);
            }

            targetValue.SetValue(newValue, null, context.Policy);
            return Literal(context, newValue);
        }

        /// <summary>
        /// Reicht einen bereits ausgewerteten Wert als Ausdruck durch. Damit kann die
        /// zusammengesetzte Zuweisung ihre Zielseite wiederverwenden, statt sie erneut
        /// auszuwerten.
        /// </summary>
        private sealed class PreEvaluated : ExpressionNode
        {
            private readonly ScriptValue value;

            public PreEvaluated(SourcePosition position, ScriptValue value)
                : base(position)
            {
                this.value = value;
            }

            public override ScriptValue Evaluate(ExecutionContext context)
            {
                return value;
            }
        }
    }
}
