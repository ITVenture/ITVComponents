using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    /// <summary>
    /// Prueft den Zugriff auf das Type-Objekt selbst.
    /// </summary>
    /// <remarks>
    /// Ein Ausdruck, der einen Type liefert, steht im Scripting fuer seine Klasse: Zugriffe
    /// darauf sind statische Zugriffe. GetType() und 'System.String' verhalten sich also gleich,
    /// und daran aendert sich nichts.
    ///
    /// $Type wechselt die Strategie: ab dort ist das Type-Objekt selbst gemeint. Es ist damit
    /// kein gewoehnliches Member, sondern ein Sprachmittel, das sich zur Laufzeit anders
    /// verhaelt - der Erbauer faltet es in den folgenden Zugriff ein.
    ///
    /// Nur der Interpreter beherrscht das. Der ScriptVisitor hat keine Bauphase und kann den
    /// Wechsel nicht einfalten; er wird ohnehin abgeloest. Wo beide geprueft werden, ist es
    /// ausdruecklich vermerkt.
    /// </remarks>
    [TestClass]
    public class TypeAccessTest
    {
        private static Dictionary<string, object> Vars()
        {
            return new Dictionary<string, object>
            {
                { "value", 1 },
                { "text", "abc" },
                { "Math", typeof(Math) },
                { "nameOf", new Func<Type, string>(t => t.Name) }
            };
        }

        [TestMethod]
        public void StaticAccessIsUnchanged()
        {
            // Die Gegenprobe zuerst: der bisherige Sinn von Type darf sich nicht verschieben -
            // in beiden Maschinen.
            AssertBoth(Math.PI, "Math.PI");
            AssertBoth(int.MaxValue, "'System.Int32'.MaxValue");
        }

        /// <summary>
        /// Haelt fest, wie weit der Strategiewechsel reicht.
        /// </summary>
        /// <remarks>
        /// $Type ist ein Wechsel am Ort des Zugriffs, keine Umwandlung, die man speichern kann.
        /// Legt man das Ergebnis in einer Variablen ab, ist es wieder ein gewoehnlicher Type -
        /// und der steht fuer seine Klasse. Wer die Member des Type-Objekts will, schreibt
        /// $Type erneut unmittelbar davor.
        ///
        /// Das ist die Kehrseite davon, dass $Type kein Huellobjekt liefert: die Strategie
        /// haengt am Zugriff, nicht am Wert. Dafuer kann der Wert ueberallhin - in Variablen,
        /// als Argument, in Vergleiche - ohne als Fremdkoerper anzukommen.
        /// </remarks>
        [TestMethod]
        public void TheSwitchAppliesAtTheAccessNotToTheStoredValue()
        {
            // Gespeichert ist es wieder ein Type und steht damit fuer seine Klasse.
            Assert.AreEqual(string.Empty,
                ScriptInterpreter.ParseBlock("Fubar = 'System.String'.$Type; return Fubar.Empty;", Vars()),
                "Ein gespeicherter Type verhaelt sich wieder wie die Klasse.");

            Assert.ThrowsException<ScriptException>(
                () => ScriptInterpreter.ParseBlock("Fubar = 'System.String'.$Type; return Fubar.Name;", Vars()),
                "Name ist kein statisches Member von System.String - der Zugriff muss scheitern.");

            // Der Weg zum Type-Objekt bleibt offen: $Type direkt vor dem Zugriff.
            Assert.AreEqual("String",
                ScriptInterpreter.ParseBlock("Fubar = 'System.String'.$Type; return Fubar.$Type.Name;", Vars()),
                "$Type unmittelbar vor dem Zugriff schaltet wieder auf das Type-Objekt.");
        }

        [TestMethod]
        public void WithoutDollarTypeTheClassIsMeant()
        {
            // "Name" ist kein statisches Member von System.Int32 - der Zugriff muss scheitern.
            // Das ist gewollt: wer das Type-Objekt meint, schreibt $Type.
            AssertBothThrow("value.GetType().Name");
            AssertBothThrow("'System.Int32'.Name");
        }

        [TestMethod]
        public void DollarTypeSwitchesToTheTypeObject()
        {
            AssertInterpreter("Int32", "value.GetType().$Type.Name");
            AssertInterpreter("System.Int32", "value.GetType().$Type.FullName");
            AssertInterpreter("String", "'System.String'.$Type.Name");
            AssertInterpreter(true, "'System.Int32'.$Type.IsValueType");

            // Ohne Umweg ueber GetType(): $Type auf einem beliebigen Wert liefert dessen Typ.
            AssertInterpreter("Int32", "value.$Type.Name");
            AssertInterpreter("String", "text.$Type.Name");
        }

        [TestMethod]
        public void DollarTypeBeatsAStaticMemberOfTheSameName()
        {
            // Der Kern des Strategiewechsels: nach $Type wird gar nicht mehr statisch gesucht.
            // System.String hat ein statisches Empty; Type hat kein Empty - waere es ein
            // Rueckfall statt eines Wechsels, gewaenne hier das statische Member.
            AssertInterpreter("String", "'System.String'.$Type.Name");
            AssertBoth(string.Empty, "'System.String'.Empty");
        }

        [TestMethod]
        public void DollarTypeYieldsARealTypeAndDoesNotLeak()
        {
            // $Type liefert ein gewoehnliches Type-Objekt - kein Huellobjekt, das ueber
            // Zuweisungen und Argumente in den ganzen Wertebereich auswandern wuerde.
            object direct = ScriptInterpreter.Parse("value.$Type", Vars());
            Assert.IsInstanceOfType<Type>(direct, "$Type muss ein echtes Type-Objekt liefern.");
            Assert.AreEqual(typeof(int), direct);

            object assigned = ScriptInterpreter.ParseBlock("t = value.$Type; return t;", Vars());
            Assert.AreEqual(typeof(int), assigned, "Eine Zuweisung darf den Wert nicht veraendern.");

            AssertInterpreter("Int32", "nameOf(value.$Type)");
            AssertInterpreter(true, "value.$Type == 'System.Int32'");

            // Und nach einer Zuweisung gilt wieder die normale Strategie: t ist ein Type und
            // steht damit fuer seine Klasse.
            Assert.AreEqual(int.MaxValue,
                ScriptInterpreter.ParseBlock("t = value.$Type; return t.MaxValue;", Vars()));
        }

        private static void AssertBoth(object expected, string expression)
        {
            Assert.AreEqual(expected, ExpressionParser.Parse(expression, Vars()),
                $"ScriptVisitor liefert fuer '{expression}' nicht den erwarteten Wert.");
            Assert.AreEqual(expected, ScriptInterpreter.Parse(expression, Vars()),
                $"Interpreter liefert fuer '{expression}' nicht den erwarteten Wert.");
        }

        private static void AssertBothThrow(string expression)
        {
            Assert.ThrowsException<ScriptException>(() => ExpressionParser.Parse(expression, Vars()),
                $"ScriptVisitor sollte '{expression}' abweisen.");
            Assert.ThrowsException<ScriptException>(() => ScriptInterpreter.Parse(expression, Vars()),
                $"Interpreter sollte '{expression}' abweisen.");
        }

        private static void AssertInterpreter(object expected, string expression)
        {
            Assert.AreEqual(expected, ScriptInterpreter.Parse(expression, Vars()),
                $"Interpreter liefert fuer '{expression}' nicht den erwarteten Wert.");
        }
    }
}
