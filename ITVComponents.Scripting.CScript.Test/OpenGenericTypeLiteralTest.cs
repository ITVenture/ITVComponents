using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    /// <summary>
    /// Prueft die offene Form des Typ-Literals: 'System.Collections.Generic.List`1'.
    /// </summary>
    /// <remarks>
    /// Der Backtick ist ein eigenes Token der Grammatik (typeArguments: OpenGenerics), kein
    /// Zeichen im Namen. Am Typ-Literal ist die offene Definition ein zulaessiges Ergebnis -
    /// geschlossen wird sie erst dort, wo die Argumente stehen.
    ///
    /// Der abgeloeste ScriptVisitor konnte das (er haengte den Text der Stelligkeit an den
    /// Namen); der neue Erbauer schickte anfangs auch das Typ-Literal durch die Pruefung, die
    /// nur fuer Aufrufe und Konstruktionen gedacht war, und wies damit new Liste&lt;#t&gt;()
    /// ab. Diese Tests halten den wiederhergestellten Stand fest.
    /// </remarks>
    [TestClass]
    public class OpenGenericTypeLiteralTest
    {
        private static Dictionary<string, object> Vars()
        {
            return new Dictionary<string, object>
            {
                { "t", typeof(string) },
                { "k", typeof(int) }
            };
        }

        [TestMethod]
        public void TheOpenDefinitionIsAValidTypeLiteral()
        {
            Assert.AreEqual(typeof(List<>),
                ScriptInterpreter.Parse("'System.Collections.Generic.List`1'", Vars()),
                "Die offene Definition muss sich als Typ-Literal lesen lassen.");

            Assert.AreEqual(typeof(Dictionary<,>),
                ScriptInterpreter.Parse("'System.Collections.Generic.Dictionary`2'", Vars()),
                "Auch mehrstellige Definitionen muessen aufloesen.");
        }

        [TestMethod]
        public void TheOpenDefinitionCanBeClosedOnConstruction()
        {
            // Der eigentliche Anwendungsfall: die Definition steht in einer Variablen, die
            // Argumente erst an der Erzeugung.
            object list = ScriptInterpreter.ParseBlock(
                "Liste = 'System.Collections.Generic.List`1'; return new Liste<#t>();", Vars());
            Assert.IsInstanceOfType<List<string>>(list, "new Liste<#t>() muss eine List<string> liefern.");

            object map = ScriptInterpreter.ParseBlock(
                "Map = 'System.Collections.Generic.Dictionary`2'; return new Map<#t,#k>();", Vars());
            Assert.IsInstanceOfType<Dictionary<string, int>>(map,
                "Mehrere Typargumente muessen in der gegebenen Reihenfolge schliessen.");
        }

        [TestMethod]
        public void ClosingAtTheLiteralKeepsWorking()
        {
            // Die Gegenprobe: die geschlossene Form am Literal darf sich nicht verschieben.
            Assert.AreEqual(typeof(List<string>),
                ScriptInterpreter.Parse("'System.Collections.Generic.List<#t>'", Vars()));

            Assert.IsInstanceOfType<List<string>>(
                ScriptInterpreter.Parse("new 'System.Collections.Generic.List<#t>'()", Vars()));
        }

        [TestMethod]
        public void TheOpenFormRemainsRefusedWhereArgumentsAreExpected()
        {
            // In einem Aufruf ist die offene Form weiterhin ein Fehler: dort sind die
            // Typargumente die Angabe selbst, eine Stelligkeit sagt nichts aus.
            var ex = Assert.ThrowsException<ScriptException>(
                () => ScriptInterpreter.ParseBlock(
                    "Liste = new 'System.Collections.Generic.List<#t>'(); return Liste.ConvertAll`1();", Vars()),
                "Ein Methodenaufruf mit offener Stelligkeit muss abgewiesen werden.");
            StringAssert.Contains(ex.Message, "Open Generic Arguments",
                "Die Abweisung muss aus der Pruefung des Erbauers stammen, nicht aus einem Syntaxfehler.");
        }
    }
}
