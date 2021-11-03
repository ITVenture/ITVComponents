using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Exceptions;

namespace ITVComponents.Scripting.CScript.Evaluators.FlowControl
{
    public abstract class EvaluatorBase
    {
        /// <summary>
        /// The parser element which this evaluator instance is based on for pre-evaluation
        /// </summary>
        private readonly ParserRuleContext preValuationParserElementContext;

        /// <summary>
        /// The parser element which this evaluator instance is based on
        /// </summary>
        private readonly ParserRuleContext parserElementContext;

        /// <summary>
        /// The parser element which this evaluator instance is based on for post-evaluation
        /// </summary>
        private readonly ParserRuleContext postValuationParserElementContext;

        /// <summary>
        /// the number of evaluated chlidren
        /// </summary>
        private int evaluated = 0;

        private readonly ICollection<EvaluatorBase> preValuationChildren;
        private readonly ICollection<EvaluatorBase> children;
        private readonly ICollection<EvaluatorBase> postValuationChildren;

        /// <summary>
        /// Initializes a new instance of the EvaluatorBase class
        /// </summary>
        /// <param name="children">a list of children that are required to estimate the value of this evaluator</param>
        /// <param name="parserElementContext">The parser element which this evaluator instance is based on</param>
        /// <param name="preValuationChildren">a list of children that are required to estimate the preprocessing-value of this evaluator</param>
        /// <param name="postValuationChildren">a list of children that are required to estimate the postprocessing-value of this evaluator</param>
        /// <param name="preValuationParserElementContext">The pre-evaluation parser element which this evaluator instance is based on</param>
        /// <param name="postValuationParserElementContext">The post-evaluation parser element which this evaluator instance is based on</param>
        protected EvaluatorBase(ICollection<EvaluatorBase> preValuationChildren, ParserRuleContext preValuationParserElementContext, ICollection<EvaluatorBase> children, ParserRuleContext parserElementContext,ICollection<EvaluatorBase> postValuationChildren, ParserRuleContext postValuationParserElementContext)
        {
            this.preValuationChildren = preValuationChildren;
            this.preValuationParserElementContext = preValuationParserElementContext;
            this.children = children;
            this.parserElementContext = parserElementContext;
            this.postValuationChildren = postValuationChildren;
            this.postValuationParserElementContext = postValuationParserElementContext;
            State = EvaluationState.Initial;
        }

        /// <summary>
        /// Gets or sets the next Evaluator that is on the same level as this evaluator
        /// </summary>
        public EvaluatorBase Next { get; internal set; }

        /// <summary>
        /// Gets or sets the parentEvaluator that represents this evaluators main-scope
        /// </summary>
        public EvaluatorBase Parent { get; internal set; }

        /// <summary>
        /// Gets or sets the current evaluation state of this evaluator
        /// </summary>
        public EvaluationState State { get; protected set; }

        /// <summary>
        /// Gets or sets the Expected result type of this Evaluator implementation
        /// </summary>
        public abstract ResultType ExpectedResult { get; internal set; }

        /// <summary>
        /// Gets or sets the expected Access-Type of this Evaluator implementation
        /// </summary>
        public abstract AccessMode AccessMode { get;internal set; }

        /// <summary>
        /// Indicates wheter th put the outcome of this Evaluator on the value-stack
        /// </summary>
        public abstract bool PutValueOnStack{get;}

        /// <summary>
        /// The number of child-evaluators
        /// </summary>
        public int EvaluationChildCount
        {
            get
            {
                var retVal = 0;
                if (Children != null)
                {
                    retVal = children.Count;
                }

                return retVal;
            }
        }

        /// <summary>
        /// a list of pre-evaluation chlidren of this evaluator item
        /// </summary>
        protected virtual ICollection<EvaluatorBase> PreValuationChildren => preValuationChildren;

        /// <summary>
        /// a list of chlidren of this evaluator item
        /// </summary>
        protected virtual ICollection<EvaluatorBase> Children => children;

