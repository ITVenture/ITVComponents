using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Exceptions;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class CallConstructorEvaluator:EvaluatorBase
    {
        private readonly SequenceEvaluator arguments;
        private readonly SequenceEvaluator genericArguments;

        public CallConstructorEvaluator(EvaluatorBase baseValue, SequenceEvaluator arguments, SequenceEvaluator genericArguments, ITVScriptingParser.NewExpressionContext context) : base(null, null, BuildArguments(baseValue, genericArguments, arguments), context, null, null)
        {
            this.arguments = arguments;
            this.genericArguments = genericArguments;
        }

        public override ResultType ExpectedResult
        {
            get
            {
                return ResultType.Constructor;
            }
            internal set
            {
                if (value != ResultType.Constructor)
                {
                    throw new InvalidOperationException("This is a constructor-only evaluator!");
                }
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
        public override bool PutValueOnStack { get; }
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            var type = arguments[0];
            var nextId = 0;
            if (type is Type t)
            {
                Type[] typeArguments = null;
                object[] methodArguments = null;
                if (genericArguments != null)
                {
                    typeArguments = ((object[])arguments[nextId]).Cast<Type>().ToArray();
                    nextId++;
                }

                if (this.arguments != null)
                {
                    methodArguments = (object[])arguments[nextId];
                }

                if (t.IsGenericTypeDefinition && typeArguments != null)
                {
                    t = t.MakeGenericType(typeArguments);
                }
                else if (typeArguments != null)
                {
                    throw new ScriptException("A Generic-Type was expected!");
                }

                if (t.IsGenericTypeDefinition)
                {
                    throw new ScriptException("Unable to create an Instance of a generic type without Type-Arguments!");
                }

                var info = MethodHelper.GetCapableConstructor(t, methodArguments, out var oarg);
                return info.Invoke(oarg);
            }

            throw new ScriptException("An Un-Expected value was provided by the child-evaluator!");
        }

        private static List<EvaluatorBase> BuildArguments(EvaluatorBase baseValue, SequenceEvaluator genericArguments,
            SequenceEvaluator arguments)
        {
            var retVal = new List<EvaluatorBase>();
            retVal.Add(baseValue);
            if (genericArguments != null)
            {
                retVal.Add(genericArguments);
            }

            if (arguments != null)
            {
                retVal.Add(arguments);
            }

            return retVal;
        }
    }
}
