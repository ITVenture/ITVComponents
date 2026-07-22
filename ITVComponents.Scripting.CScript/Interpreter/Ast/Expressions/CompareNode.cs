using System;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Runtime;
using ITVComponents.Scripting.CScript.Operating;
using ITVComponents.Scripting.CScript.Optimization.LazyExecutors;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Ast.Expressions
{
    /// <summary>
    /// Die Vergleichsoperatoren.
    /// </summary>
    public enum ComparisonType
    {
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Equal,
        NotEqual
    }

    /// <summary>
    /// Ein Vergleich zweier Werte.
    /// </summary>
    /// <remarks>
    /// Gleichheit laeuft nicht ueber OperationsHelper, sondern ueber object.Equals, und
    /// ignoriert TypeSafety - so macht es der ScriptVisitor, und daran haengen bestehende
    /// Scripts.
    /// </remarks>
    public sealed class CompareNode : ExpressionNode
    {
        private readonly IExpressionNode left;
        private readonly IExpressionNode right;
        private readonly ComparisonType comparison;

        public CompareNode(SourcePosition position, IExpressionNode left, IExpressionNode right,
            ComparisonType comparison)
            : base(position)
        {
            this.left = left ?? throw new ArgumentNullException(nameof(left));
            this.right = right ?? throw new ArgumentNullException(nameof(right));
            this.comparison = comparison;
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            ScriptValue leftValue = left.Evaluate(context);
            ScriptValue rightValue = right.Evaluate(context);

            if (comparison == ComparisonType.Equal || comparison == ComparisonType.NotEqual)
            {
                object l = leftValue.GetValue(null, context.Policy);
                object r = rightValue.GetValue(null, context.Policy);
                bool isEqual = (l == null && r == null) || (l != null && l.Equals(r));
                return Literal(context, comparison == ComparisonType.Equal ? isEqual : !isEqual);
            }

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
                if (context.TypeSafety)
                {
                    // Nur vergleichbare Werte werden verglichen. Sind sie es nicht, ist das
                    // Ergebnis null - nicht false und keine Exception. Das ist Ist-Verhalten
                    // des ScriptVisitors (Core/ScriptVisitor.cs:1807-1851), das bestehende
                    // Scripts sehen koennen.
                    if (v1 is IComparable && v2 is IComparable)
                    {
                        int cmp = OperationsHelper.Compare(v1, v2, true);
                        return Literal(context, Evaluate(cmp));
                    }

                    return Literal(context, null);
                }

                Func<dynamic, dynamic, bool> compare = Unsafe(comparison);
                bool result = compare(v1, v2);
                if (context.LazyInvokation)
                {
                    SetPreferredExecutor(new LazyOp((a, b, c) => compare(a, b), true, context.Policy));
                }

                return Literal(context, result);
            }
            catch (ScriptException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ScriptException($"Compare failed at {Position.Line}/{Position.Column}", ex);
            }
        }

        private bool Evaluate(int comparisonResult)
        {
            switch (comparison)
            {
                case ComparisonType.GreaterThan:
                    return comparisonResult > 0;
                case ComparisonType.GreaterThanOrEqual:
                    return comparisonResult >= 0;
                case ComparisonType.LessThan:
                    return comparisonResult < 0;
                case ComparisonType.LessThanOrEqual:
                    return comparisonResult <= 0;
                default:
                    throw new ScriptException($"Unerwarteter Vergleich {comparison}");
            }
        }

        private static Func<dynamic, dynamic, bool> Unsafe(ComparisonType comparison)
        {
            switch (comparison)
            {
                case ComparisonType.GreaterThan:
                    return (a, b) => a > b;
                case ComparisonType.GreaterThanOrEqual:
                    return (a, b) => a >= b;
                case ComparisonType.LessThan:
                    return (a, b) => a < b;
                case ComparisonType.LessThanOrEqual:
                    return (a, b) => a <= b;
                default:
                    throw new ScriptException($"Unerwarteter Vergleich {comparison}");
            }
        }
    }
}
