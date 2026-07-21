using ITVComponents.Scripting.CScript.Interpreter.Runtime;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast
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
