using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Das Erzeugen einer Instanz: new T(x) beziehungsweise new T { a: 1 }.
    /// </summary>
    public sealed class NewNode : ExpressionNode
    {
        private readonly IExpressionNode type;
        private readonly IReadOnlyList<IExpressionNode> arguments;
        private readonly IReadOnlyList<IExpressionNode> typeArguments;
        private readonly ObjectLiteralNode initializer;

        public NewNode(SourcePosition position, IExpressionNode type, IReadOnlyList<IExpressionNode> arguments,
            IReadOnlyList<IExpressionNode> typeArguments, ObjectLiteralNode initializer)
            : base(position)
        {
            this.type = type ?? throw new ArgumentNullException(nameof(type));
            this.arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            this.typeArguments = typeArguments;
            this.initializer = initializer;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Der Konstruktor-Pfad von ScriptValue.GetValue erwartet nur zwei Argumente -
        /// Typargumente und Argumente - waehrend der Methoden-Pfad drei nimmt. Ein dritter
        /// Eintrag wuerde hier ignoriert, ein fehlender zweiter dagegen zur Ausnahme fuehren.
        /// </remarks>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            ScriptValue typeValue = type.Evaluate(context);
            SequenceValue argumentValues = Sequence(context, arguments);
            SequenceValue typeArgumentValues = typeArguments == null ? null : Sequence(context, typeArguments);

            object instance;
            try
            {
                typeValue.ValueType = ScriptValues.ValueType.Constructor;
                instance = typeValue.GetValue(new ScriptValue[] { typeArgumentValues, argumentValues },
                    context.Policy);
            }
            catch (ScriptException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ScriptException(
                    $"Failed to create new instance at {Position.Line}/{Position.Column}", ex);
            }

            if (initializer != null)
            {
                Initialize(context, instance);
            }

            return Literal(context, instance);
        }

        /// <summary>
        /// Uebertraegt die Werte eines Initialisierers auf die frische Instanz.
        /// </summary>
        private void Initialize(ExecutionContext context, object instance)
        {
            object literal = initializer.Evaluate(context).GetValue(null, context.Policy);
            if (!(literal is ObjectLiteral values))
            {
                throw new ScriptException(
                    $"Unexpected value provided as initializer! at {Position.Line}/{Position.Column}");
            }

            foreach (var item in values)
            {
                instance.SetMemberValue(item.Key, item.Value, null, ScriptValues.ValueType.PropertyOrField,
                    context.Policy);
            }
        }

        private static SequenceValue Sequence(ExecutionContext context, IReadOnlyList<IExpressionNode> nodes)
        {
            var values = new ScriptValue[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                values[i] = nodes[i].Evaluate(context);
            }

            var retVal = new SequenceValue(context.BypassCompatibilityOnLazyInvokation);
            retVal.Initialize(values);
            return retVal;
        }
    }

    /// <summary>
    /// Ein ref-Literal: ref'System.Int32'. Markiert einen Parameter als by-ref.
    /// </summary>
    public sealed class RefLiteralNode : ExpressionNode
    {
        private readonly IExpressionNode type;

        public RefLiteralNode(SourcePosition position, IExpressionNode type)
            : base(position)
        {
            this.type = type ?? throw new ArgumentNullException(nameof(type));
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            var targetType = (Type)type.Evaluate(context).GetValue(null, context.Policy);
            return Literal(context, new ReferenceWrapper { Type = targetType.MakeByRefType() });
        }
    }
}
