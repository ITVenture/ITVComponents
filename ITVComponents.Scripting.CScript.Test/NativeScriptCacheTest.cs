using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core.Native;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    /// <summary>
    /// Der Puffer der kompilierten Skripte: gleicher Text heisst gleiche Kompilierung.
    /// </summary>
    /// <remarks>
    /// Frueher galt jeder zweite Aufruf mit demselben Label als Aenderung und leerte den ganzen Puffer. Jede
    /// neue Kompilierung kostet Zeit und laesst eine Assembly im Speicher zurueck, die nie mehr frei wird.
    /// </remarks>
    [TestClass]
    public class NativeScriptCacheTest
    {
        private static Lazy<ScriptRunner<object>> Script()
            => new Lazy<ScriptRunner<object>>(() => (globals, token) => Task.FromResult<object>(null));

        private static Lazy<ScriptRunner<object>> FailingScript()
            => new Lazy<ScriptRunner<object>>(() => throw new InvalidOperationException("Absicht"));

        private static int InMemoryAssemblies()
            => AppDomain.CurrentDomain.GetAssemblies().Count(a => !a.IsDynamic && a.Location.Length == 0);

        private static string UniqueConfiguration()
            => $"CACHETEST-{Guid.NewGuid():N}";

        [TestMethod]
        public void SameLabelAndText_KeepsTheCompiledScript()
        {
            var config = new NativeConfiguration();
            Lazy<ScriptRunner<object>> first = config.GetOrAddScript("A", "hash-a", Script());

            for (int i = 0; i < 5; i++)
            {
                Assert.AreSame(first, config.GetOrAddScript("A", "hash-a", Script()));
            }
        }

        [TestMethod]
        public void RepeatingOneLabel_KeepsTheOtherScripts()
        {
            var config = new NativeConfiguration();
            Lazy<ScriptRunner<object>> other = config.GetOrAddScript("B", "hash-b", Script());
            config.GetOrAddScript("A", "hash-a", Script());
            config.GetOrAddScript("A", "hash-a", Script());

            Assert.AreSame(other, config.GetOrAddScript("B", "hash-b", Script()));
        }

        [TestMethod]
        public void ChangedText_ReplacesOnlyTheScriptOfThatLabel()
        {
            var config = new NativeConfiguration();
            Lazy<ScriptRunner<object>> other = config.GetOrAddScript("B", "hash-b", Script());
            Lazy<ScriptRunner<object>> original = config.GetOrAddScript("A", "hash-a", Script());

            Lazy<ScriptRunner<object>> changed = config.GetOrAddScript("A", "hash-a2", Script());

            Assert.AreNotSame(original, changed);
            Assert.AreSame(other, config.GetOrAddScript("B", "hash-b", Script()));
            Assert.AreNotSame(original, config.GetOrAddScript("A", "hash-a", Script()),
                "Die alte Fassung gehoert nicht mehr in den Puffer.");
        }

        /// <summary>Eine neue Referenz kann das Ergebnis jeder Kompilierung aendern - das bleibt ein Grund zum Leeren.</summary>
        [TestMethod]
        public void NewReference_StillClearsTheCache()
        {
            var config = new NativeConfiguration();
            Lazy<ScriptRunner<object>> first = config.GetOrAddScript("A", "hash-a", Script());

            config.AddReference("Some.New.Assembly");

            Assert.AreNotSame(first, config.GetOrAddScript("A", "hash-a", Script()));
        }

        /// <summary>
        /// Ein <see cref="Lazy{T}"/> behaelt seine Ausnahme. Ohne eigene Pruefung bliebe ein Fehlschlag gepuffert,
        /// auch wenn seine Ursache laengst behoben ist.
        /// </summary>
        [TestMethod]
        public void FailedCompilation_IsRetried()
        {
            var config = new NativeConfiguration();
            Lazy<ScriptRunner<object>> failed = config.GetOrAddScript("A", "hash-a", FailingScript());
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = failed.Value);

            Lazy<ScriptRunner<object>> retry = Script();

            Assert.AreSame(retry, config.GetOrAddScript("A", "hash-a", retry));
            Assert.IsNotNull(retry.Value);
        }

        [TestMethod]
        public void RunningAnUnchangedScriptAgain_CompilesNothing()
        {
            string configuration = UniqueConfiguration();
            var noArguments = new Dictionary<string, object>();
            NativeScriptHelper.RunLinqQuery(configuration, "A", "return 1;", noArguments);
            NativeScriptHelper.RunLinqQuery(configuration, "B", "return 2;", noArguments);
            int before = InMemoryAssemblies();

            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(1, NativeScriptHelper.RunLinqQuery(configuration, "A", "return 1;", noArguments));
                Assert.AreEqual(2, NativeScriptHelper.RunLinqQuery(configuration, "B", "return 2;", noArguments));
            }

            Assert.AreEqual(before, InMemoryAssemblies());
        }

        /// <summary>Das, wofuer das Label da ist: eine angepasste Abfrage wirkt beim naechsten Lauf.</summary>
        [TestMethod]
        public void ChangedScriptText_TakesEffectOnTheNextRun()
        {
            string configuration = UniqueConfiguration();
            var noArguments = new Dictionary<string, object>();

            Assert.AreEqual(1, NativeScriptHelper.RunLinqQuery(configuration, "A", "return 1;", noArguments));
            Assert.AreEqual(2, NativeScriptHelper.RunLinqQuery(configuration, "A", "return 2;", noArguments));
        }

        [TestMethod]
        public void CompilingAnUnchangedExpressionAgain_CompilesNothing()
        {
            string configuration = UniqueConfiguration();
            NativeScriptHelper.CompileExpression<Func<int, bool>>(configuration, "A", "x => x > 0");
            int before = InMemoryAssemblies();

            for (int i = 0; i < 6; i++)
            {
                Expression<Func<int, bool>> expression =
                    NativeScriptHelper.CompileExpression<Func<int, bool>>(configuration, "A", "x => x > 0");
                Assert.IsTrue(expression.Compile()(1));
            }

            Assert.AreEqual(before, InMemoryAssemblies());
        }

        /// <summary>Ein wiederholtes Label hat frueher auch die Usings der Konfiguration verworfen.</summary>
        [TestMethod]
        public void RepeatedExpressionLabel_KeepsTheUsings()
        {
            string configuration = UniqueConfiguration();
            NativeScriptHelper.AddUsing(configuration, "System.Text");
            const string text = "x => new StringBuilder().Length == x";

            NativeScriptHelper.CompileExpression<Func<int, bool>>(configuration, "A", text);
            NativeScriptHelper.CompileExpression<Func<int, bool>>(configuration, "A", text);
            Expression<Func<int, bool>> changed = NativeScriptHelper.CompileExpression<Func<int, bool>>(
                configuration, "A", "x => new StringBuilder().Length != x");

            Assert.IsTrue(changed.Compile()(1));
        }
    }
}
