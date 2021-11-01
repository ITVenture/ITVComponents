﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class TernaryEvaluator:EvaluatorBase
    {
        public TernaryEvaluator(EvaluatorBase condition,  EvaluatorBase trueEvaluator, EvaluatorBase falseEvaluator, ParserRuleContext context) : base(new[] { condition }, null, new[]{trueEvaluator}, context, new[]{falseEvaluator}, null)
        {
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

            PrepareFor(EvaluationState.PreValuationChildIteration);
        }

        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            return arguments[0];
        }

        protected override object PerformPostValuation(object[] arguments, EvaluationContext context, out bool putOnStack)
        {
            putOnStack = true;
            State = EvaluationState.Done;
            return arguments[0];
        }

        protected override object PerformPreValuation(object[] arguments, EvaluationContext context, out bool putOnStack)
        {
            var ok = arguments[0] is true;
            putOnStack = false;
            State = ok ? EvaluationState.EvaluationChildIteration : EvaluationState.PostValuationChildIteration;
            return ok;
        }
    }
}
