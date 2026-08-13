using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.AssemblyResolving;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Scripting.CScript.Security.Restrictions;

namespace ITVComponents.Scripting.CScript.Ast.Expressions
{
    /// <summary>
    /// Ein Typ-Literal: 'System.Int32' beziehungsweise 'X.Y'@"Assembly".
    /// </summary>
    /// <remarks>
    /// Bewusste Abweichung vom abgeloesten Builder: der Typ wird zwar weiterhin beim Bauen
    /// aufgeloest (Reflection ist teuer und der Name ist konstant), die Policy-Pruefung
    /// passiert aber erst beim Auswerten. Der Builder hat beides zur Bauzeit gemacht und
    /// damit die Policy in den Baum eingebacken - derselbe zwischengespeicherte Baum haette
    /// unter einer strengeren Policy weiter Zugriff gewaehrt.
    /// </remarks>
    public sealed class TypeLiteralNode : ExpressionNode
    {
        private readonly Type resolvedType;
        private readonly Assembly sourceAssembly;
        private readonly IReadOnlyList<IExpressionNode> typeArguments;

        private TypeLiteralNode(SourcePosition position, Type resolvedType, Assembly sourceAssembly,
            IReadOnlyList<IExpressionNode> typeArguments)
            : base(position)
        {
            this.resolvedType = resolvedType;
            this.sourceAssembly = sourceAssembly;
            this.typeArguments = typeArguments;
        }

        /// <summary>
        /// Loest den Typnamen auf und erzeugt den Knoten.
        /// </summary>
        /// <param name="position">die Quellposition</param>
        /// <param name="typeName">
        /// der Name des Typs - bei der offenen Form bereits samt Stelligkeit (Name`1), weil dann
        /// keine Argumente vorliegen, aus denen sie sich ableiten liesse
        /// </param>
        /// <param name="assemblyName">die Assembly, in der gesucht wird, oder null</param>
        /// <param name="typeArguments">die generischen Argumente, oder null</param>
        public static TypeLiteralNode Resolve(SourcePosition position, string typeName, string assemblyName,
            IReadOnlyList<IExpressionNode> typeArguments)
        {
            string fullName = typeName;
            if (typeArguments != null && typeArguments.Count != 0)
            {
                fullName = $"{typeName}`{typeArguments.Count}";
            }

            Assembly source = null;
            Type resolved;
            if (assemblyName != null)
            {
                source = AssemblyResolver.FindAssemblyByName(assemblyName);
                resolved = source.GetType(fullName);
            }
            else
            {
                resolved = Type.GetType(fullName);
            }

            if (resolved == null)
            {
                throw new ScriptException($"Unknown type {fullName} at {position.Line}/{position.Column}");
            }

            return new TypeLiteralNode(position, resolved, source, typeArguments);
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            ScriptingPolicy policy = context.Policy;
            PolicyMode startPolicy = policy.TypeLoading != PolicyMode.Default ? policy.TypeLoading : policy.PolicyMode;
            if (sourceAssembly != null && policy.IsDenied(sourceAssembly, startPolicy))
            {
                startPolicy = PolicyMode.Deny;
            }

            Type effective = resolvedType;
            Deny(policy, effective, startPolicy);

            if (typeArguments != null && typeArguments.Count != 0)
            {
                effective = effective.MakeGenericType(typeArguments
                    .Select(t => (Type)t.Evaluate(context).GetValue(null, policy)).ToArray());
                Deny(policy, effective, startPolicy);
            }

            return Literal(context, effective);
        }

        private static void Deny(ScriptingPolicy policy, Type type, PolicyMode startPolicy)
        {
            if (policy.IsDenied(type, TypeAccessMode.Direct, startPolicy))
            {
                throw new ScriptSecurityException($"Access to the type {type.FullName} was denied by policy.");
            }
        }
    }
}
