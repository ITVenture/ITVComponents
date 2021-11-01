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
    public class VariableAccessEvaluator:EvaluatorBase, IAssignableEvaluator
    {
        private string name;
        private ResultType expectedResult = ResultType.PropertyOrField;
        public VariableAccessEvaluator(ITVScriptingParser.IdentifierExpressionContext parserElementContext) : base(null,null,null, parserElementContext, null,null)
        {
            name = parserElementContext.Identifier().GetText();
        }

        public override ResultType ExpectedResult
        {
            get
            {
                return expectedResult;
            }
            internal set
            {
                if (ExpectedResult == ResultType.Literal)
                {
                    throw new InvalidOperationException("This Evaluator does not support literals!");
                }

                expectedResult = value;
            }
        }
        public override AccessMode AccessMode { get; internal set; }
        public override bool PutValueOnStack => CheckPutOnStack();
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            if (AccessMode == AccessMode.Read || AccessMode == AccessMode.ReadWrite)
            {
                if (context.Scope.ContainsKey(name))
                {
                    var retVal = context.Scope[name];
                    if (ExpectedResult == ResultType.PropertyOrField)
                    {
                        if (AccessMode == AccessMode.Read)
                        {
                            return retVal;
                        }

                        return new object[]
                        {
                            retVal,
                            new ActiveCodeAccessDescriptor { BaseObject = context.Scope, Name = name }
                        };
                    }

                    if (ExpectedResult == ResultType.Method)
                    {
                        return new ActiveCodeAccessDescriptor { BaseObject = context.Scope, Name = name };
                    }

                    if (ExpectedResult == ResultType.Constructor)
                    {
                        return new ActiveCodeAccessDescriptor { BaseObject = context.Scope[name] };
                    }

                    throw new ScriptException($"Result-Type {ExpectedResult} is not supported!");
                }

                throw new ScriptException($"Variable {name} is unknown!");
            }

            return new ActiveCodeAccessDescriptor { Name = name, BaseObject = context.Scope };
        }

        private bool CheckPutOnStack()
        {
            return AccessMode == AccessMode.Read || AccessMode == AccessMode.ReadWrite;
        }

        public void Assign(object value, ActiveCodeAccessDescriptor contextTarget)
        {
            if (contextTarget.BaseObject is IDictionary<string, object> target)
            {
                if ((AccessMode & AccessMode.Write) == AccessMode.Write)
                {
                    target[name] = value;
                }
            }

            throw new ScriptException("This Variable was not properly initialized in Write-Mode");
        }
    }
}
