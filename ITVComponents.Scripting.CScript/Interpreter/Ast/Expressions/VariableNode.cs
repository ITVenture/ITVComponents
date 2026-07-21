using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Zugriff auf eine Variable des aktuellen Scopes.
    /// </summary>
    /// <remarks>
    /// Liefert bewusst einen schreibbaren VariableAccessValue statt eines Werts: erst dadurch
    /// kann eine Zuweisung ihre linke Seite als Ziel benutzen. Der VariableAccessValue nimmt
    /// nie am Inline-Cache teil (er bekommt strukturell kein Creator-Symbol) - eine
    /// Dictionary-Suche im Scope ist bereits guenstiger als der Cache-Check.
    ///
    /// Unbekannte Namen liefern null statt einer Exception. Das ist Ist-Verhalten des
    /// ScriptVisitors und bleibt so.
    /// </remarks>
    public sealed class VariableNode : ExpressionNode
    {
        private readonly string name;

        public VariableNode(SourcePosition position, string name)
            : base(position)
        {
            this.name = name;
        }

        /// <summary>
        /// Der Name der Variablen.
        /// </summary>
        public string Name => name;

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            var retVal = new VariableAccessValue(context.BypassCompatibilityOnLazyInvokation);
            retVal.Initialize(context.Variables, name);
            return retVal;
        }
    }
}
