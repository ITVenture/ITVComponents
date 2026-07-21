using System;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Statements
{
    /// <summary>
    /// Eine Ausnahmebehandlung.
    /// </summary>
    /// <remarks>
    /// Fehler erreichen diesen Knoten auf zwei Wegen: als Throw-Completion aus einem
    /// Script-throw und als CLR-Ausnahme aus der Runtime. Beide werden hier gleich behandelt -
    /// der ScriptVisitor hatte dafuer zwei getrennte Zweige, die sich in drei Punkten
    /// unterschieden, jeweils zuungunsten des Script-Pfads.
    /// </remarks>
    public sealed class TryNode : StatementNode
    {
        private readonly IStatementNode tryBlock;
        private readonly string exceptionVariable;
        private readonly IStatementNode catchBlock;
        private readonly IStatementNode finallyBlock;

        public TryNode(SourcePosition position, IStatementNode tryBlock, string exceptionVariable,
            IStatementNode catchBlock, IStatementNode finallyBlock)
            : base(position)
        {
            this.tryBlock = tryBlock ?? throw new ArgumentNullException(nameof(tryBlock));
            this.exceptionVariable = exceptionVariable;
            this.catchBlock = catchBlock;
            this.finallyBlock = finallyBlock;
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            try
            {
                Completion completion = RunProtected(context);
                if (completion.Kind == CompletionKind.Throw && catchBlock != null && completion.Catchable)
                {
                    completion = RunCatch(context, completion);
                }

                return completion;
            }
            finally
            {
                RunFinally(context);
            }
        }

        private Completion RunProtected(ExecutionContext context)
        {
            try
            {
                return tryBlock.Execute(context);
            }
            catch (Exception ex)
            {
                // Eine CLR-Ausnahme aus der Runtime wird zum selben Signal wie ein
                // Script-throw, damit beide denselben catch-Zweig durchlaufen.
                return Completion.Throw(ex);
            }
        }

        private Completion RunCatch(ExecutionContext context, Completion thrown)
        {
            context.Variables.OpenInnerScope();
            try
            {
                if (exceptionVariable != null)
                {
                    context.Variables[exceptionVariable] = thrown.Thrown;
                }

                Completion completion = catchBlock.Execute(context);

                // Ein throw ohne Ausdruck wirft den urspruenglichen Fehler weiter - mitsamt
                // seiner Nutzlast. Beim ScriptVisitor ging sie im Script-Pfad verloren, weil
                // dort das ReThrow-Singleton mit Wert null nach oben gereicht wurde.
                if (completion.Kind == CompletionKind.ReThrow)
                {
                    return thrown;
                }

                // Jedes andere Ergebnis des catch-Blocks gilt. Der ScriptVisitor verwarf im
                // Script-Pfad alles ausser ReThrow - ein return aus einem catch verschwand
                // damit spurlos.
                return completion;
            }
            catch (Exception ex)
            {
                return Completion.Throw(ex);
            }
            finally
            {
                context.Variables.CollapseScope();
            }
        }

        /// <summary>
        /// Fuehrt den finally-Block aus.
        /// </summary>
        /// <remarks>
        /// Laeuft bei jedem Ausgang, auch bei return, break oder einem Fehler. Ein abruptes
        /// Ergebnis aus dem finally-Block selbst kann es nicht geben: der Erbauer laesst dort
        /// weder break noch continue noch return zu.
        /// </remarks>
        private void RunFinally(ExecutionContext context)
        {
            if (finallyBlock == null)
            {
                return;
            }

            Completion completion = finallyBlock.Execute(context);
            if (completion.Kind == CompletionKind.Throw)
            {
                // Ein Fehler im finally-Block ersetzt das bisherige Ergebnis - so wie eine
                // Ausnahme aus einem finally in C# die urspruengliche verdraengt.
                throw completion.Thrown as Exception
                      ?? new ScriptException(completion.Thrown?.ToString() ?? "Error in finally-block");
            }
        }
    }
}
