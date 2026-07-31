using System.Collections.Generic;

namespace ITVComponents.Workflow.Expressions
{
    /// <summary>
    /// Wie ein Skript-Text zu lesen ist. Der Unterschied ist NICHT kosmetisch: ein Block ohne
    /// <c>return</c> liefert null, und ein Block, der als Ausdruck ausgewertet wird, liefert
    /// stillschweigend einen falschen Wert statt eines Fehlers. Deshalb steht der Modus explizit an
    /// jedem Feld und wird nicht geraten.
    /// </summary>
    public enum ScriptMode
    {
        /// <summary>
        /// EIN Ausdruck, dessen Wert das Ergebnis ist (<c>amount &gt; 100</c>). Der Standard - so
        /// verhalten sich alle Definitionen, die es vor dem Schalter gab.
        /// </summary>
        Expression,

        /// <summary>
        /// Ein ganzes Skript mit Anweisungen und <b>explizitem <c>return</c></b>
        /// (<c>ts = 'System.TimeSpan'; return new ts(0,0,10);</c>). <b>Ohne <c>return</c> ist das
        /// Ergebnis null.</b>
        /// </summary>
        Block
    }

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
        /// Wertet einen Ausdruck aus und liefert sein Ergebnis.
        /// </summary>
        /// <param name="expression">der Ausdruck bzw. das Skript</param>
        /// <param name="variables">die Variablen, gegen die ausgewertet wird (nur lesend)</param>
        /// <param name="mode">wie der Text zu lesen ist</param>
        /// <returns>das Ergebnis der Auswertung</returns>
        /// <remarks>
        /// Als Standard-Implementierung auf <see cref="Evaluate(string,IReadOnlyDictionary{string,object})"/>
        /// abgebildet: ein bestehender Auswerter bleibt damit uebersetzbar und verhaelt sich wie bisher
        /// (also immer als Ausdruck). Wer <see cref="ScriptMode.Block"/> unterstuetzen will, ueberschreibt.
        /// </remarks>
        object Evaluate(string expression, IReadOnlyDictionary<string, object> variables, ScriptMode mode)
            => Evaluate(expression, variables);

        /// <summary>
        /// Wertet einen Ausdruck als Bedingung aus.
        /// </summary>
        /// <param name="expression">der Ausdruck; soll einen booleschen Wert liefern</param>
        /// <param name="variables">die Variablen, gegen die ausgewertet wird (nur lesend)</param>
        /// <returns>das Wahrheitsergebnis</returns>
        bool EvaluateCondition(string expression, IReadOnlyDictionary<string, object> variables);

        /// <summary>
        /// Wertet einen Ausdruck als Bedingung aus.
        /// </summary>
        /// <param name="expression">der Ausdruck; soll einen booleschen Wert liefern</param>
        /// <param name="variables">die Variablen, gegen die ausgewertet wird (nur lesend)</param>
        /// <param name="mode">wie der Text zu lesen ist</param>
        /// <returns>das Wahrheitsergebnis</returns>
        bool EvaluateCondition(string expression, IReadOnlyDictionary<string, object> variables, ScriptMode mode)
            => EvaluateCondition(expression, variables);
    }
}
