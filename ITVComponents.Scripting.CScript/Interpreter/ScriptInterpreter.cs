using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Interpreter.Ast;
using ITVComponents.Scripting.CScript.Interpreter.Ast.Building;
using ITVComponents.Scripting.CScript.Interpreter.Runtime.Debugging;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
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
        /// Zwischenspeicher der uebersetzten Programme. Getrennt von den Ausdruecken, weil
        /// derselbe Quelltext als Ausdruck und als Programm unterschiedlich uebersetzt wird.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Lazy<CompiledScript>> compiledPrograms =
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
        /// Wertet einen einzelnen Ausdruck gegen einen bestehenden Scope aus (Repl-Fall).
        /// </summary>
        /// <param name="expression">der Ausdruck</param>
        /// <param name="scope">der Scope, der zugleich die Sitzung ist</param>
        /// <param name="policy">die geltende Sicherheits-Policy, oder null fuer die des Scopes</param>
        /// <returns>das Ergebnis der Auswertung</returns>
        /// <remarks>
        /// Der Scope ist die Sitzung: aufeinanderfolgende Auswertungen sehen die Variablen der
        /// jeweils vorigen, weil die Programmwurzel keinen eigenen Scope oeffnet. So ersetzt der
        /// Interpreter die Repl-Ausfuehrung des ScriptVisitors, ohne dessen Instanzen-Pool.
        /// </remarks>
        public static object Parse(string expression, IScope scope, ScriptingPolicy policy = null)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return Compile(expression).Execute(scope, policy);
        }

        /// <summary>
        /// Fuehrt ein ganzes Programm gegen einen bestehenden Scope aus (Repl-Fall).
        /// </summary>
        /// <param name="script">der Quelltext</param>
        /// <param name="scope">der Scope, der zugleich die Sitzung ist</param>
        /// <param name="policy">die geltende Sicherheits-Policy, oder null fuer die des Scopes</param>
        /// <returns>der Wert eines return, sonst null</returns>
        public static object ParseBlock(string script, IScope scope, ScriptingPolicy policy = null)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return CompileBlock(script).Execute(scope, policy);
        }

        /// <summary>
        /// Uebersetzt ein ganzes Programm.
        /// </summary>
        /// <param name="script">der Quelltext</param>
        /// <returns>das uebersetzte Script</returns>
        public static CompiledScript CompileBlock(string script)
        {
            if (script == null)
            {
                throw new ArgumentNullException(nameof(script));
            }

            return compiledPrograms.GetOrAdd(script,
                key => new Lazy<CompiledScript>(() => BuildProgram(key))).Value;
        }

        /// <summary>
        /// Fuehrt ein ganzes Programm aus.
        /// </summary>
        /// <param name="script">der Quelltext</param>
        /// <param name="variables">die Startvariablen</param>
        /// <param name="scopeInitializer">ein Initialisierer fuer den Scope, oder null</param>
        /// <param name="policy">die geltende Sicherheits-Policy, oder null</param>
        /// <returns>der Wert eines return, sonst null</returns>
        public static object ParseBlock(string script, IDictionary<string, object> variables,
            InitializeScopeVariables scopeInitializer = null, ScriptingPolicy policy = null)
        {
            return CompileBlock(script).Execute(variables, scopeInitializer, policy);
        }

        /// <summary>
        /// Fuehrt ein Programm unter der Aufsicht eines Debuggers aus.
        /// </summary>
        /// <param name="script">der Quelltext</param>
        /// <param name="debugger">der Debugger, der Haltepunkte und Schritte steuert</param>
        /// <param name="variables">die Startvariablen</param>
        /// <param name="policy">die geltende Sicherheits-Policy, oder null</param>
        /// <returns>der Wert eines return, sonst null</returns>
        /// <remarks>
        /// Laeuft synchron im aufrufenden Thread. Haelt der Debugger an, blockiert dieser
        /// Aufruf, bis dessen Rueckruf entschieden hat, wie es weitergeht.
        /// </remarks>
        public static object Debug(string script, ScriptDebugger debugger,
            IDictionary<string, object> variables, ScriptingPolicy policy = null)
        {
            if (debugger == null)
            {
                throw new ArgumentNullException(nameof(debugger));
            }

            CompiledScript compiled = CompileBlock(script);
            var scope = new Scope(variables, policy);
            return debugger.Run(() => compiled.Execute(scope, policy, debugger));
        }

        /// <summary>
        /// Leert den Uebersetzungs-Zwischenspeicher.
        /// </summary>
        public static void ClearCache()
        {
            compiledExpressions.Clear();
            compiledPrograms.Clear();
        }

        private static CompiledScript Build(string expression)
        {
            ParserRuleContext tree = ExpressionParser.GetRawExpressionTree(expression,
                ExpressionParser.ExpressionMode.Expression);
            return new CompiledScript(new AstBuilder().BuildExpression(tree));
        }

        private static CompiledScript BuildProgram(string script)
        {
            var tree = (ITVScriptingParser.ProgramContext)ExpressionParser.GetRawExpressionTree(script,
                ExpressionParser.ExpressionMode.Program);
            return new CompiledScript(new AstBuilder().BuildProgram(tree));
        }
    }
}
