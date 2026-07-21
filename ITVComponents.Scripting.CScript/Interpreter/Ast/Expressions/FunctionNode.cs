using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Eine Funktionsdefinition, benannt oder anonym.
    /// </summary>
    /// <remarks>
    /// Eine benannte Definition legt sich zusaetzlich unter ihrem Namen im Scope ab. Das
    /// geschieht erst beim Auswerten, nicht vorab: eine Funktion, die vor ihrer Definition
    /// aufgerufen wird, existiert nicht. Kein Hoisting - so wie beim ScriptVisitor.
    /// </remarks>
    public sealed class FunctionNode : ExpressionNode
    {
        private readonly string name;
        private readonly string[] parameters;
        private readonly IStatementNode body;

        public FunctionNode(SourcePosition position, string name, string[] parameters, IStatementNode body)
            : base(position)
        {
            this.name = name;
            this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            this.body = body ?? throw new ArgumentNullException(nameof(body));
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            if (context.Policy.IsDenied(context.Policy.ScriptMethods))
            {
                throw new ScriptSecurityException("Implementing script-methods was denied by policy.");
            }

            // Die Funktion nimmt den Zustand des umgebenden Scopes als Kopie mit. Sie sieht
            // spaetere Aenderungen daran also nicht - eine Momentaufnahme, keine lebende
            // Referenz. Das ist Ist-Verhalten des ScriptVisitors.
            Dictionary<string, object> initial = context.Variables.Snapshot();

            // Der Name geht mit: die Funktion bindet sich darunter in ihren eigenen Scope und
            // kann sich damit selbst aufrufen. Ohne das steht im Snapshot nichts unter diesem
            // Namen, weil er erst danach gebunden wird.
            var function = new InterpretedFunction(initial, parameters, body, context.Policy, name);
            if (context.Variables is FunctionScope functionScope)
            {
                function.ParentScope = functionScope.ParentScope;
            }

            if (name != null)
            {
                context.Variables[name] = function;
            }

            return Literal(context, function);
        }
    }

    /// <summary>
    /// Ein Objekt-Literal: { a: 1, b: function() { ... } }.
    /// </summary>
    public sealed class ObjectLiteralNode : ExpressionNode
    {
        private readonly IReadOnlyList<KeyValuePair<string, IExpressionNode>> properties;

        public ObjectLiteralNode(SourcePosition position,
            IReadOnlyList<KeyValuePair<string, IExpressionNode>> properties)
            : base(position)
        {
            this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            var values = new Dictionary<string, object>();
            foreach (var property in properties)
            {
                values[property.Key] = property.Value.Evaluate(context).GetValue(null, context.Policy);
            }

            var retVal = new ObjectLiteral(values, context.Variables);

            // Enthaltene Funktionen bekommen einen eigenen Scope und das Literal als
            // Elternscope - erst dadurch sieht eine Methode die Geschwister-Eigenschaften
            // ihres Objekts.
            foreach (var item in values)
            {
                if (item.Value is FunctionLiteral function)
                {
                    FunctionLiteral copy = function.Copy();
                    retVal[item.Key] = copy;
                    copy.ParentScope = retVal;
                }
            }

            return Literal(context, retVal);
        }
    }
}
