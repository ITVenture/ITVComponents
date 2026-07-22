using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ITVComponents.Scripting.CScript;

namespace ITVComponents.Scripting.CScript.Test
{
    /// <summary>
    /// Prueft das Laden und Ausfuehren von Scripts aus Dateien und Texten.
    /// </summary>
    /// <remarks>
    /// Prueft ITVComponents.Scripting.CScript.ScriptFile, das ueber den Interpreter ausfuehrt
    /// und den ScriptVisitor-basierten Vorgaenger abgeloest hat.
    /// </remarks>
    [TestClass]
    public class InterpreterScriptFileTest
    {
        private readonly List<string> tempFiles = new List<string>();

        [TestCleanup]
        public void Cleanup()
        {
            foreach (string file in tempFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException ex)
                {
                    // Aufraeumen darf den Testlauf nicht kippen - die Datei liegt im
                    // Temp-Verzeichnis und stoert dort niemanden.
                    Console.WriteLine($"Konnte Testdatei '{file}' nicht loeschen: {ex.Message}");
                }
            }

            tempFiles.Clear();
            ScriptFile<object>.ClearCache();
        }

        [TestMethod]
        public void RunsScriptFromText()
        {
            var script = ScriptFile<object>.FromText("a = 2; b = 3; return a*b;");
            Assert.AreEqual(6, script.Execute(new Dictionary<string, object>()));
        }

        [TestMethod]
        public void UsesProvidedVariables()
        {
            var script = ScriptFile<object>.FromText("return a+b;");
            Assert.AreEqual(7, script.Execute(new Dictionary<string, object> { { "a", 3 }, { "b", 4 } }));
        }

        [TestMethod]
        public void RunsScriptFromFile()
        {
            string path = WriteScript("return 21*2;");
            Assert.AreEqual(42, ScriptFile<object>.FromFile(path).Execute(new Dictionary<string, object>()));
        }

        [TestMethod]
        public void SameFileIsLoadedOnlyOnce()
        {
            string path = WriteScript("return 1;");
            Assert.AreSame(ScriptFile<object>.FromFile(path), ScriptFile<object>.FromFile(path),
                "Derselbe Pfad muss dieselbe Instanz liefern.");
        }

        [TestMethod]
        public void PicksUpChangesToTheFile()
        {
            string path = WriteScript("return 1;");
            var script = ScriptFile<object>.FromFile(path);
            Assert.AreEqual(1, script.Execute(new Dictionary<string, object>()));

            // Der Zeitstempel muss sich messbar unterscheiden.
            File.SetLastWriteTime(path, DateTime.Now.AddSeconds(-1));
            File.WriteAllText(path, "return 2;", Encoding.Default);
            File.SetLastWriteTime(path, DateTime.Now.AddSeconds(1));

            Assert.AreEqual(2, script.Execute(new Dictionary<string, object>()),
                "Eine geaenderte Datei muss beim naechsten Lauf uebernommen werden.");
        }

        [TestMethod]
        public void BrokenScriptReportsWhereItFailed()
        {
            var script = ScriptFile<object>.FromText("a = ;;;");
            var error = Assert.ThrowsException<ScriptException>(
                () => script.Execute(new Dictionary<string, object>()));
            Assert.IsTrue(error.Message.Contains("not runnable"),
                "Die Meldung soll sagen, dass das Script nicht lauffaehig ist.");
        }

        [TestMethod]
        public void ScriptsCanCallOtherScripts()
        {
            string helper = WriteScript("return x*2;");
            var script = ScriptFile<object>.FromText($"return Call(\"{helper.Replace("\\", "\\\\")}\", Dict([\"x\"],[21]));");

            Assert.AreEqual(42, script.Execute(new Dictionary<string, object>()),
                "Call muss ein anderes Script mit eigenen Variablen ausfuehren.");
        }

        [TestMethod]
        public void CalledScriptInheritsValuesOfTheCaller()
        {
            string helper = WriteScript("return faktor*3;");
            var script = ScriptFile<object>.FromText(
                $"return Call(\"{helper.Replace("\\", "\\\\")}\", Dict([],[]));");

            Assert.AreEqual(9, script.Execute(new Dictionary<string, object> { { "faktor", 3 } }),
                "Werte des rufenden Scripts muessen im gerufenen sichtbar sein.");
        }

