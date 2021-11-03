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
    public class LiteralExtensionEvaluator:EvaluatorBase
    {
        private readonly ExtensionType extensionType;

        public LiteralExtensionEvaluator(LiteralEvaluator baseEvaluator, ExtensionType extensionType, ParserRuleContext context) : base(null,null,baseEvaluator!=null?new[]{baseEvaluator}:null,context, null,null)
        {
            this.extensionType = extensionType;
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

        public override bool PutValueOnStack { get; } = true;
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            if (arguments.Length == 1)
            {
                var basevalue = arguments[0];
                if (basevalue is not Type t)
                {
                    throw new ScriptException("Type expected!");
                }

                if (extensionType == ExtensionType.Null)
                {
                    return new TypedNull { Type = t };
                }

                return new ReferenceWrapper { Type = t.MakeByRefType() };
            }

            if (extensionType != null)
            {
                throw new ScriptException("A Base-Value is required!");
            }

            return null;
        }
    }

    public enum ExtensionType
    {
        Null,
        Ref
    }
}
