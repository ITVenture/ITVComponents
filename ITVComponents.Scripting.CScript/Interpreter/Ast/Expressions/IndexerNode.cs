using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Ast.Expressions
{
    /// <summary>
    /// Indizierter Zugriff: a[i].
    /// </summary>
    public sealed class IndexerNode : ExpressionNode
    {
        private readonly IExpressionNode target;
        private readonly IReadOnlyList<IExpressionNode> indexes;
        private readonly IExpressionNode explicitType;

        public IndexerNode(SourcePosition position, IExpressionNode target,
            IReadOnlyList<IExpressionNode> indexes, IExpressionNode explicitType = null)
            : base(position)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.indexes = indexes ?? throw new ArgumentNullException(nameof(indexes));
            this.explicitType = explicitType;
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            ScriptValue baseValue = target.Evaluate(context);
            var indexValues = new ScriptValue[indexes.Count];
            for (int i = 0; i < indexes.Count; i++)
            {
                indexValues[i] = indexes[i].Evaluate(context);
            }

            Type typeHint = explicitType?.Evaluate(context).GetValue(null, context.Policy) as Type;
            var retVal = new IndexerScriptValue(CacheSlot(context), context.Policy,
                context.BypassCompatibilityOnLazyInvokation);
            retVal.Initialize(baseValue, indexValues, typeHint);
            return retVal;
        }
    }
}
