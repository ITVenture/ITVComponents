using System.Collections.Generic;

namespace ITVComponents.Workflow.Expressions
{
    /// <summary>
    /// Wertet einen Ausdruck ueber den Variablen einer Instanz aus. Kapselt die konkrete
    /// Skriptsprache, damit die Engine nicht fest an CScript haengt und in Tests ein einfacher
    /// Ersatz eingesetzt werden kann.
    /// </summary>
    public interface IExpressionEvaluator
    {
        /// <summary>
        /// Wertet einen Ausdruck aus und liefert sein Ergebnis.
        /// </summary>
        /// <param name="expression">der Ausdruck</param>
        /// <param name="variables">die Variablen, gegen die ausgewertet wird (nur lesend)</param>
        /// <returns>das Ergebnis der Auswertung</returns>
        object Evaluate(string expression, IReadOnlyDictionary<string, object> variables);

        /// <summary>
        /// Wertet einen Ausdruck als Bedingung aus.
        /// </summary>
        /// <param name="expression">der Ausdruck; soll einen booleschen Wert liefern</param>
        /// <param name="variables">die Variablen, gegen die ausgewertet wird (nur lesend)</param>
        /// <returns>das Wahrheitsergebnis</returns>
        bool EvaluateCondition(string expression, IReadOnlyDictionary<string, object> variables);
    }
}
