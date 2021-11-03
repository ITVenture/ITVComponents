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

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class AssignmentEvaluator:EvaluatorBase
    {
        private IAssignableEvaluator writeBack;
        public AssignmentEvaluator(EvaluatorBase left, EvaluatorBase right, ITVScriptingParser.AssignmentExpressionContext context) : base(null, null, new[] { left, right }, context, null, null)
        {
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
                if (value!= ResultType.Literal)
                {
                    throw new InvalidOperationException("This is a literal-only evaluator!");
                }
            }
        }

        public override bool PutValueOnStack { get; } = true;
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            var left = (ActiveCodeAccessDescriptor)arguments[0];
            var right = arguments[1];

            writeBack.Assign(right, left);
            return right;
        }
    }
}
