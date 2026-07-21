using System;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Statements
{
    /// <summary>
    /// Eine Fallunterscheidung.
    /// </summary>
    public sealed class IfNode : StatementNode
    {
        private readonly IExpressionNode condition;
        private readonly IStatementNode whenTrue;
        private readonly IStatementNode whenFalse;

        public IfNode(SourcePosition position, IExpressionNode condition, IStatementNode whenTrue,
            IStatementNode whenFalse)
            : base(position)
        {
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.whenTrue = whenTrue ?? throw new ArgumentNullException(nameof(whenTrue));
            this.whenFalse = whenFalse;
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            if (ValueHelper.IsTrue(condition.Evaluate(context), context))
            {
                return whenTrue.Execute(context);
            }

            return whenFalse?.Execute(context) ?? Completion.Normal;
        }
    }
}
