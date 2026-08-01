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
    /// Die Instanz-Variablen bilden den Scope. Ausgewertet wird <b>immer</b> gegen eine KOPIE davon -
    /// nicht nur bei Bedingungen: ein Ausdruck darf den Instanzzustand nicht veraendern. Das ist keine
    /// Formalie, sondern der Grund, warum eine Hilfszuweisung erlaubt ist. <c>x = {A: 1}</c> ist die
    /// uebliche Schreibweise fuer ein Objekt-Literal im Ausdrucks-Modus (am ANFANG lehnt der Parser die
    /// geschweifte Klammer ab, weil dort ein Block stuende) - und ohne die Kopie truege die Instanz
    /// danach eine Variable <c>x</c> mit sich herum, persistiert und fuer jede spaetere Bedingung
    /// sichtbar.
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
            => Evaluate(expression, variables, ScriptMode.Expression);

        /// <inheritdoc/>
        public object Evaluate(string expression, IReadOnlyDictionary<string, object> variables, ScriptMode mode)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new ArgumentException("Expression must not be empty.", nameof(expression));
            }

            // Zwei verschiedene Einstiege des Interpreters, nicht zwei Geschmacksrichtungen desselben:
            // Parse wertet EINEN Ausdruck aus (und ignoriert stillschweigend, was danach kommt),
            // ParseBlock ein ganzes Skript - dessen Ergebnis ist ohne 'return' null.
            return mode == ScriptMode.Block
                ? ScriptInterpreter.ParseBlock(expression, Copy(variables), policy: policy)
                : ScriptInterpreter.Parse(expression, Copy(variables), policy: policy);
        }

        /// <inheritdoc/>
        public bool EvaluateCondition(string expression, IReadOnlyDictionary<string, object> variables)
            => EvaluateCondition(expression, variables, ScriptMode.Expression);

        /// <inheritdoc/>
        public bool EvaluateCondition(string expression, IReadOnlyDictionary<string, object> variables,
            ScriptMode mode)
        {
            object result = Evaluate(expression, variables, mode);
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
