using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Exceptions;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class VariableAccessEvaluator:EvaluatorBase
    {
        private string[] accessPath;
        public VariableAccessEvaluator(ITVScriptingParser.TypeIdentifierContext parserElementContext) : base(null,null,null, parserElementContext, null,null)
        {
            accessPath = parserElementContext.Identifier().Select(n => n.GetText()).ToArray();
        }

        public override ResultType ExpectedResult { get; internal set; }
        public override AccessMode AccessMode { get; internal set; }
        public override bool PutValueOnStack => CheckPutOnStack();
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            if (AccessMode == AccessMode.Read || AccessMode == AccessMode.ReadWrite)
            {

            }

            throw new ScriptException("This is a write-only Evaluator and should not be evaluated!");
        }

        private bool CheckPutOnStack()
        {
            return AccessMode == AccessMode.Read || AccessMode == AccessMode.ReadWrite;
        }
    }
}
