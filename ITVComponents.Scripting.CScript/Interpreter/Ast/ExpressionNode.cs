using ITVComponents.Scripting.CScript.Runtime;
using ITVComponents.Scripting.CScript.Optimization;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Ast
{
    /// <summary>
    /// Basisklasse der Ausdrucks-Knoten. Traegt die Quellposition und den Inline-Cache.
    /// </summary>
    /// <remarks>
    /// Beim ScriptVisitor sass der Cache-Slot im ANTLR-Kontextobjekt: die Parser-Kontexte
    /// implementierten IScriptSymbol (siehe Core/ITVScriptingParserExtensions.cs). Da der
    /// Interpreter eigene Knotenobjekte hat, gehoert der Slot an den Knoten - der Parse-Baum
    /// wird nach dem Bauen nicht mehr gebraucht.
    ///
    /// Der Cache ist hier bewusst intrinsisch statt optional: der aufgeloeste Executor wird
    /// ohne Sperre geschrieben. Die Aufloesung ist idempotent, konkurrierende Schreiber
    /// ermitteln dasselbe Ergebnis - der letzte gewinnt, und ein verlorener Schreibvorgang
    /// kostet nur eine weitere Aufloesung.
    /// </remarks>
    public abstract class ExpressionNode : IExpressionNode, IScriptSymbol
    {
        private IExecutor preferredExecutor;

        protected ExpressionNode(SourcePosition position)
        {
            Position = position;
        }

        /// <inheritdoc/>
        public SourcePosition Position { get; }

        /// <inheritdoc/>
        public abstract ScriptValue Evaluate(ExecutionContext context);

        /// <inheritdoc/>
        public void SetPreferredExecutor(IExecutor executor)
        {
            preferredExecutor = executor;
        }

        /// <inheritdoc/>
        public object InvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck,
            out bool success)
        {
            IExecutor executor = preferredExecutor;
            if (executor != null && (bypassCompatibilityCheck || executor.CanExecute(value, arguments)))
            {
                success = true;
                return executor.Invoke(value, arguments);
            }

            success = false;
            return null;
        }

        /// <inheritdoc/>
        public bool CanInvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck)
        {
            IExecutor executor = preferredExecutor;
            return executor != null && (bypassCompatibilityCheck || executor.CanExecute(value, arguments));
        }

        /// <summary>
        /// Gibt dieses Symbol als Cache-Traeger zurueck, wenn der Inline-Cache aktiv ist.
        /// Ohne aktives LazyInvokation bekommen die ScriptValues null - genau wie beim
        /// ScriptVisitor, der `lazyInvokation ? context : null` uebergab.
        /// </summary>
        protected IScriptSymbol CacheSlot(ExecutionContext context)
        {
            return context.LazyInvokation ? this : null;
        }

        /// <summary>
        /// Verpackt ein Ergebnis als Literalwert.
        /// </summary>
        protected ScriptValue Literal(ExecutionContext context, object value)
        {
            var retVal = new LiteralScriptValue(context.BypassCompatibilityOnLazyInvokation);
            retVal.Initialize(value);
            return retVal;
        }
    }
}
