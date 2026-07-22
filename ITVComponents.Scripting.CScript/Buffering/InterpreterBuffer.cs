using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Buffering
{
    /// <summary>
    /// Baut Repl-Sitzungen fuer den Interpreter.
    /// </summary>
    /// <remarks>
    /// Eine Sitzung ist nur noch ein Scope samt Policy (<see cref="ReplSession"/>). Der fruehere
    /// Aufbau - ein Pool aus <c>ScriptVisitor</c>-Instanzen (RunnerItem) mit Lock, weil eine
    /// Visitor-Instanz ihren Laufzustand auf sich selbst trug und darum nicht nebenlaeufig
    /// wiederverwendbar war - entfaellt ersatzlos: der Interpreter fuehrt gegen den Scope aus,
    /// sein Ausfuehrungsbaum ist unveraenderlich, und jeder Lauf bekommt ohnehin seinen eigenen
    /// ExecutionContext. Damit braucht es keinen wiederverwendbaren Zustandstraeger und keinen
    /// Pool mehr; das Erzeugen einer Sitzung ist ein blosses Einpacken des Scopes.
    /// </remarks>
    internal static class InterpreterBuffer
    {
        /// <summary>
        /// Beginnt eine Repl-Sitzung ueber die angegebenen Startwerte.
        /// </summary>
        public static IDisposable GetReplInstance(IDictionary<string, object> baseValues,
            InitializeScopeVariables scopeInitializer)
        {
            return GetReplInstance(baseValues, scopeInitializer, ScriptingPolicy.Default);
        }

        /// <summary>
        /// Beginnt eine Repl-Sitzung ueber die angegebenen Startwerte unter einer Policy.
        /// </summary>
        /// <remarks>
        /// Ist <paramref name="baseValues"/> bereits ein Scope, wird er unmittelbar zur Sitzung;
        /// sonst wird ein neuer Scope aus den Werten aufgebaut. Das entspricht der frueheren
        /// Fallunterscheidung simpleInit/ClearScope des ScriptVisitors.
        /// </remarks>
        public static IDisposable GetReplInstance(IDictionary<string, object> baseValues,
            InitializeScopeVariables scopeInitializer, ScriptingPolicy policy)
        {
            IScope scope = baseValues as IScope
                           ?? new Scope(baseValues ?? new Dictionary<string, object>(), policy);
            return CreateSession(scope, scopeInitializer, policy);
        }

        /// <summary>
        /// Beginnt eine Repl-Sitzung mit einem impliziten Kontext ($data).
        /// </summary>
        public static IDisposable GetReplInstance(object implicitContext,
            InitializeScopeVariables scopeInitializer)
        {
            return GetReplInstance(implicitContext, scopeInitializer, ScriptingPolicy.Default);
        }

        /// <summary>
        /// Beginnt eine Repl-Sitzung mit einem impliziten Kontext ($data) unter einer Policy.
        /// </summary>
        public static IDisposable GetReplInstance(object implicitContext,
            InitializeScopeVariables scopeInitializer, ScriptingPolicy policy)
        {
            var scope = new Scope(new Dictionary<string, object> { { "$data", implicitContext } }, policy);
            scope.ImplicitContext = "$data";
            return CreateSession(scope, scopeInitializer, policy);
        }

        /// <summary>
        /// Packt den vorbereiteten Scope in eine Sitzung und laesst den Initialisierer laufen.
        /// </summary>
        /// <remarks>
        /// Der Initialisierer bekommt die Sitzung selbst als <c>ReplSession</c> (fuer die
        /// Standard-Rueckrufe, die sie als Fixture <c>session</c> im Scope hinterlegen) und
        /// bewusst keinen Visitor - kein Aufrufer liest ihn, seit der Interpreter ausfuehrt.
        /// </remarks>
        private static IDisposable CreateSession(IScope scope, InitializeScopeVariables scopeInitializer,
            ScriptingPolicy policy)
        {
            scope.OverridePolicy(policy);
            var session = new ReplSession(scope, policy);
            InitializeScopeVariables init = scopeInitializer ??
                (a => DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession));
            init(new ScopePreparationCallbackArguments(scope, session));
            return session;
        }

        /// <summary>
        /// Eine Repl-Sitzung: der Scope, gegen den der Interpreter ausfuehrt, samt Policy.
        /// </summary>
        internal sealed class ReplSession : IDisposable
        {
            public ReplSession(IScope scope, ScriptingPolicy policy)
            {
                Scope = scope;
                Policy = policy;
            }

            /// <summary>
            /// Der Scope, der die Sitzung ist. Aufeinanderfolgende Auswertungen sehen die
            /// Variablen der jeweils vorigen.
            /// </summary>
            public IScope Scope { get; }

            /// <summary>
            /// Die Policy, unter der in dieser Sitzung ausgefuehrt wird.
            /// </summary>
            public ScriptingPolicy Policy { get; }

            /// <summary>
            /// Nichts freizugeben: keine gepoolte Instanz, kein Lock. Bleibt IDisposable, weil
            /// die Aufrufer die Sitzung in using(...) fuehren.
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}
