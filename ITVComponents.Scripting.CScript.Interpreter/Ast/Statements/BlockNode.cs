using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Statements
{
    /// <summary>
    /// Eine Folge von Anweisungen, wahlweise mit eigenem Variablen-Scope.
    /// </summary>
    /// <remarks>
    /// Ob ein Block einen Scope oeffnet, entscheidet der Knoten selbst - der Erbauer setzt es
    /// beim Bauen fest. Beim ScriptVisitor lief das ueber das Instanzfeld openBlockScope, das
    /// ein Aufrufer vor dem Besuch setzte und der Block danach zuruecksetzte: eine Fernwirkung
    /// ueber Methodengrenzen hinweg, bei der vier Aufrufer den Wert hart auf true zurueckstellten
    /// statt den vorherigen wiederherzustellen.
    ///
    /// Kein eigener Scope brauchen: der Rumpf einer for-/foreach-Schleife (er teilt ihn mit dem
    /// Schleifenkopf) und der Rumpf eines catch (er teilt ihn mit der Ausnahmevariablen).
    /// </remarks>
    public sealed class BlockNode : StatementNode
    {
        private readonly IReadOnlyList<IStatementNode> statements;
        private readonly bool ownsScope;

        public BlockNode(SourcePosition position, IReadOnlyList<IStatementNode> statements, bool ownsScope)
            : base(position)
        {
            this.statements = statements ?? throw new ArgumentNullException(nameof(statements));
            this.ownsScope = ownsScope;
        }

        /// <summary>
        /// Die Anweisungen dieses Blocks.
        /// </summary>
        public IReadOnlyList<IStatementNode> Statements => statements;

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            if (!ownsScope)
            {
                return ExecuteStatements(context);
            }

            context.Variables.OpenInnerScope();
            try
            {
                return ExecuteStatements(context);
            }
            finally
            {
                context.Variables.CollapseScope();
            }
        }

        private Completion ExecuteStatements(ExecutionContext context)
        {
            IExecutionObserver observer = context.Observer;
            for (int i = 0; i < statements.Count; i++)
            {
                IStatementNode statement = statements[i];
                observer?.BeforeStatement(statement, context);
                Completion completion = statement.Execute(context);
                observer?.AfterStatement(statement, context, completion);

                // Ein abruptes Ergebnis behandelt dieser Block nicht selbst - wer es behandelt,
                // entscheidet der umgebende Knoten (Schleife, switch, try, Funktion).
                if (completion.IsAbrupt)
                {
                    return completion;
                }
            }

            return Completion.Normal;
        }
    }
}
