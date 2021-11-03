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
    public class CallIndexerEvaluator:EvaluatorBase
    {
        private readonly SequenceEvaluator indexArgumentEvaluator;
        private readonly EvaluatorBase explicitType;

        public CallIndexerEvaluator(EvaluatorBase baseValue, SequenceEvaluator indexArgumentEvaluator, EvaluatorBase explicitType, ITVScriptingParser.MemberIndexExpressionContext context) : base(null,null,BuildArguments(baseValue, indexArgumentEvaluator, explicitType),context,null,null)
        {
            this.indexArgumentEvaluator = indexArgumentEvaluator;
            this.explicitType = explicitType;
        }

        public override ResultType ExpectedResult
        {
            get
            {
                return ResultType.PropertyOrField;
            }
            internal set
            {
                if (value != ResultType.PropertyOrField)
                {
                    throw new InvalidOperationException("This is a PropertyOrField-only evaluator!");
                }
            }
        }

        public override AccessMode AccessMode { get; internal set; }
        public override bool PutValueOnStack { get; }
        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            var baseValue = arguments[0];
            var indexArguments = (object[])arguments[1];
            Type exTyp = baseValue?.GetType();
            if (explicitType != null)
            {
                exTyp= (Type)arguments[2];
            }

            if (exTyp != null)
            {
                var indexer = MethodHelper.GetCapableIndexer(exTyp, indexArguments, out var oarg);
                object result = null;
                if ((AccessMode & AccessMode.Read) == AccessMode.Read)
                {
                    result = indexer.GetValue(baseValue, oarg);
                    if (AccessMode == AccessMode.ReadWrite)
                    {
                        return new[]
                        {
                            result,
                            new ActiveCodeAccessDescriptor
                                { Arguments = oarg, BaseObject = baseValue, ExplicitType = exTyp }
                        };
                    }

                    return result;
                }

                return new ActiveCodeAccessDescriptor
                    { Arguments = oarg, BaseObject = baseValue, ExplicitType = exTyp };
            }

            throw new ScriptException("Unable to access Indexer. Object is null.");
        }

        private static List<EvaluatorBase> BuildArguments(EvaluatorBase baseValue, SequenceEvaluator indexAruments,
            EvaluatorBase explicitType)
        {
            List<EvaluatorBase> retVal = new List<EvaluatorBase>();
            retVal.Add(baseValue);
            retVal.Add(indexAruments);
            if (explicitType != null)
            {
                retVal.Add(explicitType);
            }
            return retVal;
        }
    }
}
