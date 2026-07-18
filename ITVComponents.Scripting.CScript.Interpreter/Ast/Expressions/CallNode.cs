using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Ein Methodenaufruf: a.b(x) oder f(x).
    /// </summary>
    public sealed class CallNode : ExpressionNode
    {
        private readonly IExpressionNode target;
        private readonly IReadOnlyList<IExpressionNode> arguments;
        private readonly IReadOnlyList<IExpressionNode> typeArguments;
        private readonly IExpressionNode explicitType;

        public CallNode(SourcePosition position, IExpressionNode target,
            IReadOnlyList<IExpressionNode> arguments, IReadOnlyList<IExpressionNode> typeArguments = null,
            IExpressionNode explicitType = null)
            : base(position)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            this.typeArguments = typeArguments;
            this.explicitType = explicitType;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Der Aufruf laeuft ueber ScriptValue.GetValue mit ValueType.Method. Dessen
        /// Argumentkonvention ist positionsgebunden und nicht verhandelbar:
        /// [0] Typargumente als SequenceValue oder null, [1] Argumente als SequenceValue
        /// (nie null), [2] expliziter Typ oder null.
        /// </remarks>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            ScriptValue targetValue = target.Evaluate(context);
            SequenceValue argumentValues = Sequence(context, arguments);
            SequenceValue typeArgumentValues = typeArguments == null ? null : Sequence(context, typeArguments);
            ScriptValue explicitTypeValue = explicitType?.Evaluate(context);

            targetValue.ValueType = ScriptValues.ValueType.Method;
            var retVal = new LiteralScriptValue(context.BypassCompatibilityOnLazyInvokation);
            try
            {
                retVal.Initialize(targetValue.GetValue(
                    new ScriptValue[] { typeArgumentValues, argumentValues, explicitTypeValue }, context.Policy));
            }
            catch (ScriptException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ScriptException($"Method-Call failed! at {Position.Line}/{Position.Column}", ex);
            }

            return retVal;
        }

        private static SequenceValue Sequence(ExecutionContext context, IReadOnlyList<IExpressionNode> nodes)
        {
            var values = new ScriptValue[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                values[i] = nodes[i].Evaluate(context);
            }

            var retVal = new SequenceValue(context.BypassCompatibilityOnLazyInvokation);
            retVal.Initialize(values);
            return retVal;
        }
    }
}
