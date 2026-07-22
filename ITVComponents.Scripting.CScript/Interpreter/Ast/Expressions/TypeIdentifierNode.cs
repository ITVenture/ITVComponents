using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Ast.Expressions
{
    /// <summary>
    /// Ein Typbezeichner eines Typ-Hinweises, etwa a.b.MyType.
    /// </summary>
    /// <remarks>
    /// Loest ueber den Scope auf, nicht ueber Reflection: die Pfadsegmente vor dem letzten
    /// muessen verschachtelte Scopes sein. Nicht zu verwechseln mit TypeLiteralNode, das
    /// echte CLR-Typen laedt.
    ///
    /// Der abgeloeste Builder baute hier einen Knoten, dessen Argumente er anschliessend
    /// nie setzte - der gesamte Typpfad ging verloren und jeder Typ-Hinweis war wirkungslos.
    /// </remarks>
    public sealed class TypeIdentifierNode : ExpressionNode
    {
        private readonly IReadOnlyList<string> path;

        public TypeIdentifierNode(SourcePosition position, IReadOnlyList<string> path)
            : base(position)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            if (path.Count == 0)
            {
                throw new ArgumentException("Ein Typbezeichner braucht mindestens ein Segment.", nameof(path));
            }
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            IScope scope = context.Variables;
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (!(scope[path[i]] is IScope inner))
                {
                    throw new ScriptException($"Failed to resolve Type at {Position.Line}/{Position.Column}");
                }

                scope = inner;
            }

            var retVal = new VariableAccessValue(context.BypassCompatibilityOnLazyInvokation);
            retVal.Initialize(scope, path[path.Count - 1]);
            return retVal;
        }
    }

    /// <summary>
    /// Ein typisiertes null: null'System.String'.
    /// </summary>
    /// <remarks>
    /// Traegt den Zieltyp mit, damit die Ueberladungsaufloesung eine null-Referenz einem
    /// Parameter zuordnen kann.
    /// </remarks>
    public sealed class TypedNullNode : ExpressionNode
    {
        private readonly IExpressionNode type;

        public TypedNullNode(SourcePosition position, IExpressionNode type)
            : base(position)
        {
            this.type = type ?? throw new ArgumentNullException(nameof(type));
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            var targetType = (Type)type.Evaluate(context).GetValue(null, context.Policy);
            return Literal(context, new TypedNull { Type = targetType });
        }
    }
}
