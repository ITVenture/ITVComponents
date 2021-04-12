using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security;
using System.Threading;
using ITVComponents.Scripting.CScript;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;

namespace ITVComponents.Decisions.DefaultConstraints
{
    /// <summary>
    /// Acceptance constraint class that uses scripting in order to verify the acceptance of provided data. The used Expressions or Scripts must result to a boolean value.
    /// </summary>
    /// <typeparam name="T">the Type for which this constraint can be used</typeparam>
    public class ScriptedConstraint<T>:IConstraint<T> where T:class
    {
        /// <summary>
        /// The Acceptance Expression to run in order to verify the acceptance of specific data
        /// </summary>
        private readonly string expression;

        /// <summary>
        /// The acceptance mode to be used for the provided expression
        /// </summary>
        private readonly ConstraintExpressionMode mode;

        /// <summary>
        /// holds information about what this constraint examines
        /// </summary>
        private readonly string constraintInformation;

        /// <summary>
        /// further variables to be used with the acceptance script or expression
        /// </summary>
        private IDictionary<string, object> variables;

        /// <summary>
        /// the parent of this constraint
        /// </summary>
        private IDecider<T> parent;

        /// <summary>
        /// the last message that was created by a derived constraint
        /// </summary>
       [NonSerialized]
        private ThreadLocal<string> lastMessage = new ThreadLocal<string>();

        /// <summary>
        /// Initializes a new instance of the ScriptedAcceptanceConstraint class
        /// </summary>
        /// <param name="expression">the expression that is used to verify the acceptance of a specific piece of data</param>
        /// <param name="mode">the expressionmode of the given expression</param>
        public ScriptedConstraint(string expression, ConstraintExpressionMode mode):this(expression,mode,$"Processing provided data using the following Expression: ({expression})")
        {    
        }

        /// <summary>
        /// Initializes a new instance of the ScriptedAcceptanceConstraint class
        /// </summary>
        /// <param name="expression">the expression that is used to verify the acceptance of a specific piece of data</param>
        /// <param name="mode">the expressionmode of the given expression</param>
        /// <param name="additionalVariables">additional variables that are provided to ensure the ability to verify a constraint</param>
        public ScriptedConstraint(string expression, ConstraintExpressionMode mode,
            IDictionary<string, object> additionalVariables) : this(expression, mode, $"Processing provided data using the following Expression: ({expression})")
        {
            variables = additionalVariables;
        }

        /// <summary>
        /// Initializes a new instance of the ScriptedAcceptanceConstraint class
        /// </summary>
        /// <param name="expression">the expression that is used to verify the acceptance of a specific piece of data</param>
        /// <param name="mode">the expressionmode of the given expression</param>
        /// <param name="additionalVariables">additional variables that are provided to ensure the ability to verify a constraint</param>
        /// <param name="constraintInformation">additional information about what this constraint examines</param>
        public ScriptedConstraint(string expression, ConstraintExpressionMode mode,
            IDictionary<string, object> additionalVariables, string constraintInformation) : this(expression, mode,
            constraintInformation)
        {
            variables = additionalVariables;
        }

        /// <summary>
        /// Initializes a new instance of the ScriptedAcceptanceConstraint class
        /// </summary>
        /// <param name="expression">the expression that is used to verify the acceptance of a specific piece of data</param>
        /// <param name="mode">the expressionmode of the given expression</param>
        /// <param name="constraintInformation">additional information about what this constraint examines</param>
        public ScriptedConstraint(string expression, ConstraintExpressionMode mode, string constraintInformation)
        {
            this.expression = expression;
            this.mode = mode;
            this.constraintInformation = constraintInformation;
        }

        /// <summary>
        /// Gets or sets the last message that was created by a derived constraint
        /// </summary>
        protected string LastMessage
        {
            get { return lastMessage.Value; }
            set { lastMessage.Value = value; }
        }

        #region Implementation of IAcceptanceConstraint<T>

        /// <summary>
        /// Sets the Parent of this Constraint
        /// </summary>
        /// <param name="parent">the new parent of this constraint</param>
        public void SetParent(IDecider<T> parent)
        {
            if (this.parent != null)
            {
                throw new InvalidOperationException("SetParent can only be called once!");
            }

            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            this.parent = parent;
        }

