using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    /// <summary>
    /// Prueft den Interpreter gegen den ScriptVisitor.
    /// </summary>
    /// <remarks>
    /// Der Interpreter soll den Visitor ersetzen, nicht bloss aehnlich rechnen. Deshalb laeuft
    /// jeder Ausdruck durch beide Maschinen, und beide Ergebnisse muessen uebereinstimmen -
    /// zusaetzlich zum erwarteten Wert. Ein Test, der nur gegen den erwarteten Wert prueft,
    /// wuerde eine Abweichung im Randbereich uebersehen.
    ///
    /// Die Ausdruecke entsprechen denen aus Tests.TestMath, MixedTestsFromBach, TestLogic,
    /// HasTest und IsTest.
    /// </remarks>
    [TestClass]
    public class InterpreterTest
    {
        [TestMethod]
        public void TestMath()
        {
            AssertSame(1 + 2 * 3 / 4, "1+2*3/4");
            AssertSame((1 + 2) * 3 / 4, "(1+2)*3/4");
            AssertSame(1d + 2D * 3 / 4, "1+2D*3/4");
            AssertSame(1 + 2M * 3 / 4, "1+2M*3/4");
            AssertSame(1 + 2F * 3 / 4, "1+2F*3/4");

            // "value.GetType().Name" fehlt hier bewusst - siehe MemberAccessOnTypeIsBroken.
        }

        /// <summary>
        /// Haelt einen vorbestehenden Fehler der gemeinsamen Runtime fest.
        /// </summary>
        /// <remarks>
        /// "value.GetType().Name" wirft in beiden Maschinen "Member Name is not declared on".
        /// Ursache ist MemberAccessHelper.FindMember: liefert ein Ausdruck einen Type, wird
        /// der Folgezugriff als statischer Zugriff auf diesen Type gedeutet - gesucht wird
        /// also ein statisches Name auf System.Int32 statt der Eigenschaft Name des
        /// Type-Objekts.
        ///
        /// Der Fehler ist aelter als der Interpreter: er tritt unveraendert im Basis-Commit
        /// 0c9e016c auf und laesst dort bereits Tests.TestMath scheitern. Der Interpreter
        /// erbt ihn, weil er MemberAccessHelper bewusst unveraendert weiterverwendet.
        ///
        /// Dieser Test schlaegt fehl, sobald jemand FindMember repariert - dann gehoert die
        /// Zeile zurueck nach TestMath und dieser Test geloescht.
        /// </remarks>
        [TestMethod]
        public void MemberAccessOnTypeIsBroken()
        {
            var vars = new Dictionary<string, object> { { "value", 1 } };
            Assert.ThrowsException<ScriptException>(
                () => ExpressionParser.Parse("value.GetType().Name", Copy(vars)),
                "ScriptVisitor sollte weiterhin scheitern.");
            Assert.ThrowsException<ScriptException>(
                () => ScriptInterpreter.Parse("value.GetType().Name", Copy(vars)),
                "Interpreter sollte dasselbe Verhalten zeigen wie der ScriptVisitor.");
        }

        [TestMethod]
        public void MixedTestsFromBach()
        {
            var a = 10L;
            int b = 50;
            short c = 5;
            var d = .5;
            var e = .75F;
            var f = 99M;
            var g = true;
            var h = false;
            var vars = new Dictionary<string, object>
            {
                { "a", a }, { "b", b }, { "c", c }, { "d", d },
                { "e", e }, { "f", f }, { "g", g }, { "h", h }
            };

            AssertSame(a + b, "a+b", vars);
            AssertSame(a * b, "a*b", vars);
            AssertSame(a - b, "a-b", vars);
            AssertSame(a / b, "a/b", vars);
            AssertSame(a ^ b, "a^b", vars);
            AssertSame(-a * b, "-a*b", vars);
            AssertSame(a * -b, "a*-b", vars);
            AssertSame(a << b, "a<<b", vars);
            AssertSame(a >> b, "a>>b", vars);
            AssertSame(a % b, "a%b", vars);
            AssertSame(a & b, "a&b", vars);
            AssertSame(a | b, "a|b", vars);
            AssertSame(!g || b > c && h ^ f - (decimal)e > 90, "!g||b>c&&h^f-e>90", vars);
            AssertSame(a + b * c + (decimal)d - (decimal)e * f, "a+b*c+d-e*f", vars);
            AssertSame(a | b ^ a & b | ~a ^ ~b, "a | b ^ a & b | ~a ^ ~b", vars);
            AssertSame(g | h ^ g & h | !g ^ !h, "g|h^g&h|!g^!h", vars);
        }

        [TestMethod]
        public void TestLogic()
        {
            // Die Division durch null darf nie ausgefuehrt werden - beide Maschinen muessen
            // kurzschliessen, sonst wirft der Ausdruck.
            AssertSame(false, "1 + 2 > 3 && 3 / 0 != 5");
            AssertSame(true, "1 + 2 == 3 && 3 / 1 != 5");
            AssertSame(false, "1 + 2 > 3 || 3 / 1 == 5");
            AssertSame(true, "1 + 2 == 3 || 3 / 0 == 5");
        }

        [TestMethod]
        public void HasTest()
        {
            AssertSame(true, "12 has ToString()");
            AssertSame(false, "12 has HornDampf(\"TEST\")");
            AssertSame(false, "12 has Length");
            AssertSame(true, "[] has Length");
        }

        [TestMethod]
        public void IsTest()
        {
            AssertSame(true, "12 is 'System.Int32'");
            AssertSame(true, "[12] is 'System.Array'");
            AssertSame(false, "12 is 'System.Array'");
        }

        [TestMethod]
        public void CompiledScriptIsReusable()
        {
            // Derselbe uebersetzte Baum muss mit unterschiedlichen Variablen laufen. Beim
            // ScriptVisitor ging das nur ueber den Instanz-Pool, weil der Zustand an der
            // Visitor-Instanz hing; hier steckt er im ExecutionContext.
            var compiled = ScriptInterpreter.Compile("a*b");
            Assert.AreEqual(6, compiled.Execute(new Dictionary<string, object> { { "a", 2 }, { "b", 3 } }));
            Assert.AreEqual(20, compiled.Execute(new Dictionary<string, object> { { "a", 4 }, { "b", 5 } }));
        }

        private static void AssertSame(object expected, string expression,
            IDictionary<string, object> variables = null)
        {
            object visitorResult = ExpressionParser.Parse(expression, Copy(variables));
            object interpreterResult = ScriptInterpreter.Parse(expression, Copy(variables));

            Assert.AreEqual(expected, visitorResult,
                $"ScriptVisitor liefert fuer '{expression}' nicht den erwarteten Wert.");
            Assert.AreEqual(expected, interpreterResult,
                $"Interpreter liefert fuer '{expression}' nicht den erwarteten Wert.");
            Assert.AreEqual(visitorResult, interpreterResult,
                $"Interpreter und ScriptVisitor weichen fuer '{expression}' voneinander ab.");
        }

        private static Dictionary<string, object> Copy(IDictionary<string, object> variables)
        {
            return variables == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(variables);
        }
    }
}
