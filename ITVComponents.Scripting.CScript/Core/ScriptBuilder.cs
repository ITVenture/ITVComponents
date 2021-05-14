using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Evaluators;
using ITVComponents.Scripting.CScript.Evaluators.FlowControl;

namespace ITVComponents.Scripting.CScript.Core
{
    public class ScriptBuilder:ITVScriptingBaseVisitor<EvaluatorBase>
    {
        protected override EvaluatorBase DefaultResult => new RootEvaluator();

        public override EvaluatorBase VisitAdditiveExpression(ITVScriptingParser.AdditiveExpressionContext context)
        {
            ITVScriptingParser.SingleExpressionContext[] subExpressions = context.singleExpression();
            var leftChild = Visit(subExpressions[0]);
            var rightChild = Visit(subExpressions[1]);
            string op = context.GetChild(1).GetText();
            return new AdditionEvaluator(leftChild, rightChild, op, context);
        }

        public override EvaluatorBase VisitArgumentList(ITVScriptingParser.ArgumentListContext context)
        {
            List<EvaluatorBase> elements = new List<EvaluatorBase>();
            if (context != null)
            {
                ITVScriptingParser.SingleExpressionContext[] expressions = context.singleExpression();
                if (expressions != null)
                {
                    foreach (ITVScriptingParser.SingleExpressionContext se in expressions)
                    {
                        EvaluatorBase tmp = Visit(se);
                        elements.Add(tmp);
                    }
                }
            }

            SequenceEvaluator rv = new SequenceEvaluator(elements, context);
            return rv;
        }

        public override EvaluatorBase VisitArguments(ITVScriptingParser.ArgumentsContext context)
        {
            return VisitArgumentList(context.argumentList());
        }

        public override EvaluatorBase VisitArgumentsExpression(ITVScriptingParser.ArgumentsExpressionContext context)
        {
            var argumentEvaluator = (SequenceEvaluator)VisitArguments(context.arguments());
            SequenceEvaluator typeEvaluator = null;
            ITVScriptingParser.TypeArgumentsContext targ = context.typeArguments();
            if (targ != null)
            {
                var genericsContext = targ as ITVScriptingParser.FinalGenericsContext;
                if (genericsContext != null)
                {
                    typeEvaluator = (SequenceEvaluator)VisitFinalGenerics(genericsContext);
                }
                else
                {
                    throw new InvalidOperationException($"Open Generic Arguments are not supported in Methodcalls! at {context.Start.Line}/{context.Start.Column}");
                }
            }

            TypeIdentifierEvaluator explicitTyping = null;
            ITVScriptingParser.ExplicitTypeHintContext ext = context.explicitTypeHint();
            if (ext != null)
            {
                explicitTyping = (TypeIdentifierEvaluator)VisitExplicitTypeHint(ext);
            }

            MemberAccessEvaluator 
        }

        protected override EvaluatorBase AggregateResult(EvaluatorBase aggregate, EvaluatorBase nextResult)
        {
            aggregate.Next = nextResult;
            return nextResult;
        }
    }
}
