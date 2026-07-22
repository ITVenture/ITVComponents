using ITVComponents.Scripting.CScript.Runtime;

namespace ITVComponents.Scripting.CScript.Ast
{
    /// <summary>
    /// Basisklasse der Anweisungs-Knoten.
    /// </summary>
    public abstract class StatementNode : IStatementNode
    {
        protected StatementNode(SourcePosition position)
        {
            Position = position;
        }

        /// <inheritdoc/>
        public SourcePosition Position { get; }

        /// <inheritdoc/>
        public abstract Completion Execute(ExecutionContext context);
    }
}
