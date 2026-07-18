using System;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Statements
{
    /// <summary>
    /// Meldet eine Assembly-Referenz fuer eine native Konfiguration an: `R(konfiguration)"...".
    /// </summary>
    /// <remarks>
    /// Die Anmeldung passiert bei der Ausfuehrung, nicht beim Bauen. Das ist wesentlich: die
    /// Anweisung kann in einem Zweig oder einer Schleife stehen, und die Reihenfolge gegenueber
    /// den nativen Aufrufen entscheidet mit darueber, wann NativeConfiguration ihren
    /// Kompilat-Zwischenspeicher verwirft. Zoege man den Seiteneffekt in die Bauphase, liefe
    /// ein Script, das erst Code ausfuehrt und dann eine Referenz nachtraegt, anders.
    /// </remarks>
    public sealed class NativeReferenceNode : StatementNode
    {
        private readonly string configuration;
        private readonly string reference;

        public NativeReferenceNode(SourcePosition position, string configuration, string reference)
            : base(position)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.reference = reference ?? throw new ArgumentNullException(nameof(reference));
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            NativeGuard.Ensure(context.Policy);
            NativeScriptHelper.AddReference(configuration, reference);
            return Completion.Normal;
        }
    }

    /// <summary>
    /// Meldet eine using-Anweisung fuer eine native Konfiguration an: `U(konfiguration)"...".
    /// </summary>
    public sealed class NativeUsingNode : StatementNode
    {
        private readonly string configuration;
        private readonly string usingName;

        public NativeUsingNode(SourcePosition position, string configuration, string usingName)
            : base(position)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.usingName = usingName ?? throw new ArgumentNullException(nameof(usingName));
        }

        /// <inheritdoc/>
        public override Completion Execute(ExecutionContext context)
        {
            NativeGuard.Ensure(context.Policy);
            NativeScriptHelper.AddUsing(configuration, usingName);
            return Completion.Normal;
        }
    }
}
