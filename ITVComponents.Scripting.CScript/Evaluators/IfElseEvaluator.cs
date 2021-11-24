using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class IfElseEvaluator:EvaluatorBase, IImplicitBlock
    {
        private readonly EvaluatorBase trueStatement;
        private readonly EvaluatorBase falseStatement;
        private List<EvaluatorBase> localChildren = new List<EvaluatorBase>();

        public IfElseEvaluator(EvaluatorBase condition, EvaluatorBase trueStatement, EvaluatorBase falseStatement, ITVScriptingParser.IfStatementContext context) : base(new[]{condition}, context, null, context, null, null)
        {
            this.trueStatement = trueStatement;
            this.falseStatement = falseStatement;
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
                if (value != ResultType.Literal)
                {
                    throw new InvalidOperationException("This is a literal-only evaluator!");
                }
            }
        }

        public override bool PutValueOnStack { get; } = false;

        protected override ICollection<EvaluatorBase> Children => localChildren;

        public override void Initialize()
        {
            if (State != EvaluationState.Initial && State != EvaluationState.Done)
            {
                throw new InvalidOperationException("Invalid initial state! should be Initial or done");
            }

            PrepareFor(EvaluationState.PreValuationChildIteration);
        }

        protected override object PerformPreValuation(object[] arguments, EvaluationContext context, out bool putOnStack)
        {
            context.Scope.OpenInnerScope();
            if (arguments[0] is true)
            {
                localChildren.Clear();
                localChildren.Add(trueStatement);
                PrepareFor(EvaluationState.EvaluationChildIteration);
            }
            else if (falseStatement != null)
            {
                localChildren.Clear();
                localChildren.Add(falseStatement);
                PrepareFor(EvaluationState.EvaluationChildIteration);
            }
            else
            {
                PrepareFor(EvaluationState.PostValuation);
            }

            putOnStack = false;
            return null;
        }

        public override int PassThroughOccurred(EvaluationContext context)
        {
            context.Scope.CollapseScope();
            return base.PassThroughOccurred(context);
        }

        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            PrepareFor(EvaluationState.PostValuation);
            return null;
        }

        protected override object PerformPostValuation(object[] arguments, EvaluationContext context, out bool putOnStack)
        {
            context.Scope.CollapseScope();
            putOnStack = false;
            return null;
        }
    }
}
