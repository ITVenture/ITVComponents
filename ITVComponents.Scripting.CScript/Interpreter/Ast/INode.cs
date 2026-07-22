using ITVComponents.Scripting.CScript.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Ast
{
    /// <summary>
    /// Basis aller Knoten des Ausfuehrungsbaums. Knoten sind nach dem Bauen unveraenderlich
    /// (abgesehen vom Inline-Cache) und koennen daher von mehreren Threads gleichzeitig
    /// ausgefuehrt werden. Der veraenderliche Zustand einer Ausfuehrung liegt komplett im
    /// <see cref="ExecutionContext"/>.
    /// </summary>
    public interface INode
    {
        /// <summary>
        /// Position im Quelltext. Grundlage fuer Breakpoints und Fehlermeldungen.
        /// </summary>
        SourcePosition Position { get; }
    }

    /// <summary>
    /// Ein Knoten, der zu einem Wert ausgewertet werden kann. Ausdruecke werden atomar
    /// rekursiv ausgewertet - die Schrittgranularitaet des Debuggers liegt bewusst auf
    /// Statement-Ebene.
    /// </summary>
    public interface IExpressionNode : INode
    {
        /// <summary>
        /// Wertet diesen Ausdruck aus.
        /// </summary>
        /// <param name="context">der Ausfuehrungskontext</param>
        /// <returns>
        /// der Wert des Ausdrucks als <see cref="ScriptValue"/>. Bewusst kein object:
        /// nur so bleiben L-Value/R-Value-Unterscheidung, Overload-Aufloesung, ref-Writeback
        /// und die Security-Pruefungen der bestehenden Runtime erhalten.
        /// </returns>
        ScriptValue Evaluate(ExecutionContext context);
    }

    /// <summary>
    /// Ein Knoten, der eine Anweisung darstellt.
    /// </summary>
    public interface IStatementNode : INode
    {
        /// <summary>
        /// Fuehrt diese Anweisung aus.
        /// </summary>
        /// <param name="context">der Ausfuehrungskontext</param>
        /// <returns>
        /// wie die Ausfuehrung endete - normal oder ueber break/continue/return/throw.
        /// Ersetzt die Sentinel-Rueckgabewerte (IPassThroughValue) des ScriptVisitors.
        /// </returns>
        Completion Execute(ExecutionContext context);
    }
}
