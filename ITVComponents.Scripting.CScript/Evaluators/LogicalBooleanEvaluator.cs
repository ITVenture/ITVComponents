using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class LogicalBooleanEvaluator:EvaluatorBase
    {
        private readonly string op;

        public LogicalBooleanEvaluator(EvaluatorBase left, EvaluatorBase right, string op, ParserRuleContext context) : base(new[]{left},context,new[]{right}, context, null,null)
        {
            this.op = op;
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
        public override void Initialize()
        {
            if (State != EvaluationState.Initial && State != EvaluationState.Done)
            {
                throw new InvalidOperationException("Invalid initial state! should be Initial or done");
            }

            PrepareFor(EvaluationState.PreValuation);
        }

        protected override object PerformPreValuation(object[] arguments, EvaluationContext context, out bool forcePutOnStack)
        {
            var baseVal = arguments[0];
            var ok = baseVal is true;
            forcePutOnStack = false;
            State = EvaluationState.Evaluation;
            if (op == "&&" && !ok)
            {
                forcePutOnStack = true;
                State = EvaluationState.Done;
            }
            else if (op == "||" && ok)
            {
                forcePutOnStack = true;
                State = EvaluationState.Done;
            }

            return ok;
        }

        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            return arguments[0];
        }
    }
}
