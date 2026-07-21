using System;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Exceptions;
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
        private readonly IExpressionNode expressionRoot;
        private readonly IStatementNode statementRoot;

        internal CompiledScript(IExpressionNode root)
        {
            this.expressionRoot = root ?? throw new ArgumentNullException(nameof(root));
        }

        internal CompiledScript(IStatementNode root)
        {
            this.statementRoot = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>
        /// Fuehrt das Script aus.
        /// </summary>
        /// <param name="variables">der Variablen-Scope</param>
        /// <param name="policy">die geltende Sicherheits-Policy, oder null fuer die des Scopes</param>
        /// <param name="observer">ein Debugger-Hook, oder null</param>
        /// <returns>
        /// bei einem Ausdruck dessen Wert; bei einem Programm der Wert eines return, sonst null.
        /// </returns>
        public object Execute(IScope variables, ScriptingPolicy policy = null, IExecutionObserver observer = null)
        {
            var context = new ExecutionContext(variables, policy) { Observer = observer };
            if (expressionRoot != null)
            {
                return expressionRoot.Evaluate(context).GetValue(null, context.Policy);
            }

            return Unwrap(statementRoot.Execute(context), context);
        }

        /// <summary>
        /// Macht aus dem Ergebnis eines Programms den Rueckgabewert.
        /// </summary>
        /// <remarks>
        /// Ein Programm liefert nur ueber return einen Wert - laeuft es einfach aus, ist das
        /// Ergebnis null. Ein Fehler wird hier zur CLR-Ausnahme: an dieser Grenze endet die
        /// Signalisierung ueber Completion.
        /// </remarks>
        private static object Unwrap(Completion completion, ExecutionContext context)
        {
            switch (completion.Kind)
            {
                case CompletionKind.Return:
                    return completion.Value?.GetValue(null, context.Policy);
                case CompletionKind.Throw:
                    throw AsException(completion);
                case CompletionKind.ReThrow:
                    // Kann nur auftreten, wenn ein throw ohne Ausdruck ausserhalb eines catch
                    // steht - das lehnt der Erbauer bereits ab.
                    throw new ScriptException("Illegal Re-Throw statement found!");
                case CompletionKind.Break:
                case CompletionKind.Continue:
                    // Der Erbauer laesst break und continue nur innerhalb von Schleifen und
                    // switch zu; hier anzukommen hiesse, dass diese Pruefung eine Luecke hat.
                    throw new ScriptException($"Unexpected {completion.Kind} at script level");
                default:
                    return null;
            }
        }

        private static Exception AsException(Completion completion)
        {
            switch (completion.Thrown)
            {
                case ScriptException scriptException:
                    return scriptException;
                case Exception exception:
                    return new ScriptException("Error while executing Script", exception);
                default:
                    return new ScriptException(completion.Thrown?.ToString() ?? "Error while executing Script");
            }
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
