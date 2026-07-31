using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    /// <summary>
    /// Haelt den Unterschied der beiden Einstiege fest - die Grundlage dafuer, dass CScript-Felder einer
    /// Workflow-Definition einen EXPLIZITEN Ausdruck/Block-Schalter brauchen und nicht einfach auf den
    /// maechtigeren Einstieg umgestellt werden koennen.
    /// </summary>
    [TestClass]
    public class BlockVsExpressionSemanticsTest
    {
        private static Dictionary<string, object> Vars()
            => new Dictionary<string, object> { { "TimeSpan", typeof(TimeSpan) }, { "x", 5 } };

        [TestMethod]
        public void Block_WithoutReturn_YieldsNull()
        {
            // DER Grund fuer den Schalter: wuerde man alle Ausdruecke pauschal als Block auswerten, waeren
            // alle bestehenden Definitionen schlagartig null - Bedingungen also false, und Prozesse
            // naehmen still den falschen Weg.
            Assert.IsNull(ScriptInterpreter.ParseBlock("1+2", Vars()));
            Assert.IsNull(ScriptInterpreter.ParseBlock("x>3", Vars()));
        }

        [TestMethod]
        public void Block_WithReturn_YieldsTheValue()
        {
            Assert.AreEqual(3, ScriptInterpreter.ParseBlock("return 1+2;", Vars()));
            Assert.AreEqual(true, ScriptInterpreter.ParseBlock("return x>3;", Vars()));
        }

        [TestMethod]
        public void Expression_WithABlockScript_SilentlyYieldsTheWrongValue()
        {
            // Kein Fehler, sondern ein falscher Wert: der Typ statt der TimeSpan. Genau deshalb muss der
            // Modus am Feld SICHTBAR sein - ein falsch gesetzter Schalter faellt sonst nirgends auf.
            object result = ScriptInterpreter.Parse("ts = TimeSpan; return new ts(0,0,10);", Vars());
            Assert.AreEqual(typeof(TimeSpan), result,
                "Parse wertet den Block NICHT aus - es kommt der Typ heraus, nicht die Zeitspanne.");
        }

        [TestMethod]
        public void Block_ResolvesATypeNameGivenAsString()
        {
            // Der reale Fall aus dem Workflow-Designer.
            Assert.AreEqual(new TimeSpan(0, 0, 10),
                ScriptInterpreter.ParseBlock("ts = 'System.TimeSpan'; return new ts(0,0,10);", Vars()));
        }
    }
}
