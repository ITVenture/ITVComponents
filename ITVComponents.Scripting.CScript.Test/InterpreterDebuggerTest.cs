using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ITVComponents.Scripting.CScript;
using ITVComponents.Scripting.CScript.Runtime.Debugging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    /// <summary>
    /// Prueft die Debugger-Naht: anhalten, schrittweise weiterlaufen, Variablen ansehen.
    /// </summary>
    /// <remarks>
    /// Das ist der Zweck des ganzen Umbaus - beim ScriptVisitor gab es keine Stelle, an der man
    /// zwischen zwei Anweisungen haette eingreifen koennen.
    /// </remarks>
    [TestClass]
    public class InterpreterDebuggerTest
    {
        private const string CountingScript =
            "x = 1;\n" +
            "y = 2;\n" +
            "z = x + y;\n" +
            "return z;";

        [TestMethod]
        public void WithoutDebuggerNothingIsObserved()
        {
            // Die Naht darf im Normalbetrieb nichts kosten: ohne Debugger wird nicht beobachtet.
            Assert.AreEqual(3, ScriptInterpreter.ParseBlock(CountingScript, new Dictionary<string, object>()));
        }

        [TestMethod]
        public void BreakpointStopsAtLine()
        {
            var stops = new List<DebugStop>();
            var debugger = new ScriptDebugger(stop =>
            {
                stops.Add(stop);
                return DebugCommand.Continue;
            });
            debugger.AddBreakpoint(3);

            object result = ScriptInterpreter.Debug(CountingScript, debugger, new Dictionary<string, object>());

            Assert.AreEqual(3, result, "Das Script muss trotz Haltepunkt zu Ende laufen.");
            Assert.AreEqual(1, stops.Count, "Genau ein Halt auf Zeile 3 erwartet.");
            Assert.AreEqual(3, stops[0].Position.Line);
            Assert.AreEqual("Breakpoint", stops[0].Reason);
        }

        [TestMethod]
        public void VariablesAreVisibleAtTheBreakpoint()
        {
            DebugStop captured = null;
            var debugger = new ScriptDebugger(stop =>
            {
                captured = stop;
                return DebugCommand.Continue;
            });
            debugger.AddBreakpoint(3);

            ScriptInterpreter.Debug(CountingScript, debugger, new Dictionary<string, object>());

            Assert.IsNotNull(captured);
            // Auf Zeile 3 sind x und y zugewiesen, z noch nicht.
            Assert.AreEqual(1, captured.Variables["x"]);
            Assert.AreEqual(2, captured.Variables["y"]);
            Assert.IsFalse(captured.Variables.ContainsKey("z"),
                "z darf vor der Ausfuehrung von Zeile 3 noch nicht existieren.");
        }

        [TestMethod]
        public void SteppingWalksStatementByStatement()
        {
            var lines = new List<int>();
            var debugger = new ScriptDebugger(stop =>
            {
                lines.Add(stop.Position.Line);
                return DebugCommand.StepInto;
            });
            debugger.BreakOnEntry();

            ScriptInterpreter.Debug(CountingScript, debugger, new Dictionary<string, object>());

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, lines,
                "Der Einzelschritt muss jede Anweisung der Reihe nach treffen.");
        }

        [TestMethod]
        public void StepIntoEntersFunctionsAndBuildsCallStack()
        {
            const string script =
                "function add(a,b) {\n" +
                "  s = a + b;\n" +
                "  return s;\n" +
                "}\n" +
                "r = add(3,4);\n" +
                "return r;";

            var insideFunction = new List<DebugStop>();
            var debugger = new ScriptDebugger(stop =>
            {
                if (stop.CallStack.Count > 1)
                {
                    insideFunction.Add(stop);
                }

                return DebugCommand.StepInto;
            });
            debugger.BreakOnEntry();

            object result = ScriptInterpreter.Debug(script, debugger, new Dictionary<string, object>());

            Assert.AreEqual(7, result);
            Assert.AreNotEqual(0, insideFunction.Count,
                "Der Einzelschritt muss in den Funktionsrumpf hineinfuehren.");

            DebugStop inside = insideFunction[0];
            Assert.AreEqual("add", inside.CallStack[0].FunctionName,
                "Der innerste Rahmen muss die aufgerufene Funktion sein.");
            Assert.IsNull(inside.CallStack[inside.CallStack.Count - 1].FunctionName,
                "Der aeusserste Rahmen ist die oberste Ebene und hat keinen Funktionsnamen.");

            // Die Parameter sind im Rahmen der Funktion sichtbar.
            Assert.AreEqual(3, inside.CallStack[0].Variables["a"]);
            Assert.AreEqual(4, inside.CallStack[0].Variables["b"]);
        }

        [TestMethod]
        public void StepOverSkipsFunctionBodies()
        {
            const string script =
                "function add(a,b) {\n" +
                "  return a + b;\n" +
                "}\n" +
                "r = add(3,4);\n" +
                "return r;";

            int deepestStack = 0;
            var debugger = new ScriptDebugger(stop =>
            {
                deepestStack = Math.Max(deepestStack, stop.CallStack.Count);
                return DebugCommand.StepOver;
            });
            debugger.BreakOnEntry();

            ScriptInterpreter.Debug(script, debugger, new Dictionary<string, object>());

            Assert.AreEqual(1, deepestStack,
                "Bei StepOver darf der Debugger nie in einem Funktionsrumpf anhalten.");
        }

        [TestMethod]
        public void StopAbortsExecution()
        {
            var debugger = new ScriptDebugger(stop => DebugCommand.Stop);
            debugger.AddBreakpoint(2);

            Assert.ThrowsExactly<ScriptAbortedException>(
                () => ScriptInterpreter.Debug(CountingScript, debugger, new Dictionary<string, object>()),
                "Ein Abbruch muss die Ausfuehrung beenden.");
        }

        [TestMethod]
        public void SnapshotSurvivesSerialization()
        {
            // Fuer entferntes Debuggen wandert die Momentaufnahme ueber die IPC-Schnittstelle,
            // waehrend das Script in seinem Dienst weiterlaeuft. Sie muss deshalb aus reinen
            // Daten bestehen und keine lebenden Verweise auf den Scope enthalten.
            DebugStop captured = null;
            var debugger = new ScriptDebugger(stop =>
            {
                captured = stop;
                return DebugCommand.Continue;
            });
            debugger.AddBreakpoint(3);

            ScriptInterpreter.Debug(CountingScript, debugger, new Dictionary<string, object>());

            var transferable = new
            {
                Line = captured.Position.Line,
                captured.Reason,
                Frames = captured.CallStack
                    .Select(f => new { f.FunctionName, Variables = f.Variables.ToDictionary(v => v.Key, v => v.Value) })
                    .ToArray()
            };

            string json = JsonSerializer.Serialize(transferable);
            Assert.IsTrue(json.Contains("\"Line\":3"), "Die Position muss uebertragbar sein.");
            Assert.IsTrue(json.Contains("\"x\":1"), "Die Variablen muessen uebertragbar sein.");
        }

        [TestMethod]
        public void SnapshotIsDetachedFromLiveState()
        {
            // Die Aufnahme darf sich nicht mehr aendern, wenn das Script weiterlaeuft - sonst
            // zeigte eine entfernte Oberflaeche einen Zustand, den es nie gab.
            DebugStop captured = null;
            var debugger = new ScriptDebugger(stop =>
            {
                captured ??= stop;
                return DebugCommand.Continue;
            });
            debugger.AddBreakpoint(2);

            ScriptInterpreter.Debug(CountingScript, debugger, new Dictionary<string, object>());

            Assert.AreEqual(1, captured.Variables["x"]);
            Assert.IsFalse(captured.Variables.ContainsKey("z"),
                "Die Aufnahme von Zeile 2 darf spaetere Zuweisungen nicht nachtraeglich zeigen.");
        }
    }
}
