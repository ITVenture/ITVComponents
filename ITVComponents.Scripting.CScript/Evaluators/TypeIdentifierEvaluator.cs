using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Exceptions;

namespace ITVComponents.Scripting.CScript.Evaluators
{ 
    public class TypeIdentifierEvaluator:EvaluatorBase
    {
        string[] accessPath;
        public TypeIdentifierEvaluator(ITVScriptingParser.TypeIdentifierContext parserElementContext) : base(null,null,null, parserElementContext, null,null)
        {
            accessPath = parserElementContext.Identifier().Select(n => n.GetText()).ToArray();
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
            IScope tmpVar = context.Scope;
            var path = accessPath;
            string finalMember = path[path.Length - 1];
            for (int i = 0; i< path.Length-1; i++)
            {
                var targetName = path[i];
                if (!(tmpVar[targetName] is IScope))
                {
                    throw new ScriptException($"Failed to resolve Type {string.Join(".", accessPath)}");
                }

                tmpVar = (IScope) tmpVar[targetName];
            }

            return tmpVar[finalMember];
        }
    }
}