        [TestMethod]
        public void ExecutesInAPreparedScope()
        {
            // Der Scope ist die Sitzung: wer ihn selbst aufbaut, kann ihn mehrfach benutzen und
            // sieht die Aenderungen des Scripts darin.
            var scope = new Scope(new Dictionary<string, object> { { "a", 5 } });
            ScriptFile<object>.Prepare(scope);

            var script = ScriptFile<object>.FromText("b = a*2; return b;");
            Assert.AreEqual(10, script.Execute(scope));
            Assert.AreEqual(10, scope["b"], "Das Script muss im uebergebenen Scope gearbeitet haben.");
        }

        [TestMethod]
        public void ReloadDoesNotDisturbRunningExecutions()
        {
            // Beim ScriptVisitor musste ein Neuladen warten, bis keine Ausfuehrung mehr laeuft -
            // es tauschte den Baum aus, den laufende Ausfuehrungen gerade begingen. Hier ist der
            // uebersetzte Stand unveraenderlich; ein Neuladen ist ein Referenzwechsel.
            string path = WriteScript("s=0; for(i=0;i<200;i=i+1) { s=s+1; } return s;");
            var script = ScriptFile<object>.FromFile(path);

            var runs = new Task<object>[8];
            for (int i = 0; i < runs.Length; i++)
            {
                runs[i] = Task.Run(() => script.Execute(new Dictionary<string, object>()));
            }

            File.SetLastWriteTime(path, DateTime.Now.AddSeconds(-1));
            File.WriteAllText(path, "s=0; for(i=0;i<200;i=i+1) { s=s+1; } return s;", Encoding.Default);
            File.SetLastWriteTime(path, DateTime.Now.AddSeconds(1));

            Task.WaitAll(runs);
            foreach (var run in runs)
            {
                Assert.AreEqual(200, run.Result,
                    "Ein Neuladen darf laufende Ausfuehrungen nicht stoeren.");
            }
        }

        /// <summary>
        /// Prueft, dass ein fehlgeschlagenes Neuladen den Betrieb nicht anhaelt.
        /// </summary>
        /// <remarks>
        /// Der praktische Fall ist ein Deployment: die Scriptdatei wird geschrieben, waehrend
        /// Scripts laufen. Sie ist dann kurz gesperrt oder halb geschrieben. Wuerde das
        /// Neuladen dabei durchschlagen, riesse es die laufende Ausfuehrung mit - ein
        /// funktionierendes Script wuerde also scheitern, weil nebenan ein anderes ausgerollt
        /// wird.
        ///
        /// Stattdessen bleibt der bisherige Stand aktiv, und der Fehlschlag wird protokolliert.
        /// Beim naechsten Lauf wird erneut versucht, weil der Zeitstempel unveraendert alt ist.
        /// </remarks>
        [TestMethod]
        public void FailedReloadKeepsTheWorkingVersion()
        {
            string path = WriteScript("return 1;");
            var script = ScriptFile<object>.FromFile(path);
            Assert.AreEqual(1, script.Execute(new Dictionary<string, object>()));

            // Datei aendern und exklusiv sperren - das Neuladen kommt nicht an sie heran.
            File.SetLastWriteTime(path, DateTime.Now.AddSeconds(-1));
            File.WriteAllText(path, "return 2;", Encoding.Default);
            File.SetLastWriteTime(path, DateTime.Now.AddSeconds(1));

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.AreEqual(1, script.Execute(new Dictionary<string, object>()),
                    "Bei gesperrter Datei muss der bisherige Stand weiterlaufen.");
            }

            // Sobald die Sperre weg ist, wird die Aenderung uebernommen.
            Assert.AreEqual(2, script.Execute(new Dictionary<string, object>()),
                "Nach dem Freigeben muss der neue Stand greifen.");
        }

        private string WriteScript(string content)
        {
            string path = Path.Combine(Path.GetTempPath(), $"itv-scripttest-{Guid.NewGuid():N}.its");
            File.WriteAllText(path, content, Encoding.Default);
            tempFiles.Add(path);
            return path;
        }
    }
}
