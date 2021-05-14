using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Operating;
using ITVComponents.Scripting.CScript.Optimization.LazyExecutors;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class AdditionEvaluator : EvaluatorBase
    {
        private readonly EvaluatorBase left;
        private readonly EvaluatorBase right;
        private readonly string op;

        private LazyOp operationExecutor;

        public AdditionEvaluator(EvaluatorBase left, EvaluatorBase right, string op, ParserRuleContext parserElementContext) : base(null, null, new[] {left, right}, parserElementContext, null, null)
        {
            this.left = left;
            this.right = right;
            left.Next = right;
            left.Parent = this;
            right.Parent = this;
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
            var lazy = context.LazyEvaluation;
            if (operationExecutor == null || !lazy)
            {
                switch (op)
                {
                    case "+":
                    {
                        if (!lazy)
                        {
                            return OperationsHelper.Add(arguments[0], arguments[1], context.TypeSafety);
                        }

                        operationExecutor = new LazyOp(OperationsHelper.Add, context.TypeSafety);
                        break;
                    }
                    case "-":
                    {
                        if (!lazy)
                        {
                            return OperationsHelper.Subtract(arguments[0], arguments[1], context.TypeSafety);
                        }

                        operationExecutor = new LazyOp(OperationsHelper.Subtract, context.TypeSafety);
                        break;
                    }
                    default:
                    {
                        throw new InvalidOperationException("Unable to perform additive operation.");
                    }
                }
            }

            if (operationExecutor != null && operationExecutor.CanExecute(null, arguments))
            {
                return operationExecutor.Invoke(null, arguments);
            }

            throw new InvalidOperationException("Unable to perform additive operation.");
        }
    }
}
