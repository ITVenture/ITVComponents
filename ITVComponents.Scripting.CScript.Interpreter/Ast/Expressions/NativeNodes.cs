using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Interpreter.Ast.Expressions
{
    /// <summary>
    /// Gemeinsames fuer die Knoten, die C#-Code ueber Roslyn ausfuehren.
    /// </summary>
    internal static class NativeGuard
    {
        /// <summary>
        /// Prueft, ob natives Scripting erlaubt ist.
        /// </summary>
        /// <remarks>
        /// Bewusst zur Laufzeit gegen die Policy des Laufs, nicht beim Bauen: derselbe Baum
        /// kann unter verschiedenen Policies laufen, und ein nativer Aufruf in einem nicht
        /// genommenen Zweig darf nicht anschlagen.
        /// </remarks>
        public static void Ensure(ScriptingPolicy policy)
        {
            if (policy.IsDenied(policy.NativeScripting))
            {
                throw new ScriptSecurityException("Native scripting was disabled by policy.");
            }
        }

        /// <summary>
        /// Liest die benannten Parameter hinter "with".
        /// </summary>
        /// <remarks>
        /// Steht dort kein Objekt-Literal, wird eine leere Sammlung gereicht statt null.
        /// NativeScriptHelper.RunLinqQuery greift ungeprueft darauf zu - mit null endete das
        /// in einer NullReferenceException statt einer verstaendlichen Meldung.
        /// </remarks>
        public static IDictionary<string, object> Parameters(IExpressionNode node, ExecutionContext context)
        {
            object value = node.Evaluate(context).GetValue(null, context.Policy);
            return (value as ObjectLiteral)?.Snapshot() ?? new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Fuehrt C#-Code auf einem Zielobjekt aus: `E(ziel as name -&gt; konfiguration)::"code" with {...}.
    /// </summary>
    public sealed class NativeExpressionNode : ExpressionNode
    {
        private readonly IExpressionNode target;
        private readonly IExpressionNode code;
        private readonly IExpressionNode parameters;
        private readonly string targetName;
        private readonly string configuration;

        public NativeExpressionNode(SourcePosition position, IExpressionNode target, IExpressionNode code,
            IExpressionNode parameters, string targetName, string configuration)
            : base(position)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.code = code ?? throw new ArgumentNullException(nameof(code));
            this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            this.targetName = targetName;
            this.configuration = configuration;
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            NativeGuard.Ensure(context.Policy);

            object targetValue = target.Evaluate(context).GetValue(null, context.Policy);
            if (!(code.Evaluate(context).GetValue(null, context.Policy) is string text))
            {
                throw new InvalidOperationException("string value expected for linq execution!");
            }

            IDictionary<string, object> arguments = NativeGuard.Parameters(parameters, context);
            return Literal(context,
                NativeScriptHelper.RunLinqQuery(configuration, targetValue, targetName, null, text, arguments));
        }
    }

    /// <summary>
    /// Fuehrt einen C#-Codeblock ohne Zielobjekt aus: `E(#konfiguration)::@#...# with {...}.
    /// </summary>
    public sealed class NativeLiteralNode : ExpressionNode
    {
        private readonly string code;
        private readonly IExpressionNode parameters;
        private readonly string configuration;

        public NativeLiteralNode(SourcePosition position, string code, IExpressionNode parameters,
            string configuration)
            : base(position)
        {
            this.code = code ?? throw new ArgumentNullException(nameof(code));
            this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            this.configuration = configuration;
        }

        /// <inheritdoc/>
        public override ScriptValue Evaluate(ExecutionContext context)
        {
            NativeGuard.Ensure(context.Policy);
            IDictionary<string, object> arguments = NativeGuard.Parameters(parameters, context);
            return Literal(context, NativeScriptHelper.RunLinqQuery(configuration, null, code, arguments));
        }
    }
}
