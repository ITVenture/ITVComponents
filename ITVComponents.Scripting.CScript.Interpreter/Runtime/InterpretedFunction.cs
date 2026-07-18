using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Ast;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Interpreter.Runtime
{
    /// <summary>
    /// Eine im Script definierte Funktion, deren Rumpf der Interpreter ausfuehrt.
    /// </summary>
    /// <remarks>
    /// Erbt bewusst von FunctionLiteral statt einen eigenen Typ einzufuehren: die gemeinsame
    /// Runtime prueft an mehreren Stellen auf genau diesen Typ - beim Aufruf ueber
    /// ScriptValue.GetValue, beim Auto-Invoke, beim Anhaengen an ein Ereignis und beim
    /// Einsammeln der Methoden eines Objekt-Literals. Eine bloss aehnliche Klasse wuerde
    /// dort ueberall durchfallen.
    ///
    /// Der Rumpf ist ein Anweisungsknoten; ausgefuehrt wird er im FunctionScope, den die
    /// Basisklasse vor dem Aufruf vorbereitet.
    /// </remarks>
    public sealed class InterpretedFunction : FunctionLiteral
    {
        private readonly IStatementNode body;
        private readonly string[] argumentNames;

        /// <param name="name">
        /// der Name, unter dem die Funktion definiert wurde, oder null bei einer anonymen.
        /// Die Basisklasse bindet die Funktion darunter in ihren eigenen Scope, damit sie sich
        /// selbst aufrufen kann.
        /// </param>
        public InterpretedFunction(Dictionary<string, object> values, string[] arguments, IStatementNode body,
            ScriptingPolicy policy, string name)
            : base(values, arguments, policy, name)
        {
            this.body = body ?? throw new ArgumentNullException(nameof(body));
            this.argumentNames = arguments;
        }

        /// <inheritdoc/>
        protected override object ExecuteBody()
        {
            var context = new ExecutionContext(Scope, Policy);
            Completion completion = body.Execute(context);

            switch (completion.Kind)
            {
                case CompletionKind.Return:
                    return completion.Value?.GetValue(null, context.Policy);
                case CompletionKind.Throw:
                    throw AsException(completion);
                default:
                    // Eine Funktion ohne return liefert null - wie beim ScriptVisitor, der
                    // GetScriptValueResult mit alwaysReturn: false aufruft.
                    return null;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Ein Objekt-Literal klont die Funktionen, die es aufnimmt, damit jede ihren eigenen
        /// Scope bekommt. Ohne diese Ueberschreibung faellt der Klon auf die Basisklasse
        /// zurueck und haette gar keinen Rumpf mehr.
        /// </remarks>
        public override FunctionLiteral Copy()
        {
            return new InterpretedFunction(InitialValues, argumentNames, body, Policy, FunctionName);
        }

        private static Exception AsException(Completion completion)
        {
            switch (completion.Thrown)
            {
                case ScriptException scriptException:
                    return scriptException;
                case Exception exception:
                    return new ScriptException("Error while executing Script", exception);
                default:
                    return new ScriptException(completion.Thrown?.ToString() ?? "Error while executing Script");
            }
        }
    }
}