        /// <summary>
        /// Verifies the provided input
        /// </summary>
        /// <param name="data">the data that was provided by a source</param>
        /// <param name="message">the message that was generated during the validation of this constraint</param>
        /// <returns>a value indicating whether the data fullfills the requirements of the underlaying Requestor</returns>
        public virtual DecisionResult Verify(T data, out string message)
        {
            Scope varScope;
            IDisposable evaluationContext = GetEvaluationContext(out varScope);
            varScope["data"] = data;
            PrepareVariables(varScope);
            message = null;
            try
            {
                DecisionResult retVal = DecisionResult.None;
                switch (mode)
                {
                    case ConstraintExpressionMode.Single:
                    {
                        object tmp = ExpressionParser.Parse(expression, evaluationContext);
                        if (tmp is bool)
                        {
                            retVal = (bool) tmp ? DecisionResult.Success : DecisionResult.Fail;
                        }
                        else
                        {
                            retVal = (DecisionResult)tmp;
                        }

                        if (lastMessage.IsValueCreated && lastMessage.Value != null)
                        {
                            message = lastMessage.Value;
                            lastMessage.Value = null;
                        }
                        break;
                    }
                    case ConstraintExpressionMode.Script:
                    {
                        object tmp = ExpressionParser.ParseBlock(expression, evaluationContext);
                        if (tmp is bool)
                        {
                            retVal = (bool) tmp ? DecisionResult.Success : DecisionResult.Fail;
                        }
                        else
                        {
                            retVal = (DecisionResult) tmp;
                        }

                        message = varScope["message"] as string;
                        break;
                    }
                    case ConstraintExpressionMode.ScriptFile:
                    {
                        ScriptFile<object> script = ScriptFile<object>.FromFile(expression);
                        object tmp = script.Execute(evaluationContext);
                        if (tmp is bool)
                        {
                            retVal = (bool) tmp ? DecisionResult.Success : DecisionResult.Fail;
                        }
                        else
                        {
                            retVal = (DecisionResult) tmp;
                        }

                        message = varScope["message"] as string;
                        break;
                    }
                }

                return retVal;
            }
            finally
            {
                CloseEvaluationContext(evaluationContext);
            }
        }

        /// <summary>
        /// Sets the Parent of this Constraint
        /// </summary>
        /// <param name="parent">the new parent of this constraint</param>
        void IConstraint.SetParent(IDecider parent)
        {
            SetParent((IDecider<T>) parent);
        }

        /// <summary>
        /// Verifies the provided input
        /// </summary>
        /// <param name="data">the data that was provided by a source</param>
        /// <param name="message">the message that was generated during the validation of this constraint</param>
        /// <returns>a value indicating whether the data fullfills the requirements of the underlaying Requestor</returns>
        DecisionResult IConstraint.Verify(object data, out string message)
        {
            return Verify((T) data, out message);
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return constraintInformation;
        }

        /// <summary>
        /// Closes the current Evaluationcontext if the parent decider is not context-driven
        /// </summary>
        /// <param name="context">the repl-context of the this constraint</param>
        protected void CloseEvaluationContext(IDisposable context)
        {
            if (!parent.IsContextDriven)
            {
                context.Dispose();
            }
        }

        /// <summary>
        /// Gets the Evaluationcontext for the current evaluation session
        /// </summary>
        /// <param name="varScope">The variablescope for this decider</param>
        /// <returns>a repl-session that can be used for processing this constraint</returns>
        protected IDisposable GetEvaluationContext(out Scope varScope)
        {
            IDisposable retVal;
            if (!parent.IsContextDriven || parent.Context.GetValueFor(this, "Variables") == null)
            {
                varScope = variables == null ? new Scope() : new Scope(variables);
                retVal = ExpressionParser.BeginRepl(varScope, a => DefaultCallbacks.PrepareDefaultCallbacks(a.Scope,a.ReplSession));
                if (parent.IsContextDriven)
                {
                    parent.Context.SetValueFor(this, "Variables", varScope);
                    parent.Context.SetValueFor(this, "EVContext", retVal);
                }
            }
            else
            {
                varScope = (Scope)parent.Context.GetValueFor(this, "Variables");
                retVal = (IDisposable)parent.Context.GetValueFor(this, "EVContext");
            }

            return retVal;
        }

        /// <summary>
        /// Prepares this variablescope. Override this method if you want to provide additional information of functions
        /// </summary>
        /// <param name="scope">the variablescope that is passed to the scripting engine when running a decision</param>
        protected virtual void PrepareVariables(Scope scope)
        {
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            lastMessage = new ThreadLocal<string>();
        }

        #endregion
    }

    /// <summary>
    /// Expressionmodes of a constraint
    /// </summary>
    public enum ConstraintExpressionMode
    {
        /// <summary>
        /// The Constraint uses a single expression to verify the acceptance of the provided data
        /// </summary>
        Single,

        /// <summary>
        /// The constraint uses a Code-Block to verify the acceptance of the provided data. The Block must end with a return statement.
        /// </summary>
        Script,

        /// <summary>
        /// The constraint uses a Script-File to verify the acceptance of the provided data. The Script must end with a return statement
        /// </summary>
        ScriptFile
    }
}
