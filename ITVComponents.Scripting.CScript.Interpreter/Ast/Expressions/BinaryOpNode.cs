using System;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.Operating;
using ITVComponents.Scripting.CScript.Optimization.LazyExecutors;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Eine zweistellige Rechen- oder Bitoperation.
    /// </summary>
    public sealed class BinaryOpNode : ExpressionNode
    {
        private readonly IExpressionNode left;
        private readonly IExpressionNode right;
        private readonly BinaryOperator op;

        public BinaryOpNode(SourcePosition position, IExpressionNode left, IExpressionNode right, BinaryOperator op)
            : base(position)
        {
            this.left = left ?? throw new ArgumentNullException(nameof(left));
            this.right = right ?? throw new ArgumentNullException(nameof(right));
            this.op = op;
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            ScriptValue leftValue = left.Evaluate(context);
            ScriptValue rightValue = right.Evaluate(context);

            if (context.LazyInvokation)
            {
                object cached = InvokeExecutor(null, new[] { leftValue, rightValue },
                    context.BypassCompatibilityOnLazyInvokation, out bool ok);
                if (ok)
                {
                    return Literal(context, cached);
                }
            }

            object v1 = leftValue.GetValue(null, context.Policy);
            object v2 = rightValue.GetValue(null, context.Policy);

            try
            {
                // Bei &, | und ^ haben Wahrheitswerte Vorrang: OperationsHelper wuerde sie ueber
                // den dynamic-Pfad als Zahlen behandeln. Der ScriptVisitor macht denselben
                // Sonderweg.
                if (v1 is bool b1 && v2 is bool b2)
                {
                    switch (op)
                    {
                        case BinaryOperator.And:
                            return Literal(context, b1 & b2);
                        case BinaryOperator.Or:
                            return Literal(context, b1 | b2);
                        case BinaryOperator.Xor:
                            return Literal(context, b1 ^ b2);
                    }
                }

                object result = Apply(v1, v2, context.TypeSafety);
                if (context.LazyInvokation)
                {
                    SetPreferredExecutor(new LazyOp(Invoker(op), context.TypeSafety, context.Policy));
                }

                return Literal(context, result);
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

        private object Apply(object v1, object v2, bool typeSafety)
        {
            switch (op)
            {
                case BinaryOperator.Add:
                    return OperationsHelper.Add(v1, v2, typeSafety);
                case BinaryOperator.Subtract:
                    return OperationsHelper.Subtract(v1, v2, typeSafety);
                case BinaryOperator.Multiply:
                    return OperationsHelper.Multiply(v1, v2, typeSafety);
                case BinaryOperator.Divide:
                    return OperationsHelper.Divide(v1, v2, typeSafety);
                case BinaryOperator.Modulus:
                    return OperationsHelper.Modulus(v1, v2, typeSafety);
                case BinaryOperator.And:
                    return OperationsHelper.And(v1, v2, typeSafety);
                case BinaryOperator.Or:
                    return OperationsHelper.Or(v1, v2, typeSafety);
                case BinaryOperator.Xor:
                    return OperationsHelper.Xor(v1, v2, typeSafety);
                case BinaryOperator.LeftShift:
                    // Schiebeoperationen ignorieren TypeSafety - der Verschiebebetrag muss
                    // nicht den Typ des linken Operanden annehmen.
                    return OperationsHelper.LShift(v1, v2);
                case BinaryOperator.RightShift:
                    return OperationsHelper.RShift(v1, v2);
                default:
                    throw new ScriptException($"Unbekannter Operator {op}");
            }
        }

        private static Func<object, object, bool, object> Invoker(BinaryOperator op)
        {
            switch (op)
            {
                case BinaryOperator.Add:
                    return OperationsHelper.Add;
                case BinaryOperator.Subtract:
                    return OperationsHelper.Subtract;
                case BinaryOperator.Multiply:
                    return OperationsHelper.Multiply;
                case BinaryOperator.Divide:
                    return OperationsHelper.Divide;
                case BinaryOperator.Modulus:
                    return OperationsHelper.Modulus;
                case BinaryOperator.And:
                    return OperationsHelper.And;
                case BinaryOperator.Or:
                    return OperationsHelper.Or;
                case BinaryOperator.Xor:
                    return OperationsHelper.Xor;
                case BinaryOperator.LeftShift:
                    return OperationsHelper.LShift;
                case BinaryOperator.RightShift:
                    return OperationsHelper.RShift;
                default:
                    throw new ScriptException($"Unbekannter Operator {op}");
            }
        }
    }
}
