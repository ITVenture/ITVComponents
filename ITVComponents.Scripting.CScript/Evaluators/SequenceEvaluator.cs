using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class SequenceEvaluator : EvaluatorBase
    {
        private readonly ICollection<EvaluatorBase> children;
        private readonly SequenceType type;

        public SequenceEvaluator(ICollection<EvaluatorBase> children, SequenceType type,
            ParserRuleContext parserElementContext) : base(null, null, children, parserElementContext, null, null)
        {
            this.children = children;
            this.type = type;
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

        public EvaluatorBase OneAndOnly
        {
            get
            {
                EvaluatorBase retVal = null;
                if (children.Count == 1)
                {
                    retVal = children.First();
                }

                return retVal;
            }
        }

        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            return type == SequenceType.ExpressionSequence ? arguments : null;
        }
    }

    public enum SequenceType
    {
        StatementSequence,
        ExpressionSequence
    }
}