        /// <summary>
        /// a list of post-evaluation chlidren of this evaluator item
        /// </summary>
        protected virtual ICollection<EvaluatorBase> PostValuationChildren => postValuationChildren;

        /// <summary>
        /// Prepares this item for initialization
        /// </summary>
        public virtual void Initialize()
        {
            if (State != EvaluationState.Initial && State != EvaluationState.Done)
            {
                throw new InvalidOperationException("Invalid initial state! should be Initial or done");
            }

            if ((Children?.Count ?? 0) != 0)
            {
                PrepareFor(EvaluationState.EvaluationChildIteration);
            }
            else
            {
                PrepareFor(EvaluationState.Evaluation);
            }
        }

        /// <summary>
        /// Evaluates the value of the expression or program that is represented by this evaluator instance
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public object Evaluate(EvaluationContext context, out bool forcePutOnStack)
        {
            forcePutOnStack = false;
            var arguments = context.ReadStack(evaluated);
            evaluated = 0;
            if (State == EvaluationState.PreValuation)
            {
                try
                {
                    return PerformPreValuation(arguments, context, out forcePutOnStack);
                }
                catch (Exception ex)
                {
                    Parent?.IncreaseEvaluatedChildren();
                    State = EvaluationState.Done;
                    throw new ScriptException($"Execution error occurred at {preValuationParserElementContext.Start.Line}/{preValuationParserElementContext.Start.Column} ({preValuationParserElementContext.GetText()}): {ex.Message}", ex);
                }
                finally
                {
                    if (State == EvaluationState.PreValuation)
                    {
                        throw new InvalidOperationException("PreValuation should handle next state!");
                    }

                    if (forcePutOnStack)
                    {
                        Parent?.IncreaseEvaluatedChildren();
                    }
                }
            }
            
            if (State == EvaluationState.Evaluation)
            {
                if (PutValueOnStack)
                {
                    Parent?.IncreaseEvaluatedChildren();
                }

                try
                {
                    return Evaluate(arguments, context);
                }
                catch (Exception ex)
                {
                    if (!PutValueOnStack)
                    {
                        Parent?.IncreaseEvaluatedChildren();
                    }

                    throw new ScriptException($"Execution error occurred at {parserElementContext.Start.Line}/{parserElementContext.Start.Column} ({parserElementContext.GetText()}): {ex.Message}", ex);
                }
                finally
                {
                    if (State == EvaluationState.Evaluation)
                    {
                        State = EvaluationState.Done;
                    }
                }
            }

            if (State == EvaluationState.PostValuation)
            {
                try
                {
                    return PerformPostValuation(arguments, context, out forcePutOnStack);
                }
                catch (Exception ex)
                {
                    Parent?.IncreaseEvaluatedChildren();
                    State = EvaluationState.Done;
                    throw new ScriptException($"Execution error occurred at {postValuationParserElementContext.Start.Line}/{postValuationParserElementContext.Start.Column} ({postValuationParserElementContext.GetText()}): {ex.Message}", ex);
                }
                finally
                {
                    if (State == EvaluationState.PostValuation)
                    {
                        throw new InvalidOperationException("PostValuation should handle next state!");
                    }
                }
            }

            throw new InvalidOperationException("Invalid initial state! should be one of the Evaluation-states");
        }

        /// <summary>
        /// is called when a passthrough occurrs on any child object. the state will be set to done
        /// </summary>
        /// <param name="context">the evaluationcontext in which this evaluator was invoked</param>>
        /// <returns>the number of children that have been evaluated and that have values put on the value-stack</returns>
        public virtual int PassThroughOccurred(EvaluationContext context)
        {
            State = EvaluationState.Done;
            return evaluated;
        }

        /// <summary>
        /// performs the evaluation phase of this evaluator
        /// </summary>
        /// <param name="arguments">the arguments that are required for the evaluation phase</param>
        /// <returns>the result of the evaluation phase</returns>
        protected abstract object Evaluate(object[] arguments, EvaluationContext context);


