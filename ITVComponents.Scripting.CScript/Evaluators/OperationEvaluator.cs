using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Operating;
using ITVComponents.Scripting.CScript.Optimization;
using ITVComponents.Scripting.CScript.Optimization.LazyExecutors;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class OperationEvaluator : EvaluatorBase
    {
        private readonly EvaluatorBase left;
        private readonly EvaluatorBase right;
        private readonly string op;

        private IExecutor operationExecutor;

        public OperationEvaluator(EvaluatorBase left, EvaluatorBase right, string op, ParserRuleContext parserElementContext) : base(null, null, new[] {left, right}, parserElementContext, null, null)
        {
            this.left = left;
            this.right = right;
            left.Next = right;
            /*left.Parent = this;
            right.Parent = this;*/
            this.op = op;
        }

        public override bool PutValueOnStack { get; } = true;

        public override AccessMode AccessMode
        {
            get
            {
                return AccessMode.Read;
            }
            internal set
            {
                if ((value & AccessMode.Write) == AccessMode.Write)
                {
                    throw new InvalidOperationException("This is a read-only evaluator!");
                }
            }
        }

        public override ResultType ExpectedResult
        {
            get
            {
                return ResultType.Literal;
            }
            internal set
            {
                if (ExpectedResult != ResultType.Literal)
                {
                    throw new InvalidOperationException("This is a literal-only evaluator!");
                }
            }
        }

        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            var left = arguments[0];
            var right = arguments[1];
            object result;
            if (context.LazyEvaluation && operationExecutor != null)
                result = operationExecutor.Invoke(null, new[] { left, right });
            else
            {
                result = OperationsHelper.PerformAppropriateOperation(op, left, right, context.TypeSafety, context.LazyEvaluation, out var lop);
                operationExecutor = lop;
            }

            return result;
        }
    }
}
