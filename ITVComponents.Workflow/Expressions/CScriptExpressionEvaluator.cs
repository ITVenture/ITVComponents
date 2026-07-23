using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Workflow.Expressions
{
    /// <summary>
    /// Wertet Ausdruecke mit dem CScript-Interpreter aus. Standard-Implementierung von
    /// <see cref="IExpressionEvaluator"/>.
    /// </summary>
    /// <remarks>
    /// Die Instanz-Variablen bilden den Scope. Bedingungen werden bewusst gegen eine Kopie der
    /// Variablen ausgewertet, damit eine Bedingung den Instanzzustand nicht versehentlich
    /// veraendert.
    /// </remarks>
    public class CScriptExpressionEvaluator : IExpressionEvaluator
    {
        private readonly ScriptingPolicy policy;

        /// <summary>
        /// Initialisiert den Auswerter.
        /// </summary>
        /// <param name="policy">die Sicherheits-Policy fuer die Ausdruecke, oder null fuer die Standard-Policy</param>
        public CScriptExpressionEvaluator(ScriptingPolicy policy = null)
        {
            this.policy = policy;
        }

        /// <inheritdoc/>
        public object Evaluate(string expression, IReadOnlyDictionary<string, object> variables)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new ArgumentException("Expression must not be empty.", nameof(expression));
            }

            return ScriptInterpreter.Parse(expression, Copy(variables), policy: policy);
        }

        /// <inheritdoc/>
        public bool EvaluateCondition(string expression, IReadOnlyDictionary<string, object> variables)
        {
            object result = Evaluate(expression, variables);
            switch (result)
            {
                case bool b:
                    return b;
                case null:
                    return false;
                default:
                    throw new InvalidOperationException(
                        $"Condition '{expression}' yielded '{result}' ({result.GetType().Name}); a boolean was expected.");
            }
        }

        private static Dictionary<string, object> Copy(IReadOnlyDictionary<string, object> variables)
        {
            var copy = new Dictionary<string, object>();
            if (variables != null)
            {
                foreach (KeyValuePair<string, object> pair in variables)
                {
                    copy[pair.Key] = pair.Value;
                }
            }

            return copy;
        }
    }
}
