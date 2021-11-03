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
    public class PassThroughValueEvaluator:EvaluatorBase
    {
        private PassThroughType type;
        public PassThroughValueEvaluator(ITVScriptingParser.ContinueStatementContext context) : base(null,null,null,context,null,null)
        {
            type = PassThroughType.Continue;
        }

        public PassThroughValueEvaluator(ITVScriptingParser.BreakStatementContext context) : base(null, null, null, context, null, null)
        {
            type = PassThroughType.Break;
        }

        public PassThroughValueEvaluator(ITVScriptingParser.ReturnStatementContext context, EvaluatorBase resultEvaluator) : base(null, null, new[]{resultEvaluator}, context, null, null)
        {
            type = PassThroughType.Return;
        }

        public PassThroughValueEvaluator(ITVScriptingParser.ReturnStatementContext context) : base(null, null, null, context, null, null)
        {
            type = PassThroughType.Return;
        }

        public PassThroughValueEvaluator(ITVScriptingParser.ThrowStatementContext context, EvaluatorBase exceptionEvaluator) : base(null, null, new[]{exceptionEvaluator}, context, null, null)
        {
            type = PassThroughType.Exception;
        }

        public PassThroughValueEvaluator(ITVScriptingParser.ThrowStatementContext context) : base(null, null, null, context, null, null)
        {
            type = PassThroughType.Exception;
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
        public override bool PutValueOnStack { get; }
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            object value = null;
            if (arguments != null && arguments.Length == 1)
            {
                value = arguments[0];
            }
            else if (type == PassThroughType.Exception)
            {
                if (!context.Scope.ContainsKey("$$exception"))
                {
                    throw new ScriptException("Re-throw requires an existing exception!");
                }

                value = context.Scope["$$exception"];
            }

            return new PassThroughValue(type, value);
        }
    }
}
