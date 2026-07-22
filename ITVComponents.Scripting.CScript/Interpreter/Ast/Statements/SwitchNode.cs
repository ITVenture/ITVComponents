using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Runtime;

namespace ITVComponents.Scripting.CScript.Ast.Statements
{
    /// <summary>
    /// Eine case-Klausel eines switch-Blocks.
    /// </summary>
    public sealed class CaseClause
    {
        public CaseClause(IExpressionNode label, IStatementNode body)
        {
            this.Label = label ?? throw new ArgumentNullException(nameof(label));
            this.Body = body;
        }

        /// <summary>Der Vergleichswert dieser Klausel.</summary>
        public IExpressionNode Label { get; }

        /// <summary>Der Rumpf, oder null wenn die Klausel leer ist.</summary>
        public IStatementNode Body { get; }
    }

    /// <summary>
    /// Eine Mehrfachverzweigung.
    /// </summary>
    /// <remarks>
    /// Fall-Through ist in CScript ausdruecklich: eine zutreffende Klausel muss mit break
    /// enden oder mit continue in die naechste durchfallen. Laeuft ihr Rumpf einfach aus, ist
    /// das ein Fehler und kein stilles Weiterlaufen.
    ///
    /// Der Vergleichswert und das Fall-Through-Flag sind hier lokale Variablen. Beim
    /// ScriptVisitor waren beide in dasselbe Instanzfeld switchVal kodiert: fuer Fall-Through
    /// wurde der Vergleichswert mit einem Continue-Objekt ueberschrieben.
    /// </remarks>
    public sealed class SwitchNode : StatementNode
    {
        private readonly IExpressionNode value;
        private readonly IReadOnlyList<CaseClause> cases;
        private readonly IStatementNode defaultCase;

        public SwitchNode(SourcePosition position, IExpressionNode value, IReadOnlyList<CaseClause> cases,
            IStatementNode defaultCase)
            : base(position)
        {
            this.value = value ?? throw new ArgumentNullException(nameof(value));
            this.cases = cases ?? throw new ArgumentNullException(nameof(cases));
            this.defaultCase = defaultCase;
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            object switchValue = value.Evaluate(context).GetValue(null, context.Policy);
            bool fallingThrough = false;
            bool matched = false;

            for (int i = 0; i < cases.Count; i++)
            {
                CaseClause clause = cases[i];
                if (!fallingThrough)
                {
                    object label = clause.Label.Evaluate(context).GetValue(null, context.Policy);
                    if (!Matches(switchValue, label))
                    {
                        continue;
                    }
                }

                matched = true;
                fallingThrough = false;

                // Eine leere Klausel faellt in die naechste durch. Damit funktioniert die
                // uebliche Mehrfach-Markierung "case 1: case 2: tuwas(); break;", an der der
                // ScriptVisitor mit einer NullReferenceException scheiterte.
                if (clause.Body == null)
                {
                    fallingThrough = true;
                    continue;
                }

                Completion completion = clause.Body.Execute(context);
                switch (completion.Kind)
                {
                    case CompletionKind.Break:
                        return Completion.Normal;
                    case CompletionKind.Continue:
                        fallingThrough = true;
                        continue;
                    case CompletionKind.Normal:
                        return Completion.Fail(
                            "Should not fall implicit through Case Labels. Use Continue for falling " +
                            $"through at {Position.Line}/{Position.Column}");
                    default:
                        return completion;
                }
            }

            // Der default-Zweig laeuft nur, wenn keine Klausel zutraf. Beim ScriptVisitor lief
            // er zusaetzlich, wenn die letzte zutreffende Klausel mit continue endete.
            if (!matched && defaultCase != null)
            {
                Completion completion = defaultCase.Execute(context);
                if (completion.Kind == CompletionKind.Break)
                {
                    return Completion.Normal;
                }

                if (completion.IsAbrupt && completion.Kind != CompletionKind.Continue)
                {
                    return completion;
                }
            }

            return Completion.Normal;
        }

        private static bool Matches(object switchValue, object label)
        {
            if (switchValue == null)
            {
                return label == null;
            }

            return label != null && switchValue.Equals(label);
        }
    }
}
