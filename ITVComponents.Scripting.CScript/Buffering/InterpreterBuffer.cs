using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Atn;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Buffering
{
    internal static class InterpreterBuffer
    {
        /// <summary>
        /// Holds a list of runners that have alredy been initialized
        /// </summary>
        private static ConcurrentBag<RunnerItem> runners = new ConcurrentBag<RunnerItem>();

        static InterpreterBuffer()
        {
            
        }

        /// <summary>
        /// Gets an existing free repl session or creates a new one if required
        /// </summary>
        /// <param name="baseValues">the base values used to initialize the repl - session</param>
        /// <param name="scopeInitializer">a callback that is used to prepare the scope variables for the scripting</param>
        /// <param name="visitorInstance">the visitor instance that is capable to process expressions and scripts</param>
        /// <returns>a runner item that can be used to control the repl session</returns>
        public static IDisposable GetReplInstance(IDictionary<string, object> baseValues,
            InitializeScopeVariables scopeInitializer,
            out ScriptVisitor visitorInstance)
        {
            bool simpleInit = (baseValues is Scope);
            lock (runners)
            {
                RunnerItem retVal = runners.FirstOrDefault(n => !simpleInit && n.Lock());
                if (retVal == null)
                {
                    ScriptVisitor visitor = simpleInit
                        ? new ScriptVisitor((Scope) baseValues)
                        : new ScriptVisitor();
                    retVal = new RunnerItem(visitor, simpleInit);
                    retVal.Lock();
                    if (visitor.Reactivateable)
                    {
                        runners.Add(retVal);
                    }
                }

                visitorInstance = (ScriptVisitor) retVal.Visitor;
                if (!simpleInit)
                {

                    visitorInstance.ClearScope(baseValues);
                }

                InitializeScopeVariables dff = scopeInitializer ??
                                               (a =>
                                               {
                                                   DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession);
                                               });
                visitorInstance.Prepare(dff);
                return retVal;
            }
        }

        /// <summary>
        /// Gets an existing free repl session or creates a new one if required
        /// </summary>
        /// <param name="baseValues">the base values used to initialize the repl - session</param>
        /// <param name="scopeInitializer">a callback that is used to prepare the scope variables for the scripting</param>
        /// <param name="visitorInstance">the visitor instance that is capable to process expressions and scripts</param>
        /// <returns>a runner item that can be used to control the repl session</returns>
        public static IDisposable GetReplInstance(object implicitContext,
            InitializeScopeVariables scopeInitializer,
            out ScriptVisitor visitorInstance)
        {
            Scope s = new Scope(new Dictionary<string, object> { { "$data", implicitContext } });
            s.ImplicitContext = "$data";
            return GetReplInstance(s, scopeInitializer, out visitorInstance);
        }

        /// <summary>
        /// Gets the RunnerItem of the specified interpreter session
        /// </summary>
        /// <param name="interpreterSession">the current interpreter session</param>
        /// <returns>a scriptvisitor that is capable for running a script</returns>
        public static ITVScriptingBaseVisitor<ScriptValue> GetInterpreter(IDisposable interpreterSession)
        {
            if (!(interpreterSession is RunnerItem))
            {
                throw new InvalidOperationException("Interpreter session expected!");
            }

            return ((RunnerItem) interpreterSession).Visitor;
        }

        /// <summary>
        /// Runner controller item
        /// </summary>
        internal class RunnerItem : IDisposable
        {
            /// <summary>
            /// Avoids concurrent access on this runnerItem
            /// </summary>
            private object innerLock = new object();

            /// <summary>
            /// Indicates whether this runnerItem is currently free
            /// </summary>
            private bool available;

            private bool isSimpleInit;

            /// <summary>
            /// Initializes a new instance of the RunnerItem class
            /// </summary>
            /// <param name="visitor"></param>
            public RunnerItem(ScriptVisitor visitor, bool isSimpleInit)
            {
                Visitor = visitor;
                visitor.Context = this;
                available = true;
                this.isSimpleInit = isSimpleInit;
            }

            /// <summary>
            /// The ScriptVisitor that is attached to this runner-session
            /// </summary>
            public ITVScriptingBaseVisitor<ScriptValue> Visitor { get; private set; }

            /// <summary>
            /// Acquires a lock for this runner item so that no other repl-session can use the same instance
            /// </summary>
            /// <returns></returns>
            public bool Lock()
            {
                lock (innerLock)
                {
                    //GarbageControl.CurrentCrypt.Use();
                    bool retVal = available;
                    if (retVal)
                    {
                        available = false;
                    }

                    return retVal;
                }
            }

            /// <summary>
            /// Frees this runner instance
            /// </summary>
            public void Dispose()
            {
                lock (innerLock)
                {
                    //GarbageControl.CurrentCrypt.Release();
                    if (!isSimpleInit)
                    {
                        ((ScriptVisitor) Visitor).ClearScope(null);
                    }

                    available = true;
                }
            }
        }
    }
}
