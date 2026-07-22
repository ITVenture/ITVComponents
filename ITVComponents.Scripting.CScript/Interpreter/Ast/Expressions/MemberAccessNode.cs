using System;
using ITVComponents.Scripting.CScript.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Ast.Expressions
{
    /// <summary>
    /// Zugriff auf ein Member eines Werts: a.b beziehungsweise a?.b.
    /// </summary>
    /// <remarks>
    /// Anders als der abgeloeste Builder faltet dieser Knoten Ketten nicht in einen
    /// MemberPath zusammen: a.b.c ist ein MemberAccessNode ueber einem MemberAccessNode.
    /// Das kostet nichts (der Inline-Cache sitzt ohnehin pro Knoten) und macht die
    /// Schrittgranularitaet des Debuggers vorhersagbar.
    /// </remarks>
    public sealed class MemberAccessNode : ExpressionNode
    {
        private readonly IExpressionNode target;
        private readonly string memberName;
        private readonly IExpressionNode explicitType;
        private readonly bool nullPropagating;
        private readonly bool instanceSemantics;

        /// <param name="instanceSemantics">
        /// ob der Zugriff auf ein Type-Objekt dessen eigene Member meint statt der statischen
        /// Member der Klasse, fuer die es steht. Setzt der Erbauer, wenn die Basis ein $Type
        /// ist - siehe <see cref="TypeOfNode"/>.
        /// </param>
        public MemberAccessNode(SourcePosition position, IExpressionNode target, string memberName,
            IExpressionNode explicitType = null, bool nullPropagating = false,
            bool instanceSemantics = false)
            : base(position)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.memberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
            this.explicitType = explicitType;
            this.nullPropagating = nullPropagating;
            this.instanceSemantics = instanceSemantics;
        }

        /// <summary>
        /// Der Name des Members.
        /// </summary>
        public string MemberName => memberName;

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            ScriptValue baseValue = target.Evaluate(context);
            Type typeHint = ResolveExplicitType(context);

            // Die schwache Variante liefert null statt zu werfen, wenn der Basiswert null ist.
            MemberAccessValue retVal = nullPropagating
                ? new WeakReferenceMemberAccessValue(CacheSlot(context),
                    context.BypassCompatibilityOnLazyInvokation, context.Policy)
                : new MemberAccessValue(CacheSlot(context), context.BypassCompatibilityOnLazyInvokation,
                    context.Policy);

            retVal.Initialize(baseValue, memberName, typeHint, instanceSemantics);
            return retVal;
        }

        private Type ResolveExplicitType(ExecutionContext context)
        {
            if (explicitType == null)
            {
                return null;
            }

            return explicitType.Evaluate(context).GetValue(null, context.Policy) as Type;
        }
    }
}
