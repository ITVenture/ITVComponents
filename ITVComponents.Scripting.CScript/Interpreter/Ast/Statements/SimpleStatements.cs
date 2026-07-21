using System;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Statements
{
    /// <summary>
    /// Ein Ausdruck in Anweisungsposition. Der Wert wird verworfen.
    /// </summary>
    public sealed class ExpressionStatementNode : StatementNode
    {
        private readonly IExpressionNode expression;

        public ExpressionStatementNode(SourcePosition position, IExpressionNode expression)
            : base(position)
        {
            this.expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        /// <summary>
        /// Der auszuwertende Ausdruck.
        /// </summary>
        public IExpressionNode Expression => expression;

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            // GetValue erzwingt die Auswertung: ohne das bliebe ein Aufruf wie "doIt();" ein
            // ungelesener MemberAccessValue und faende nie statt.
            expression.Evaluate(context).GetValue(null, context.Policy);
            return Completion.Normal;
        }
    }

    /// <summary>
    /// Eine leere Anweisung.
    /// </summary>
    public sealed class EmptyStatementNode : StatementNode
    {
        public EmptyStatementNode(SourcePosition position)
            : base(position)
        {
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            return Completion.Normal;
        }
    }

    /// <summary>
    /// break.
    /// </summary>
    /// <remarks>
    /// Ob break hier ueberhaupt erlaubt ist, entscheidet der Erbauer: er kennt die lexikalische
    /// Verschachtelung und lehnt ein break ausserhalb von Schleife und switch schon beim Bauen
    /// ab. Der ScriptVisitor musste das zur Laufzeit ueber ein Instanzfeld pruefen, weil er
    /// keine Bauphase hat.
    /// </remarks>
    public sealed class BreakNode : StatementNode
    {
        public BreakNode(SourcePosition position)
            : base(position)
        {
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            return Completion.Break;
        }
    }

    /// <summary>
    /// continue. In einem switch bedeutet es Fall-Through in die naechste Klausel.
    /// </summary>
    public sealed class ContinueNode : StatementNode
    {
        public ContinueNode(SourcePosition position)
            : base(position)
        {
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            return Completion.Continue;
        }
    }

    /// <summary>
    /// return, mit oder ohne Wert.
    /// </summary>
    public sealed class ReturnNode : StatementNode
    {
        private readonly IExpressionNode value;

        /// <param name="value">der Rueckgabewert, oder null fuer ein blosses return</param>
        public ReturnNode(SourcePosition position, IExpressionNode value)
            : base(position)
        {
            this.value = value;
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            if (value == null)
            {
                // Die Grammatik erlaubt "return;" ohne Ausdruck. Der ScriptVisitor besuchte
                // den fehlenden Ausdruck ungeprueft und lief in eine NullReferenceException.
                return Completion.Return(null);
            }

            var retVal = new LiteralScriptValue(context.BypassCompatibilityOnLazyInvokation);
            retVal.Initialize(value.Evaluate(context).GetValue(null, context.Policy));
            return Completion.Return(retVal);
        }
    }

    /// <summary>
    /// throw, mit Ausdruck oder als erneutes Werfen der behandelten Ausnahme.
    /// </summary>
    public sealed class ThrowNode : StatementNode
    {
        private readonly IExpressionNode value;

        /// <param name="value">der zu werfende Ausdruck, oder null fuer ein blosses throw</param>
        public ThrowNode(SourcePosition position, IExpressionNode value)
            : base(position)
        {
            this.value = value;
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            if (value == null)
            {
                return Completion.ReThrow;
            }

            return Completion.Throw(value.Evaluate(context).GetValue(null, context.Policy));
        }
    }
}
