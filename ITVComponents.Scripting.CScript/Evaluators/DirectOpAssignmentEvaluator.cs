using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Operating;
using ITVComponents.Scripting.CScript.Optimization;
using ITVComponents.Scripting.CScript.Optimization.LazyExecutors;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class DirectOpAssignmentEvaluator:EvaluatorBase
    {
        private readonly string op;
        private IAssignableEvaluator writeBack;
        private IExecutor lazyOperator;
        public DirectOpAssignmentEvaluator(EvaluatorBase left, EvaluatorBase right, string op, ITVScriptingParser.AssignmentOperatorExpressionContext context) : base(null,null, new []{left,right}, context, null, null)
        {
            this.op = op;
            if (left is IAssignableEvaluator wb)
            {
                writeBack = wb;
            }
            else
            {
                throw new ScriptException("A writable evaluator was expected!");
            }
        }

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

        public override bool PutValueOnStack { get; } = true;
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            var left = (object[])arguments[0];
            var right = arguments[1];
            var assign = (ActiveCodeAccessDescriptor)left[1];
            var leftValue = left[0];

            object result;
            if (context.LazyEvaluation && lazyOperator != null)
                result = lazyOperator.Invoke(null, new[] { leftValue, right });
            else
            {
                result = OperationsHelper.PerformAppropriateOperation(op, leftValue, right, context.TypeSafety, context.LazyEvaluation, out var lop);
                lazyOperator = lop;
            }

            writeBack.Assign(result, assign);
            return result;
        }
    }
}
