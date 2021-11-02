using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class BlockEvaluator:EvaluatorBase
    {
        public BlockEvaluator(ICollection<EvaluatorBase> children, ParserRuleContext parserElementContext) : base(null,null, children, parserElementContext, null,null)
        {
        }

        public override void Initialize()
        {
            if (State != EvaluationState.Initial && State != EvaluationState.Done)
            {
                throw new InvalidOperationException("Invalid initial state! should be Initial or done");
            }

            PrepareFor(EvaluationState.PreValuation);
        }

        public override int PassThroughOccurred(EvaluationContext context)
        {
            var retVal = base.PassThroughOccurred(context);
            context.Scope.CollapseScope();
            return retVal;
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

        public override bool PutValueOnStack { get; } = false;
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            PrepareFor(EvaluationState.PostValuation);
            return null;
        }

        protected override object PerformPreValuation(object[] arguments, EvaluationContext context, out bool putOnStack)
        {
            putOnStack = false;
            context.Scope.OpenInnerScope();
            PrepareFor(EvaluationState.EvaluationChildIteration);
            return null;
        }

        protected override object PerformPostValuation(object[] arguments, EvaluationContext context, out bool putOnStack)
        {
            putOnStack = false;
            context.Scope.CollapseScope();
            PrepareFor(EvaluationState.Done);
            return null;
        }
    }
}
