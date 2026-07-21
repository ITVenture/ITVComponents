using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Interpreter.Ast;
using ITVComponents.Scripting.CScript.Interpreter.Ast.Building;
using ITVComponents.Scripting.CScript.Interpreter.Runtime;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Interpreter
{
    /// <summary>
    /// Ein Script aus einer Datei, einem Datenstrom oder einem Text.
    /// </summary>
    /// <typeparam name="TOutput">der Ergebnistyp des Scripts</typeparam>
    /// <remarks>
    /// Gegenstueck zu ITVComponents.Scripting.CScript.ScriptFile, das auf dem ScriptVisitor
    /// aufsetzt. Gleicher Zweck, gleiche Einstiegspunkte - aber ohne dessen Sperrwerk.
    ///
    /// Bewusst anders benannt als das Original: waehrend der Umstellung stehen beide
    /// nebeneinander, und ein gleicher Name in verschiedenen Namensraeumen zwaenge jede Datei,
    /// die beide sieht, zu Aliassen oder voll qualifizierten Namen.
    ///
    /// Beim ScriptVisitor musste ein Neuladen warten, bis keine Ausfuehrung mehr laeuft: es
    /// tauschte den ANTLR-Baum aus, den laufende Ausfuehrungen gerade begehen. Hier ist das
    /// uebersetzte Script unveraenderlich, ein Neuladen also ein Referenzwechsel. Laufende
    /// Ausfuehrungen behalten ihren Stand, neue nehmen den neuen - Laufzaehler und Wartesperre
    /// entfallen ersatzlos.
    /// </remarks>
    public class InterpretedScriptFile<TOutput>
    {
        /// <summary>
        /// Zwischenspeicher der aus Dateien geladenen Scripts, mit dem Pfad als Schluessel.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Lazy<InterpretedScriptFile<TOutput>>> bufferedScripts =
            new ConcurrentDictionary<string, Lazy<InterpretedScriptFile<TOutput>>>();

        private readonly string fileName;
        private readonly Stream source;

        /// <summary>
        /// Gibt an, ob die Quelle unveraenderlich ist - dann entfaellt die Pruefung auf
        /// Aenderungen.
        /// </summary>
        private readonly bool isStatic;

        private readonly object reloadLock = new object();

        /// <summary>
        /// Der aktuelle Uebersetzungsstand. Wird beim Neuladen als Ganzes ersetzt, nie im
        /// Einzelnen veraendert - deshalb sehen laufende Ausfuehrungen immer einen in sich
        /// stimmigen Stand.
        /// </summary>
        private volatile Definition definition;

        private InterpretedScriptFile(string fileName, Stream source, bool isStatic)
        {
            this.fileName = fileName;
            this.source = source;
            this.isStatic = isStatic;
            Reload();
        }

        /// <summary>
        /// Laedt ein Script aus einer Datei. Aenderungen an der Datei werden uebernommen.
        /// </summary>
        public static InterpretedScriptFile<TOutput> FromFile(string fileName)
        {
            return bufferedScripts.GetOrAdd(fileName,
                key => new Lazy<InterpretedScriptFile<TOutput>>(() => new InterpretedScriptFile<TOutput>(key, null, false))).Value;
        }

        /// <summary>
        /// Laedt ein Script aus einem Datenstrom.
        /// </summary>
        public static InterpretedScriptFile<TOutput> FromStream(Stream file)
        {
            return new InterpretedScriptFile<TOutput>(null, file, true);
        }

        /// <summary>
        /// Laedt ein Script aus einem Text.
        /// </summary>
        public static InterpretedScriptFile<TOutput> FromText(string scriptText)
        {
            var stream = new MemoryStream(Encoding.Default.GetBytes(scriptText));
            stream.Seek(0, SeekOrigin.Begin);
            return FromStream(stream);
        }

        /// <summary>
        /// Leert den Zwischenspeicher der geladenen Scripts.
        /// </summary>
        public static void ClearCache()
        {
            bufferedScripts.Clear();
        }

        /// <summary>
        /// Fuehrt das Script mit den angegebenen Variablen aus.
        /// </summary>
        /// <param name="variables">die Startvariablen</param>
        /// <param name="prepareVariables">
        /// ein Initialisierer fuer den Scope, oder null fuer die Standard-Rueckrufe
        /// </param>
        /// <param name="policy">die geltende Sicherheits-Policy, oder null</param>
        /// <returns>das Ergebnis des Scripts</returns>
        public TOutput Execute(IDictionary<string, object> variables,
            InitializeScopeVariables prepareVariables = null, ScriptingPolicy policy = null)
        {
            IScope scope = variables as IScope ?? new Scope(variables, policy);
            Prepare(scope, prepareVariables, policy);
            return Execute(scope, policy);
        }

        /// <summary>
        /// Fuehrt das Script in einem bereits vorbereiteten Scope aus.
        /// </summary>
        /// <param name="scope">der Scope, der zugleich die Sitzung ist</param>
        /// <param name="policy">die geltende Sicherheits-Policy, oder null</param>
        /// <param name="observer">ein Debugger, oder null</param>
        /// <returns>das Ergebnis des Scripts</returns>
        /// <remarks>
        /// Der Scope ist die Sitzung: verschachtelte Aufrufe brauchen keinen eigenen
        /// Sitzungsbegriff, weil alles, was sie mitbekommen muessen, ohnehin im Scope steht.
        /// Beim ScriptVisitor war dafuer ein RunnerItem aus dem Instanzen-Pool noetig.
        /// </remarks>
        public TOutput Execute(IScope scope, ScriptingPolicy policy = null,
            IExecutionObserver observer = null)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            CheckForChanges();

            // Genau einmal lesen: ein Neuladen mitten im Lauf darf den Stand nicht unter der
            // laufenden Ausfuehrung wegziehen.
            Definition current = definition;
            if (!current.Runnable)
            {
                throw new ScriptException(
                    $"Script is not runnable! Suspect Line: {current.SuspectLine}{Environment.NewLine}" +
                    $"Complete Error-List:{Environment.NewLine} {current.Errors}");
            }

            object result = current.Script.Execute(scope, policy, observer);
            return Convert(result);
        }

        /// <summary>
        /// Bestueckt einen Scope mit den Standard-Rueckrufen und den Script-Aufrufen.
        /// </summary>
        /// <param name="scope">der zu bestueckende Scope</param>
        /// <param name="prepareVariables">ein zusaetzlicher Initialisierer, oder null</param>
        /// <param name="policy">die geltende Sicherheits-Policy, oder null</param>
        public static void Prepare(IScope scope, InitializeScopeVariables prepareVariables = null,
            ScriptingPolicy policy = null)
        {
            // Der Scope ist die Sitzung; die Standard-Rueckrufe erwarten dafuer etwas
            // Wegwerfbares.
            using (var session = new ScopeSession(scope))
            {
                if (prepareVariables != null)
                {
                    prepareVariables(new ScopePreparationCallbackArguments(scope, session, null));
                }
                else
                {
                    DefaultCallbacks.PrepareDefaultCallbacks(scope, session);
                }
            }

            PrepareScriptCalls(scope, prepareVariables, policy);
        }

        /// <summary>
        /// Stellt Dict, Call und Run bereit, mit denen ein Script andere Scripts aufruft.
        /// </summary>
        /// <remarks>
        /// Wird nach den Standard-Rueckrufen gesetzt und ueberschreibt deren Run absichtlich:
        /// jenes fuehrt verschachtelte Scripts ueber den ScriptVisitor aus, hier sollen sie im
        /// Interpreter laufen.
        /// </remarks>
        private static void PrepareScriptCalls(IScope scope, InitializeScopeVariables prepareVariables,
            ScriptingPolicy policy)
        {
            scope["Dict"] = new Func<object[], object[], Dictionary<string, object>>(Dict);
            scope["Call"] = new Func<string, IDictionary<string, object>, object>(
                (script, variables) => CallScript(script, variables, prepareVariables, policy, scope.CopyInitial()));
            scope["Run"] = new Func<string, object>(
                script => InterpretedScriptFile<object>.FromFile(script).Execute(scope, policy));
        }

        /// <summary>
        /// Ruft ein Script aus einem Script heraus auf.
        /// </summary>
        /// <remarks>
        /// Die Werte des rufenden Scripts werden ergaenzt, aber nie ueberschrieben - was der
        /// Aufrufer ausdruecklich mitgibt, hat Vorrang.
        /// </remarks>
        private static object CallScript(string scriptFile, IDictionary<string, object> initialVariables,
            InitializeScopeVariables prepareVariables, ScriptingPolicy policy,
            IDictionary<string, object> baseValues)
        {
            bool callerOwnsScope = initialVariables is IScope;
            IDictionary<string, object> construct = callerOwnsScope
                ? initialVariables
                : new Dictionary<string, object>(initialVariables ?? new Dictionary<string, object>());
            try
            {
                foreach (KeyValuePair<string, object> item in baseValues)
                {
                    if (!construct.ContainsKey(item.Key))
                    {
                        construct[item.Key] = item.Value;
                    }
                }

                return InterpretedScriptFile<object>.FromFile(scriptFile).Execute(construct, prepareVariables, policy);
            }
            finally
            {
                if (!callerOwnsScope)
                {
                    construct.Clear();
                }
            }
        }

        /// <summary>
        /// Baut aus Namen und Werten ein Woerterbuch, das als Scope dienen kann.
        /// </summary>
        private static Dictionary<string, object> Dict(object[] keys, object[] values)
        {
            var retVal = new Dictionary<string, object>();
            for (int i = 0; i < keys.Length; i++)
            {
                retVal.Add((string)keys[i], values[i]);
            }

            return retVal;
        }

        /// <summary>
        /// Bringt das Ergebnis auf den erwarteten Typ.
        /// </summary>
        /// <remarks>
        /// Ein Objekt-Literal wird auf eine Schnittstelle abgebildet, wenn eine solche erwartet
        /// wird - dasselbe tut ScriptValueHelper fuer den ScriptVisitor.
        /// </remarks>
        private static TOutput Convert(object value)
        {
            if (value is ObjectLiteral literal && typeof(TOutput).IsInterface)
            {
                return (TOutput)literal.Cast(typeof(TOutput));
            }

            return (TOutput)value;
        }

        /// <summary>
        /// Uebernimmt Aenderungen an der Quelldatei.
        /// </summary>
        private void CheckForChanges()
        {
            if (isStatic)
            {
                return;
            }

            // Wer die Sperre nicht bekommt, laeuft mit dem bisherigen Stand weiter - ein
            // anderer Aufrufer laedt gerade ohnehin neu.
            if (!Monitor.TryEnter(reloadLock, 100))
            {
                return;
            }

            try
            {
                var info = new FileInfo(fileName);
                if (!info.Exists || info.LastWriteTime <= definition.CompiledAt)
                {
                    return;
                }

                try
                {
                    Reload();
                }
                catch (Exception ex)
                {
                    // Ein fehlgeschlagenes Neuladen darf die laufende Ausfuehrung nicht
                    // mitreissen. Typischer Fall: die Datei wird gerade geschrieben, also
                    // waehrend eines Deployments - dann ist sie kurz gesperrt oder halb
                    // geschrieben. Der bisherige Stand bleibt gueltig und laeuft weiter; beim
                    // naechsten Lauf wird erneut versucht, weil der Zeitstempel unveraendert
                    // alt ist.
                    LogEnvironment.LogEvent(
                        $"Konnte Script '{fileName}' nicht neu laden, der bisherige Stand bleibt aktiv: " +
                        $"{ex.OutlineException()}", LogSeverity.Warning);
                }
            }
            finally
            {
                Monitor.Exit(reloadLock);
            }
        }

        /// <summary>
        /// Uebersetzt die Quelle neu und tauscht den Stand aus.
        /// </summary>
        private void Reload()
        {
            ITVScriptingParser.ProgramContext tree = isStatic
                ? ExpressionParser.GetExpressionTreeFromFile(source, out bool runnable, out int suspectLine,
                    out string errors)
                : ExpressionParser.GetExpressionTreeFromFile(fileName, out runnable, out suspectLine, out errors);

            CompiledScript script = null;
            if (runnable)
            {
                script = new CompiledScript(new AstBuilder().BuildProgram(tree));
            }

            // Ein Zuweisen, kein schrittweises Aendern: laufende Ausfuehrungen behalten den
            // Stand, mit dem sie begonnen haben.
            definition = new Definition(script, DateTime.Now, runnable, suspectLine, errors);
        }

        /// <summary>
        /// Ein in sich abgeschlossener Uebersetzungsstand.
        /// </summary>
        private sealed class Definition
        {
            public Definition(CompiledScript script, DateTime compiledAt, bool runnable, int suspectLine,
                string errors)
            {
                Script = script;
                CompiledAt = compiledAt;
                Runnable = runnable;
                SuspectLine = suspectLine;
                Errors = errors;
            }

            public CompiledScript Script { get; }

            public DateTime CompiledAt { get; }

            public bool Runnable { get; }

            public int SuspectLine { get; }

            public string Errors { get; }
        }

        /// <summary>
        /// Reicht den Scope als Sitzung durch.
        /// </summary>
        /// <remarks>
        /// Die Standard-Rueckrufe verlangen etwas Wegwerfbares als Sitzung. Der Interpreter
        /// braucht dafuer keinen eigenen Zustand - alles Noetige steht im Scope.
        /// </remarks>
        private sealed class ScopeSession : IDisposable
        {
            public ScopeSession(IScope scope)
            {
                Scope = scope;
            }

            public IScope Scope { get; }

            public void Dispose()
            {
            }
        }
    }
}
