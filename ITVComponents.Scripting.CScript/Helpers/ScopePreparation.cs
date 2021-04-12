using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;

namespace ITVComponents.Scripting.CScript.Helpers
{
    public class ScopePreparationCallbackArguments
    {
        private readonly IScope scope;
        private readonly IDisposable replSession;
        private readonly ScriptVisitor visitor;

        /// <summary>
        /// Initializes a new instance of the ScopePreparationCallbackArguments class
        /// </summary>
        /// <param name="scope">the variable scope that contains all current variables</param>
        /// <param name="replSession">a repl-session that is provides access to the current interpreter</param>
        /// <param name="visitor">the visitor instance that is used to interpret some code</param>
        public ScopePreparationCallbackArguments(IScope scope, IDisposable replSession, ScriptVisitor visitor)
        {
            this.scope = scope;
            this.replSession = replSession;
            this.visitor = visitor;
        }

        /// <summary>
        /// Gets the variableScope of the current repl session
        /// </summary>
        public IScope Scope { get { return scope;} }

        /// <summary>
        /// Gets the current repl session
        /// </summary>
        public IDisposable ReplSession { get {return replSession; } }

        /// <summary>
        /// Gets the visitor instance that is used to interpret some code
        /// </summary>
        public ScriptVisitor Visitor
        {
            get { return visitor; }
        }
    }

    public delegate void InitializeScopeVariables(ScopePreparationCallbackArguments args);
}
