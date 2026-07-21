using System;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Der Zugriff $Type: liefert den Typ, fuer den ein Ausdruck steht.
    /// </summary>
    /// <remarks>
    /// $Type ist kein gewoehnliches Member, sondern ein Wechsel der Auswertungsstrategie. Ein
    /// Ausdruck, der einen Type liefert, steht im Scripting fuer seine Klasse - Zugriffe darauf
    /// sind statische Zugriffe, genau wie bei 'System.String'. Nach einem $Type ist dagegen das
    /// Type-Objekt selbst gemeint.
    ///
    /// Dieser Knoten liefert nur den Typ. Den Strategiewechsel traegt der Zugriff, der auf ihn
    /// folgt: der Erbauer erkennt beim Bauen, dass dessen Basis ein $Type ist, und schaltet den
    /// Zugriff fest auf Instanz-Ebene. Zur Laufzeit kostet das nichts, und es ist eindeutig -
    /// ein statisches Member gleichen Namens kann nicht mehr dazwischenkommen.
    ///
    /// Deshalb braucht es auch kein Huellobjekt: was hier herauskommt, ist ein gewoehnliches
    /// Type-Objekt und laesst sich zuweisen, weitergeben und vergleichen.
    /// </remarks>
    public sealed class TypeOfNode : ExpressionNode
    {
        private readonly IExpressionNode target;

        public TypeOfNode(SourcePosition position, IExpressionNode target)
            : base(position)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            object value = target.Evaluate(context).GetValue(null, context.Policy);

            // Ein Type steht fuer sich selbst, alles andere fuer seinen Laufzeittyp.
            return Literal(context, value as Type ?? value?.GetType());
        }
    }
}
