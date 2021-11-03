using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class RootEvaluator:EvaluatorBase
    {
        public RootEvaluator() : base(null, null, null, null, null, null)
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
                if (value != ResultType.Literal)
                {
                    throw new InvalidOperationException("This is a literal-only evaluator!");
                }
            }
        }

        public override bool PutValueOnStack { get; } = false;

        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            throw new InvalidOperationException("Root evaluator must not be evaluated");
        }
    }
}
