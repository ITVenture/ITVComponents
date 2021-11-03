using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Exceptions;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class SwitchEvaluator:EvaluatorBase, IPassThroughBarrier
    {
        private readonly EvaluatorBase selector;
        private readonly CaseClauseEvaluator[] cases;
        private readonly CaseClauseEvaluator defaultCase;
        private readonly ParserRuleContext parserElementContext;
        private List<EvaluatorBase> preValuators = new List<EvaluatorBase>();
        private List<EvaluatorBase> childEvaluator = new List<EvaluatorBase>();

        public SwitchEvaluator(EvaluatorBase selector, CaseClauseEvaluator[] cases, CaseClauseEvaluator defaultCase, ParserRuleContext parserElementContext) : base(null, null, null, parserElementContext, null, null)
        {
            this.selector = selector;
            this.cases = cases;
            this.defaultCase = defaultCase;
            this.parserElementContext = parserElementContext;
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

        protected override ICollection<EvaluatorBase> PreValuationChildren => preValuators;

        public override void Initialize()
        {
            if (State != EvaluationState.Initial && State != EvaluationState.Done)
            {
                throw new InvalidOperationException("Invalid initial state! should be Initial or done");
            }

            if (preValuators.Count == 0)
            {
                preValuators.Add(selector);
                foreach (var item in cases)
                {
                    preValuators.Add(new SequenceEvaluator(item.Label.ToArray(), SequenceType.ExpressionSequence, parserElementContext));
                }
            }
            PrepareFor(EvaluationState.PreValuationChildIteration);
        }

        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            if (arguments != null && arguments.Length != 0)
            {
                var last = arguments[arguments.Length - 1];
                if (last is PassThroughValue pth)
                {
                    if (pth.Type == PassThroughType.Break)
                    {
                        return null;
                    }
                    else if (pth.Type == PassThroughType.Continue)
                    {
                        var id = Array.IndexOf(cases, childEvaluator[0]);
                        if (id != -1)
                        {
                            CaseClauseEvaluator next = null;
                            if (id + 1 < cases.Length)
                            {
                                next = cases[id + 1];
                            }
                            else
                            {
                                next = defaultCase;
                            }

                            PrepareFor(next);
                            return null;
                        }
                    }
                }
            }

            throw new ScriptException("Un-Expected state!");
        }

        protected override object PerformPreValuation(object[] arguments, EvaluationContext context, out bool putOnStack)
        {
            putOnStack = false;
            var desiredLabel = arguments[0];
            var itemId = -1;
            for (int i = 1; i < arguments.Length; i++)
            {
                object[] values = (object[])arguments[i];
                if (values.Any(o => o.Equals(desiredLabel)))
                {
                    itemId += i;
                    break;
                }
            }

            CaseClauseEvaluator targetEvaluator = null;
            if (itemId != -1)
            {
                targetEvaluator = cases[itemId];
            }
            else
            {
                targetEvaluator = defaultCase;
            }

            PrepareFor(targetEvaluator);
            return null;
        }

        public bool CanHandle(PassThroughValue ptv)
        {
            if (ptv.Type == PassThroughType.Break || ptv.Type == PassThroughType.Continue)
            {
                return true;
            }

            return false;
        }

        private void PrepareFor(CaseClauseEvaluator target)
        {
            childEvaluator.Clear();
            childEvaluator.Add(target);
            PrepareFor(EvaluationState.EvaluationChildIteration);
        }
    }
}
