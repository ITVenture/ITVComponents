using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    /// <summary>
    /// Prueft den Zugriff auf das Type-Objekt selbst.
    /// </summary>
    /// <remarks>
    /// Ein Ausdruck, der einen Type liefert, steht im Scripting fuer die Klasse: Memberzugriffe
    /// darauf sind statische Zugriffe. Das ist gewollt und bleibt so. Es fehlte aber der Weg
    /// zum Type-Objekt selbst - "value.GetType().Name" suchte ein statisches Name auf
    /// System.Int32 und scheiterte.
    ///
    /// Geprueft wird gegen beide Maschinen: die Aenderung liegt in der gemeinsamen Runtime.
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
        public void StaticAccessKeepsPriority()
        {
            // Die Gegenprobe zuerst: der bisherige Sinn von Type darf sich nicht verschieben.
            AssertBoth(Math.PI, "Math.PI");
            AssertBoth(int.MaxValue, "'System.Int32'.MaxValue");
        }

        [TestMethod]
        public void TypeMembersAreReachable()
        {
            AssertBoth("Int32", "value.GetType().Name");
            AssertBoth("System.Int32", "value.GetType().FullName");
            AssertBoth("String", "text.GetType().Name");
        }

        [TestMethod]
        public void DollarTypeWorksOnAnyValue()
        {
            // Ohne Umweg ueber GetType().
            AssertBoth("Int32", "value.$Type.Name");
            AssertBoth("String", "text.$Type.Name");

            // Auf einem Type liefert $Type diesen Typ selbst.
            AssertBoth("Math", "Math.$Type.Name");
        }

        [TestMethod]
        public void DollarTypeYieldsARealTypeAndDoesNotLeak()
        {
            // Der entscheidende Punkt: was $Type liefert, muss ein echtes Type-Objekt sein.
            // Ein Huellobjekt wuerde ueber Zuweisungen und Argumente in den ganzen Wertebereich
            // auswandern, und die bestehenden Auspack-Stellen decken nur Methodenargumente ab.
            object direct = ScriptInterpreter.Parse("value.$Type", Vars());
            Assert.IsInstanceOfType<Type>(direct, "$Type muss ein echtes Type-Objekt liefern.");
            Assert.AreEqual(typeof(int), direct);

            // Ueber eine Zuweisung hinweg.
            object assigned = ScriptInterpreter.ParseBlock("t = value.$Type; return t;", Vars());
            Assert.AreEqual(typeof(int), assigned, "Eine Zuweisung darf den Wert nicht veraendern.");

            // Als Argument an eine .NET-Methode.
            AssertBoth("Int32", "nameOf(value.$Type)");

            // Und im Vergleich.
            AssertBoth(true, "value.$Type == 'System.Int32'");
        }

        [TestMethod]
        public void ExistenceCanBeChecked()
        {
            AssertBoth(true, "value has $Type");
        }

        /// <summary>
        /// Prueft einen Ausdruck gegen beide Maschinen.
        /// </summary>
        private static void AssertBoth(object expected, string expression)
        {
            Assert.AreEqual(expected, ExpressionParser.Parse(expression, Vars()),
                $"ScriptVisitor liefert fuer '{expression}' nicht den erwarteten Wert.");
            Assert.AreEqual(expected, ScriptInterpreter.Parse(expression, Vars()),
                $"Interpreter liefert fuer '{expression}' nicht den erwarteten Wert.");
        }
    }
}
