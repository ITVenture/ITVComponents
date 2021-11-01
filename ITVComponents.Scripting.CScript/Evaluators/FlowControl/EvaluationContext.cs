using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Exceptions;

namespace ITVComponents.Scripting.CScript.Evaluators.FlowControl
{
    public class EvaluationContext
    {
        public Scope Scope { get; set; }

        public bool LazyEvaluation { get; set; } = false;
        public bool TypeSafety { get; set; }
        public object LastResult => values.Count != 0 ? values.Peek() : null;

        private Stack<object> values = new Stack<object>();

        private Stack<EvaluatorBase> evaluators = new Stack<EvaluatorBase>();

        public object[] ReadStack(int childrenCount)
        {
            object[] retVal = new object[childrenCount];
            for (int i = 0, a=childrenCount-1; i < childrenCount;i++,a--)
            {
                retVal[a] = values.Pop();
            }

            return retVal;
        }

        public void Initialize(EvaluatorBase evaluator)
        {
            evaluators.Clear();
            evaluators.Push(evaluator);
        }

        public object EvaluateProgram()
        {
            object retVal = null;
            while (evaluators.Count != 0)
            {
                var tmp = EvaluateStep(out var ce);
                if (tmp != null)
                {
                    retVal = tmp;
                }
            }

            if (retVal is PassThroughValue v)
            {
                if (v.Type == PassThroughType.Return)
                {
                    retVal = v.Value;
                }
                else if (v.Type == PassThroughType.Exception && v.Value is Exception ex)
                {
                    throw new ScriptException($"Execution failed! Error: {ex.Message}", ex);
                }
                else
                {
                    retVal = null;
                }
            }

            return retVal;
        }

        public object EvaluateStep(out EvaluatorBase currentEvaluator)
        {
            var current = evaluators.Peek();

            if (current.State == EvaluationState.Initial || current.State == EvaluationState.Done)
            {
                current.Initialize();
            }

            if (current.State == EvaluationState.EvaluationChildIteration || current.State == EvaluationState.PreValuationChildIteration || current.State == EvaluationState.PostValuationChildIteration)
            {
                var ch = current.FirstChild();
                if (ch == null)
                {
                    throw new InvalidOperationException("Invalid state of current evaluation-item");
                }

                evaluators.Push(ch);
                currentEvaluator = ch;
                return null;
            }

            object value = null;
            if (current.State == EvaluationState.Evaluation || current.State == EvaluationState.PostValuation || current.State == EvaluationState.PreValuation)
            {
                try
                {
                    var currentState = current.State;
                    value = current.Evaluate(this, out var forcePutOnStack);
                    if (current.PutValueOnStack && currentState == EvaluationState.Evaluation ||
                        forcePutOnStack)
                    {
                        values.Push(value);
                    }
                }
                catch (ScriptException ex)
                {
                    value = new PassThroughValue(PassThroughType.Exception, ex);
                }
            }

            if (current.State == EvaluationState.Done)
            {
                var c = evaluators.Pop();
                if (current != c)
                {
                    throw new InvalidOperationException("Invalid stack state");
                }

                if (value is not PassThroughValue ptv)
                {
                    var nx = current.Next;
                    evaluators.Push(nx);
                    currentEvaluator = nx;
                }
                else
                {
                    currentEvaluator = null;
                    while (evaluators.Count != 0)
                    {
                        var v = evaluators.Peek();
                        if (v is IPassThroughBarrier ipb && ipb.CanHandle(ptv))
                        {
                            values.Push(ptv);
                            currentEvaluator = v;
                            break;
                        }

                        var stackSize = v.PassThroughOccurred();
                        ReadStack(stackSize - 1);
                        evaluators.Pop();
                    }
                }
            }
            else
            {
                currentEvaluator = current;
            }

            return value;
        }
    }
}

//
