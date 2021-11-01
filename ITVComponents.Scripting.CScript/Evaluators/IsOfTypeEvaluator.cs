using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Exceptions;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class IsOfTypeEvaluator:EvaluatorBase
    {
        public IsOfTypeEvaluator(EvaluatorBase baseValue, EvaluatorBase compareType, ITVScriptingParser.MemberIsExpressionContext context) : base(null,null,new[]{baseValue, compareType},context,null,null)
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
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            var src = arguments[0];
            if (src == null)
            {
                return false;
            }

            var typ = arguments[1];
            if (typ is Type t)
            {
                return t.IsInstanceOfType(src);
            }

            throw new ScriptException("Type expected!");
        }
    }
}
