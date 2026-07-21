using System;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.Operating;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Die einstelligen Operatoren.
    /// </summary>
    public enum UnaryOperator
    {
        /// <summary>Unaeres Plus - reicht den Wert unveraendert durch.</summary>
        Plus,

        /// <summary>Unaeres Minus.</summary>
        Minus,

        /// <summary>Logische Verneinung.</summary>
        Not,

        /// <summary>Bitweise Verneinung.</summary>
        Invert
    }

    /// <summary>
    /// Eine einstellige Operation. Ignoriert TypeSafety durchgehend - es gibt keinen zweiten
    /// Operanden, an den ein Typ anzugleichen waere.
    /// </summary>
    public sealed class UnaryOpNode : ExpressionNode
    {
        private readonly IExpressionNode operand;
        private readonly UnaryOperator op;

        public UnaryOpNode(SourcePosition position, IExpressionNode operand, UnaryOperator op)
            : base(position)
        {
            this.operand = operand ?? throw new ArgumentNullException(nameof(operand));
            this.op = op;
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            ScriptValue value = operand.Evaluate(context);
            if (op == UnaryOperator.Not)
            {
                return Literal(context, !ValueHelper.IsTrue(value, context));
            }

            if (op == UnaryOperator.Plus)
            {
                return value;
            }

            object raw = value.GetValue(null, context.Policy);
            try
            {
                switch (op)
                {
                    case UnaryOperator.Minus:
                        return Literal(context, OperationsHelper.UnaryMinus(raw));
                    case UnaryOperator.Invert:
                        return Literal(context, OperationsHelper.Negate(raw));
                    default:
                        throw new ScriptException($"Unbekannter Operator {op}");
                }
            }
            catch (ScriptException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ScriptException($"{op} failed at {Position.Line}/{Position.Column}", ex);
            }
        }
    }
}
