using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Optimization.LazyExecutors;

namespace ITVComponents.Scripting.CScript.Evaluators
{
    public class CallMethodEvaluator:EvaluatorBase
    {
        private readonly EvaluatorBase methodSource;
        private readonly SequenceEvaluator argumentsExpression;
        private readonly SequenceEvaluator genericArgumentsExpression;
        private readonly TypeIdentifierEvaluator explicitType;

        private LazyMethod lazyEvaluator;


        public CallMethodEvaluator(EvaluatorBase methodSource, SequenceEvaluator argumentsExpression, SequenceEvaluator genericArgumentsExpression, TypeIdentifierEvaluator explicitType, ParserRuleContext parserElementContext) : base(null,null, BuildArguments(methodSource, argumentsExpression,genericArgumentsExpression, explicitType), parserElementContext, null,null)
        {
            this.methodSource = methodSource;
            methodSource.ExpectedResult = ResultType.Method;
            this.argumentsExpression = argumentsExpression;
            this.genericArgumentsExpression = genericArgumentsExpression;
            this.explicitType = explicitType;
        }

        protected override object Evaluate(object[] arguments, EvaluationContext context)
        {
            var lastResult = arguments[0] as MethodInformation;
            var tmparg = arguments[1] as object[];
            Type[] tmpTypes = genericArgumentExpressions != null && genericArgumentExpressions.Count != 0 ? new Type[genericArgumentExpressions.Count] : new Type[0];
            Type xpt = null;
            var nextRoot = 0;
            if (tmparg.Length != 0)
            {
                Array.Copy(arguments, nextRoot, tmparg, 0, tmparg.Length);
                nextRoot = tmparg.Length;
            }

            if (tmpTypes.Length != 0)
            {
                Array.Copy(arguments, nextRoot, tmpTypes, 0, tmpTypes.Length);
                nextRoot += tmpTypes.Length;
            }

            if (explicitType != null)
            {
                xpt = (Type) arguments[nextRoot];
            }

            Type type;
            object target = lastResult.BaseObject;
            bool isStatic = false;
            if (lastResult.BaseObject is Type)
            {
                type = (Type)lastResult.BaseObject;
                target = null;
                isStatic = true;
            }
            else if (lastResult.BaseObject is ObjectLiteral ol)
            {
                type = lastResult.BaseObject.GetType();
                FunctionLiteral fl = ol[lastResult.Name] as FunctionLiteral;
                if (fl != null)
                {
                    return fl.Invoke(tmparg);
                }
            }
            else
            {
                type = xpt ?? lastResult.BaseObject.GetType();
            }
            object[] args;
            bool tmpStatic = isStatic;
            MethodInfo method = MethodHelper.GetCapableMethod(type, tmpTypes, lastResult.Name, ref isStatic, tmparg,
                out args);

            if (!tmpStatic && isStatic)
            {
                args[0] = target;
                target = null;
            }

            if (method == null)
            {
                throw new ScriptException(string.Format("No capable Method found for {0}", lastResult.BaseObject
                ));
            }

            var writeBacks = MethodHelper.GetWritebacks(method, args, tmparg);
            lazyEvaluator = new LazyMethod(method, tmpStatic, !tmpStatic && isStatic, args.Length != a.Sequence.Length);

            try
            {
                return method.Invoke(target, args);
            }
            finally
            {
                foreach (var wb in writeBacks)
                {
                    wb.Target.SetValue(args[wb.Index]);
                }
            }
        }

        private static ICollection<EvaluatorBase> BuildArguments(EvaluatorBase evaluatorBase, SequenceEvaluator argumentsExpression, SequenceEvaluator genericArgumentsExpression, TypeIdentifierEvaluator typeIdentifierEvaluator)
        {
            List<EvaluatorBase> retVal = new List<EvaluatorBase>();
            retVal.Add(evaluatorBase);
            if (argumentsExpression != null)
            {
                retVal.Add(argumentsExpression);
            }

            if (genericArgumentsExpression != null)
            {
                retVal.Add(genericArgumentsExpression);
            }

            if (typeIdentifierEvaluator != null)
            {
                retVal.Add(typeIdentifierEvaluator);
            }

            return retVal;
        }
    }
}
