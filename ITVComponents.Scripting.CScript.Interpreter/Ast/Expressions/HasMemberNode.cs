using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Existenzpruefung eines Members: x has Length beziehungsweise x has ToString().
    /// </summary>
    /// <remarks>
    /// Nimmt bewusst nie am Inline-Cache teil: der Wert wird nur geprueft, nicht geholt, und
    /// CanGetValue registriert konsequenterweise auch keinen Executor.
    /// </remarks>
    public sealed class HasMemberNode : ExpressionNode
    {
        private readonly IExpressionNode target;
        private readonly string memberName;
        private readonly IReadOnlyList<IExpressionNode> arguments;
        private readonly IReadOnlyList<IExpressionNode> typeArguments;
        private readonly IExpressionNode explicitType;

        public HasMemberNode(SourcePosition position, IExpressionNode target, string memberName,
            IReadOnlyList<IExpressionNode> arguments = null, IReadOnlyList<IExpressionNode> typeArguments = null,
            IExpressionNode explicitType = null)
            : base(position)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.memberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
            this.arguments = arguments;
            this.typeArguments = typeArguments;
            this.explicitType = explicitType;
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            ScriptValue baseValue = target.Evaluate(context);
            Type typeHint = explicitType?.Evaluate(context).GetValue(null, context.Policy) as Type;

            var access = new MemberAccessValue(null, context.BypassCompatibilityOnLazyInvokation, context.Policy);
            access.Initialize(baseValue, memberName, typeHint);

            if (arguments == null)
            {
                return Literal(context, access.CanGetValue(null, context.Policy));
            }

            SequenceValue argumentValues = Sequence(context, arguments);
            SequenceValue typeArgumentValues = typeArguments == null ? null : Sequence(context, typeArguments);
            ScriptValue explicitTypeValue = explicitType?.Evaluate(context);

            access.ValueType = ScriptValues.ValueType.Method;
            return Literal(context, access.CanGetValue(
                new ScriptValue[] { typeArgumentValues, argumentValues, explicitTypeValue }, context.Policy));
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
