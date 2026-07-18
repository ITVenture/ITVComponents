using System;
using System.Collections;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Statements
{
    /// <summary>
    /// Gemeinsames Verhalten aller Schleifen im Umgang mit dem Ergebnis eines Rumpfdurchlaufs.
    /// </summary>
    internal enum LoopAction
    {
        /// <summary>Weiterlaufen.</summary>
        Continue,

        /// <summary>Die Schleife verlassen.</summary>
        Break,

        /// <summary>Das Ergebnis nach aussen durchreichen.</summary>
        Propagate
    }

    /// <summary>
    /// Basis der Schleifen-Knoten.
    /// </summary>
    public abstract class LoopNode : StatementNode
    {
        protected LoopNode(SourcePosition position)
            : base(position)
        {
        }

        /// <summary>
        /// Entscheidet, wie es nach einem Rumpfdurchlauf weitergeht.
        /// </summary>
        /// <remarks>
        /// break und continue enden hier - alles andere (return, throw) geht nach aussen.
        /// </remarks>
        internal static LoopAction Decide(Completion completion)
        {
            switch (completion.Kind)
            {
                case CompletionKind.Normal:
                case CompletionKind.Continue:
                    return LoopAction.Continue;
                case CompletionKind.Break:
                    return LoopAction.Break;
                default:
                    return LoopAction.Propagate;
            }
        }
    }

    /// <summary>
    /// Eine kopfgesteuerte Schleife.
    /// </summary>
    public sealed class WhileNode : LoopNode
    {
        private readonly IExpressionNode condition;
        private readonly IStatementNode body;

        public WhileNode(SourcePosition position, IExpressionNode condition, IStatementNode body)
            : base(position)
        {
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.body = body ?? throw new ArgumentNullException(nameof(body));
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            while (ValueHelper.IsTrue(condition.Evaluate(context), context))
            {
                Completion completion = body.Execute(context);
                LoopAction action = Decide(completion);
                if (action == LoopAction.Break)
                {
                    break;
                }

                if (action == LoopAction.Propagate)
                {
                    return completion;
                }
            }

            return Completion.Normal;
        }
    }

    /// <summary>
    /// Eine fussgesteuerte Schleife. Der Rumpf laeuft mindestens einmal.
    /// </summary>
    public sealed class DoWhileNode : LoopNode
    {
        private readonly IExpressionNode condition;
        private readonly IStatementNode body;

        public DoWhileNode(SourcePosition position, IExpressionNode condition, IStatementNode body)
            : base(position)
        {
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.body = body ?? throw new ArgumentNullException(nameof(body));
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            do
            {
                Completion completion = body.Execute(context);
                LoopAction action = Decide(completion);
                if (action == LoopAction.Break)
                {
                    break;
                }

                if (action == LoopAction.Propagate)
                {
                    return completion;
                }
            } while (ValueHelper.IsTrue(condition.Evaluate(context), context));

            return Completion.Normal;
        }
    }

    /// <summary>
    /// Eine Zaehlschleife.
    /// </summary>
    /// <remarks>
    /// Kopf und Rumpf teilen sich einen Scope, den dieser Knoten oeffnet - der Rumpf-Block wird
    /// deshalb ohne eigenen Scope gebaut. Der Scope wird bewusst nicht pro Durchlauf
    /// zurueckgesetzt; im Rumpf angelegte Variablen ueberdauern die Durchlaeufe, so wie beim
    /// ScriptVisitor.
    /// </remarks>
    public sealed class ForNode : LoopNode
    {
        private readonly IReadOnlyList<IExpressionNode> initializer;
        private readonly IReadOnlyList<IExpressionNode> condition;
        private readonly IReadOnlyList<IExpressionNode> iterator;
        private readonly IStatementNode body;

        public ForNode(SourcePosition position, IReadOnlyList<IExpressionNode> initializer,
            IReadOnlyList<IExpressionNode> condition, IReadOnlyList<IExpressionNode> iterator,
            IStatementNode body)
            : base(position)
        {
            this.initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.iterator = iterator ?? throw new ArgumentNullException(nameof(iterator));
            this.body = body ?? throw new ArgumentNullException(nameof(body));
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            context.Variables.OpenInnerScope();
            try
            {
                Evaluate(context, initializer);
                while (IsConditionTrue(context))
                {
                    Completion completion = body.Execute(context);
                    LoopAction action = Decide(completion);
                    if (action == LoopAction.Break)
                    {
                        break;
                    }

                    if (action == LoopAction.Propagate)
                    {
                        return completion;
                    }

                    // Laeuft auch nach continue - sonst zaehlte die Schleife nicht weiter.
                    Evaluate(context, iterator);
                }

                return Completion.Normal;
            }
            finally
            {
                context.Variables.CollapseScope();
            }
        }

        /// <summary>
        /// Eine leere Bedingung ist wahr - damit ist for(;;) eine Endlosschleife.
        /// </summary>
        /// <remarks>
        /// Der ScriptVisitor lehnte jeden for-Kopf ab, in dem einer der drei Teile fehlte,
        /// obwohl die Grammatik alle drei als optional fuehrt.
        /// </remarks>
        private bool IsConditionTrue(ExecutionContext context)
        {
            if (condition.Count == 0)
            {
                return true;
            }

            bool result = false;
            for (int i = 0; i < condition.Count; i++)
            {
                result = ValueHelper.IsTrue(condition[i].Evaluate(context), context);
            }

            return result;
        }

        private static void Evaluate(ExecutionContext context, IReadOnlyList<IExpressionNode> expressions)
        {
            for (int i = 0; i < expressions.Count; i++)
            {
                expressions[i].Evaluate(context).GetValue(null, context.Policy);
            }
        }
    }

    /// <summary>
    /// Eine Schleife ueber eine Aufzaehlung.
    /// </summary>
    public sealed class ForEachNode : LoopNode
    {
        private readonly IExpressionNode variable;
        private readonly IExpressionNode source;
        private readonly IStatementNode body;

        public ForEachNode(SourcePosition position, IExpressionNode variable, IExpressionNode source,
            IStatementNode body)
            : base(position)
        {
            this.variable = variable ?? throw new ArgumentNullException(nameof(variable));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.body = body ?? throw new ArgumentNullException(nameof(body));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Die Aufzaehlung wird vor der Laufvariablen ausgewertet, und beide vor dem Oeffnen des
        /// inneren Scopes - die Laufvariable liegt damit im aeusseren Scope und behaelt nach der
        /// Schleife ihren letzten Wert. Das ist Ist-Verhalten des ScriptVisitors.
        /// </remarks>
        public override Completion Execute(ExecutionContext context)
        {
            object sourceValue = source.Evaluate(context).GetValue(null, context.Policy);
            var target = variable.Evaluate(context);

            if (!(sourceValue is IEnumerable enumerable))
            {
                return Completion.Fail(
                    $"Enumerable object required at {Position.Line}/{Position.Column}");
            }

            context.Variables.OpenInnerScope();
            try
            {
                foreach (object current in enumerable)
                {
                    target.SetValue(current, null, context.Policy);
                    Completion completion = body.Execute(context);
                    LoopAction action = Decide(completion);
                    if (action == LoopAction.Break)
                    {
                        break;
                    }

                    if (action == LoopAction.Propagate)
                    {
                        return completion;
                    }
                }

                return Completion.Normal;
            }
            finally
            {
                context.Variables.CollapseScope();
            }
        }
    }
}
