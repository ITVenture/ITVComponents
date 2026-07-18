using System;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Interpreter.Ast;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Interpreter
{
    /// <summary>
    /// Ein uebersetztes Script: die unveraenderliche Wurzel eines Ausfuehrungsbaums.
    /// </summary>
    /// <remarks>
    /// Weil der Baum unveraenderlich ist und jeder Lauf seinen eigenen ExecutionContext
    /// bekommt, kann dieselbe Instanz gleichzeitig von mehreren Threads ausgefuehrt werden.
    /// Genau deshalb braucht es den Pool aus InterpreterBuffer/RunnerItem nicht mehr.
    /// </remarks>
    public sealed class CompiledScript
    {
        private readonly IExpressionNode root;

        internal CompiledScript(IExpressionNode root)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>
        /// Fuehrt das Script aus.
        /// </summary>
        /// <param name="variables">der Variablen-Scope</param>
        /// <param name="policy">die geltende Sicherheits-Policy, oder null fuer die des Scopes</param>
        /// <param name="observer">ein Debugger-Hook, oder null</param>
        /// <returns>das Ergebnis der Auswertung</returns>
        public object Execute(IScope variables, ScriptingPolicy policy = null, IExecutionObserver observer = null)
        {
            var context = new ExecutionContext(variables, policy) { Observer = observer };
            return root.Evaluate(context).GetValue(null, context.Policy);
        }

        /// <summary>
        /// Fuehrt das Script in einem eigens aufgebauten Scope aus.
        /// </summary>
        /// <param name="variables">die Startvariablen</param>
        /// <param name="scopeInitializer">Initialisierer fuer den Scope, oder null</param>
        /// <param name="policy">die geltende Sicherheits-Policy, oder null</param>
        /// <returns>das Ergebnis der Auswertung</returns>
        /// <remarks>
        /// Der Initialisierer bekommt vorerst weder Repl-Session noch Visitor:
        /// ScopePreparationCallbackArguments verlangt eine ScriptVisitor-Instanz, an die der
        /// Interpreter bewusst nicht gebunden ist. Initialisierer, die nur den Scope
        /// bestuecken, funktionieren damit; alles Weitere wird mit der Repl-Anbindung in
        /// einer spaeteren Phase nachgezogen.
        /// </remarks>
        public object Execute(System.Collections.Generic.IDictionary<string, object> variables,
            InitializeScopeVariables scopeInitializer = null, ScriptingPolicy policy = null)
        {
            var scope = new Scope(variables, policy);
            scopeInitializer?.Invoke(new ScopePreparationCallbackArguments(scope, null, null));
            return Execute(scope, policy);
        }
    }
}
