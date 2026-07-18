using System;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.Operating;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Die Zaehl-Operatoren.
    /// </summary>
    public enum IncrementType
    {
        PreIncrement,
        PostIncrement,
        PreDecrement,
        PostDecrement
    }

    /// <summary>
    /// Ein Zaehl-Operator: ++x, x++, --x, x--.
    /// </summary>
    /// <remarks>
    /// Die Praefix-Form liefert den neuen Wert, die Postfix-Form den alten. Der Operand wird
    /// genau einmal ausgewertet - er kann eine Variable, ein Member oder ein Indexer sein, und
    /// mehrfaches Auswerten haette bei einem Indexer mit Seiteneffekt sichtbare Folgen.
    /// </remarks>
    public sealed class IncrementNode : ExpressionNode
    {
        private readonly IExpressionNode operand;
        private readonly IncrementType incrementType;

        public IncrementNode(SourcePosition position, IExpressionNode operand, IncrementType incrementType)
            : base(position)
        {
            this.operand = operand ?? throw new ArgumentNullException(nameof(operand));
            this.incrementType = incrementType;
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            ScriptValue target = operand.Evaluate(context);
            object oldValue = target.GetValue(null, context.Policy);

            bool isIncrement = incrementType == IncrementType.PreIncrement ||
                               incrementType == IncrementType.PostIncrement;
            object newValue;
            try
            {
                newValue = isIncrement
                    ? OperationsHelper.Increment(oldValue)
                    : OperationsHelper.Decrement(oldValue);
            }
            catch (ScriptException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ScriptException(
                    $"{incrementType} failed at {Position.Line}/{Position.Column}", ex);
            }

            target.SetValue(newValue, null, context.Policy);

            bool isPrefix = incrementType == IncrementType.PreIncrement ||
                            incrementType == IncrementType.PreDecrement;
            return Literal(context, isPrefix ? newValue : oldValue);
        }
    }
}
