using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class VariableAccessEvaluator:EvaluatorBase
    {
        private string[] accessPath;
        public VariableAccessEvaluator(ITVScriptingParser.TypeIdentifierContext parserElementContext) : base(null,null,null, parserElementContext, null,null)
        {
            accessPath = parserElementContext.Identifier().Select(n => n.GetText()).ToArray();
        }

        public override bool PutValueOnStack => CheckPutOnStack();
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            
        }

        private bool CheckPutOnStack()
        {
            return true;
        }
    }
}