        /// <summary>
        /// performs the pre-evaluation phase of this evaluator
        /// </summary>
        /// <param name="arguments">the arguments that are required for the pre-evaluation phase</param>
        /// <param name="putOnStack">indicates whether to put the generated value on stack</param>
        /// <returns>the result of the pre-evaluation phase</returns>
        protected virtual object PerformPreValuation(object[] arguments, EvaluationContext context, out bool putOnStack)
        {
            putOnStack = false;
            throw new InvalidOperationException("Do not call base from this method!");
        }

        /// <summary>
        /// performs the post-evaluation phase of this evaluator
        /// </summary>
        /// <param name="arguments">the arguments that are required for the post-evaluation phase</param>
        /// <param name="putOnStack">indicates whether to put the generated value on stack</param>
        /// <returns>the result of the post-evaluation phase</returns>
        protected virtual object PerformPostValuation(object[] arguments, EvaluationContext context, out bool putOnStack)
        {
            putOnStack = false;
            throw new InvalidOperationException("Do not call base from this method!");
        }

        /// <summary>
        /// Prepares this evaluator for the next evaluation state
        /// </summary>
        /// <param name="nextState">the next state that this evaluator will enter</param>
        /// <param name="nextChildren">the children that will be evaluated by the next evaluation iteration</param>
        protected void PrepareFor(EvaluationState nextState)
        {
            ICollection<EvaluatorBase> nextChildren = null;
            if (nextState == EvaluationState.EvaluationChildIteration)
            {
                nextChildren = Children;
            }
            else if (nextState == EvaluationState.PreValuationChildIteration)
            {
                nextChildren = PreValuationChildren;
            }
            else if (nextState == EvaluationState.PostValuationChildIteration)
            {
                nextChildren = PostValuationChildren;
            }

            if (nextChildren != null)
            {
                evaluated = 0;
                EvaluatorBase next = null;
                foreach (var child in nextChildren)
                {
                    child.Parent = this;
                    child.Next = next;
                    child.Initialize();
                    next = child;
                }
            }

            State = nextState;
        }

        /// <summary>
        /// Gets the first child of this evaluator or null if its not required
        /// </summary>
        /// <returns>the first evaluatorBase object in the execution chain of this evaluator instance</returns>
        public EvaluatorBase FirstChild()
        {
            if (State == EvaluationState.PreValuationChildIteration)
            {
                var retVal = PreValuationChildren.FirstOrDefault();
                State = EvaluationState.PreValuation;
                return retVal;
            }

            if (State == EvaluationState.EvaluationChildIteration)
            {
                var retVal = Children.FirstOrDefault();
                State = EvaluationState.Evaluation;
                return retVal;
            }

            if (State == EvaluationState.PostValuationChildIteration)
            {
                var retVal = PostValuationChildren.FirstOrDefault();
                State = EvaluationState.PostValuation;
                return retVal;
            }

            throw new InvalidOperationException("Invalid state!");

        }

        /// <summary>
        /// Evaluates the number of child-executions
        /// </summary>
        private void IncreaseEvaluatedChildren()
        {
            evaluated++;
            if (evaluated > Children.Count)
            {
                throw new InvalidOperationException("Too many child-executions detected!");
            }
        }
    }

    public enum EvaluationState
    {
        Initial,
        PreValuationChildIteration,
        PreValuation,
        EvaluationChildIteration,
        Evaluation,
        PostValuationChildIteration,
        PostValuation,
        Done
    }

    public enum ResultType
    {
        /// <summary>
        /// The value is literal and can not be assigned
        /// </summary>
        Literal,

        /// <summary>
        /// The Value is a Property and can be read and written
        /// </summary>
        PropertyOrField,

        /// <summary>
        /// The Value is a Method and can only be read
        /// </summary>
        Method,

        /// <summary>
        /// The Value is a Constructor and can only be read
        /// </summary>
        Constructor
    }

    [Flags]
    public enum AccessMode
    {
        Read = 1,
        Write = 2,
        ReadWrite = Read|Write
    }
}
