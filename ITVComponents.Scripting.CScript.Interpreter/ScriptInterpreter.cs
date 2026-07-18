using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Interpreter.Ast;
using ITVComponents.Scripting.CScript.Interpreter.Ast.Building;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Interpreter
{
    /// <summary>
    /// Einstiegspunkt des Interpreters. Gegenstueck zu ExpressionParser, das auf dem
    /// ScriptVisitor aufsetzt.
    /// </summary>
    public static class ScriptInterpreter
    {
        /// <summary>
        /// Zwischenspeicher der uebersetzten Ausdruecke, mit dem Quelltext als Schluessel.
        /// </summary>
        /// <remarks>
        /// Lazy sorgt dafuer, dass ein Quelltext auch bei gleichzeitigen Zugriffen nur einmal
        /// uebersetzt wird. Das Ergebnis ist unveraenderlich und damit gefahrlos teilbar -
        /// anders als beim ScriptVisitor, wo pro Lauf eine eigene Instanz noetig war.
        /// </remarks>
        private static readonly ConcurrentDictionary<string, Lazy<CompiledScript>> compiledExpressions =
            new ConcurrentDictionary<string, Lazy<CompiledScript>>();

        /// <summary>
        /// Uebersetzt einen einzelnen Ausdruck.
        /// </summary>
        /// <param name="expression">der Ausdruck</param>
        /// <returns>das uebersetzte Script</returns>
        public static CompiledScript Compile(string expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            return compiledExpressions.GetOrAdd(expression,
                key => new Lazy<CompiledScript>(() => Build(key))).Value;
        }

        /// <summary>
        /// Wertet einen einzelnen Ausdruck aus.
        /// </summary>
        /// <param name="expression">der Ausdruck</param>
        /// <param name="variables">die Startvariablen</param>
        /// <param name="scopeInitializer">ein Initialisierer fuer den Scope, oder null</param>
        /// <param name="policy">die geltende Sicherheits-Policy, oder null</param>
        /// <returns>das Ergebnis der Auswertung</returns>
        public static object Parse(string expression, IDictionary<string, object> variables,
            InitializeScopeVariables scopeInitializer = null, ScriptingPolicy policy = null)
        {
            return Compile(expression).Execute(variables, scopeInitializer, policy);
        }

        /// <summary>
        /// Leert den Uebersetzungs-Zwischenspeicher.
        /// </summary>
        public static void ClearCache()
        {
            compiledExpressions.Clear();
        }

        private static CompiledScript Build(string expression)
        {
            ParserRuleContext tree = ExpressionParser.GetRawExpressionTree(expression,
                ExpressionParser.ExpressionMode.Expression);
            var builder = new AstBuilder();
            IExpressionNode root = builder.BuildExpression(tree);
            return new CompiledScript(root);
        }
    }
}
