using ITVComponents.Scripting.CScript.Interpreter.Ast;

namespace ITVComponents.Scripting.CScript.Interpreter.Runtime
{
    /// <summary>
    /// Naht fuer den Debugger. Ist am <see cref="ExecutionContext"/> per Default null, damit
    /// die normale Ausfuehrung keinerlei Zusatzkosten traegt.
    /// </summary>
    /// <remarks>
    /// Die Ausfuehrung bleibt immer dort, wo sie ist - der Observer laeuft synchron im
    /// ausfuehrenden Thread. Fuer Remote-Debugging ueber die IPC-Schnittstelle wird nicht der
    /// Live-Zustand transportiert, sondern ein <see cref="ExecutionSnapshot"/>.
    /// </remarks>
    public interface IExecutionObserver
    {
        /// <summary>
        /// Wird vor der Ausfuehrung jeder Anweisung gerufen. Eine Implementierung, die hier
        /// blockiert, haelt das Script an - das ist der Einzelschritt-Modus.
        /// </summary>
        /// <param name="node">die Anweisung, die als naechstes ausgefuehrt wird</param>
        /// <param name="context">der aktuelle Ausfuehrungskontext</param>
        void BeforeStatement(IStatementNode node, ExecutionContext context);

        /// <summary>
        /// Wird nach der Ausfuehrung jeder Anweisung gerufen.
        /// </summary>
        /// <param name="node">die ausgefuehrte Anweisung</param>
        /// <param name="context">der aktuelle Ausfuehrungskontext</param>
        /// <param name="completion">wie die Anweisung geendet hat</param>
        void AfterStatement(IStatementNode node, ExecutionContext context, Completion completion);
    }
}
