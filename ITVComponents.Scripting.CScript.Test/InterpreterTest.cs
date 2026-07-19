using System.Collections.Generic;
using System;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Scripting.CScript.Security.Extensions;
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
            // GetType() liefert die Klasse - fuer die Member des Type-Objekts braucht es $Type,
            // und das beherrscht nur der Interpreter. Siehe TypeAccessTest.
            Assert.AreEqual("Int32", ScriptInterpreter.Parse("value.GetType().$Type.Name",
                new Dictionary<string, object> { { "value", 1 } }));
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

        [TestMethod]
        public void ControlFlow()
        {
            AssertSameBlock(10, "x=0; while(x<10) { x=x+1; } return x;");
            AssertSameBlock(10, "x=0; do { x=x+1; } while(x<10); return x;");
            AssertSameBlock(45, "s=0; for(i=0;i<10;i=i+1) { s=s+i; } return s;");
            AssertSameBlock(6, "s=0; foreach(i in [1,2,3]) { s=s+i; } return s;");

            // break und continue
            AssertSameBlock(5, "x=0; while(true) { x=x+1; if(x==5) { break; } } return x;");
            AssertSameBlock(25, "s=0; for(i=0;i<10;i=i+1) { if(i%2==0) { continue; } s=s+i; } return s;");

            // return aus einer Schleife heraus
            AssertSameBlock(3, "for(i=0;i<10;i=i+1) { if(i==3) { return i; } } return -1;");

            // if/else
            AssertSameBlock("gross", "x=10; if(x>5) { return \"gross\"; } else { return \"klein\"; }");
            AssertSameBlock("klein", "x=1; if(x>5) { return \"gross\"; } else { return \"klein\"; }");

            // Ein Programm ohne return liefert null.
            AssertSameBlock(null, "x=1;");
        }

        [TestMethod]
        public void SwitchAndTry()
        {
            AssertSameBlock("zwei", "x=2; switch(x) { case 1: return \"eins\"; case 2: return \"zwei\"; }");
            AssertSameBlock("sonst", "x=9; switch(x) { case 1: return \"eins\"; default: return \"sonst\"; }");

            // Ausdrueckliches Fall-Through per continue.
            AssertSameBlock(2,
                "n=0; x=1; switch(x) { case 1: n=n+1; continue; case 2: n=n+1; break; } return n;");

            // catch ohne return aus dem catch heraus - das beherrschen beide Maschinen.
            AssertSameBlock(1, "x=0; try { throw \"boom\"; } catch(e) { x=1; } return x;");
            AssertSameBlock("boom", "m=0; try { throw \"boom\"; } catch(e) { m=e; } return m;");
            AssertSameBlock(3, "x=0; try { x=1; } finally { x=3; } return x;");
        }

        [TestMethod]
        public void ScopesFollowBlocks()
        {
            // Eine Zuweisung legt keine neue Variable an, sondern trifft die vorhandene im
            // aeusseren Scope - der Schleifenkopf ueberschreibt hier also das aeussere i, und
            // nach der Schleife steht dessen letzter Wert. Beide Maschinen tun das gleich.
            AssertSameBlock(2, "i=3; for(i=0;i<2;i=i+1) { } return i;");

            // Der Rumpf teilt den Scope mit dem Kopf: die Laufvariable ist darin sichtbar.
            AssertSameBlock(3, "s=0; for(i=1;i<3;i=i+1) { s=s+i; } return s;");
        }

        /// <summary>
        /// Faelle, die der ScriptVisitor nicht beherrscht - hier wird nur der Interpreter
        /// geprueft, mit Nachweis, dass die alte Maschine daran scheitert.
        /// </summary>
        [TestMethod]
        public void InterpreterFixesVisitorDefects()
        {
            // Mehrfach-Markierung: leerer case-Rumpf faellt in den naechsten durch.
            // Der ScriptVisitor lief hier in eine NullReferenceException.
            AssertInterpreterBlock("treffer",
                "x=2; switch(x) { case 1: case 2: return \"treffer\"; default: return \"daneben\"; }");

            // return aus einem catch nach einem Script-throw. Der ScriptVisitor verwarf jedes
            // Ergebnis des catch-Blocks ausser ReThrow - das return verschwand spurlos und die
            // Auswertung lieferte null.
            AssertInterpreterBlock(42, "try { throw \"x\"; } catch(e) { return 42; }");
            AssertInterpreterBlock("gefangen", "try { throw \"boom\"; } catch(e) { return \"gefangen\"; }");
            AssertInterpreterBlock("boom", "try { throw \"boom\"; } catch(e) { return e; }");
            Assert.IsNull(ExpressionParser.ParseBlock("try { throw \"x\"; } catch(e) { return 42; }",
                new Dictionary<string, object>()),
                "ScriptVisitor sollte das return aus dem catch weiterhin verschlucken.");

            // Ein throw ohne Ausdruck reicht die urspruengliche Nutzlast weiter. Beim
            // ScriptVisitor kam oben null an.
            AssertInterpreterBlock("boom",
                "try { try { throw \"boom\"; } catch(e) { throw; } } catch(e2) { return e2; }");

            // for ohne Kopfteile. Der abgeloeste Builder verlangte alle drei.
            AssertInterpreterBlock(3, "i=0; for(;;) { i=i+1; if(i==3) { break; } } return i;");
            AssertInterpreterBlock(4, "i=0; for(;i<4;) { i=i+1; } return i;");
        }

        [TestMethod]
        public void MisplacedJumpsAreRejectedWhileBuilding()
        {
            // Der Erbauer kennt die lexikalische Verschachtelung und lehnt diese Faelle ab,
            // bevor irgendetwas ausgefuehrt wird. Der ScriptVisitor merkte es erst zur
            // Laufzeit - und nur, wenn die Stelle auch erreicht wurde.
            Assert.ThrowsException<ScriptException>(() => ScriptInterpreter.CompileBlock("break;"));
            Assert.ThrowsException<ScriptException>(() => ScriptInterpreter.CompileBlock("continue;"));
            Assert.ThrowsException<ScriptException>(
                () => ScriptInterpreter.CompileBlock("try { } finally { return 1; }"));
            Assert.ThrowsException<ScriptException>(() => ScriptInterpreter.CompileBlock("throw;"));
        }

        [TestMethod]
        public void Functions()
        {
            AssertSameBlock(7, "function add(a,b) { return a+b; } return add(3,4);");
            AssertSameBlock(6, "f = function(x) { return x*2; }; return f(3);");

            // Eine Funktion ohne return liefert null.
            AssertSameBlock(null, "function nix() { x=1; } return nix();");

            // Fehlende Argumente werden zu null.
            AssertSameBlock(null, "function f(a) { return a; } return f();");

            // Die Funktion nimmt den umgebenden Zustand als Momentaufnahme mit.
            AssertSameBlock(1, "x=1; function f() { return x; } x=2; return f();");
        }

        /// <summary>
        /// Prueft, dass eine benannte Funktion sich selbst aufrufen kann.
        /// </summary>
        /// <remarks>
        /// Zuvor ging das nicht: eine Funktion nimmt ihren umgebenden Scope als Momentaufnahme
        /// mit, und die entstand, bevor die Funktion unter ihrem Namen abgelegt wurde - sie
        /// kannte sich selbst nicht. Seit FunctionLiteral den Namen kennt, bindet es sich
        /// darunter in seinen eigenen Scope. Beide Maschinen koennen es.
        /// </remarks>
        [TestMethod]
        public void Recursion()
        {
            AssertSameBlock(120,
                "function fac(n) { if(n<=1) { return 1; } return n*fac(n-1); } return fac(5);");

            // Wechselseitige Rekursion geht weiterhin nicht: die zuerst definierte Funktion
            // kennt die spaeter definierte nicht, weil deren Name zum Zeitpunkt der
            // Momentaufnahme noch nicht gebunden war.
            AssertSameBlock(8,
                "function fib(n) { if(n<2) { return n; } return fib(n-1)+fib(n-2); } return fib(6);");

            // Auch als benannter Funktionsausdruck.
            AssertSameBlock(120,
                "f = function fac(n) { if(n<=1) { return 1; } return n*fac(n-1); }; return f(5);");

            // Die Selbstbindung muss Copy() ueberleben - ein Objekt-Literal klont jede Methode.
            AssertSameBlock(120,
                "o = { fac: function fac(n) { if(n<=1) { return 1; } return n*fac(n-1); } }; return o.fac(5);");
        }

        [TestMethod]
        public void ObjectLiterals()
        {
            AssertSameBlock(5, "o = { a: 5 }; return o.a;");
            AssertSameBlock(9, "o = { a: 4, b: 5 }; return o.a + o.b;");

            // Eine Methode des Literals sieht die Geschwister-Eigenschaften ueber den
            // Elternscope, den das Literal ihr setzt.
            AssertSameBlock(12, "o = { faktor: 3, mal: function(x) { return x*faktor; } }; return o.mal(4);");
        }

        [TestMethod]
        public void FunctionsInterfaceWithSharedRuntime()
        {
            // Eine interpretierte Funktion muss dort funktionieren, wo die gemeinsame Runtime
            // auf FunctionLiteral prueft - sonst waere sie nur fuer den Interpreter brauchbar.
            var function = ScriptInterpreter.ParseBlock("return function(x) { return x*3; };",
                new Dictionary<string, object>());
            Assert.IsInstanceOfType<FunctionLiteral>(function,
                "Der Interpreter muss echte FunctionLiterals erzeugen.");
            Assert.AreEqual(9, ((FunctionLiteral)function).Invoke(new object[] { 3 }));

            // Copy() muss den Rumpf behalten - ObjectLiteral klont darueber jede Methode.
            FunctionLiteral copy = ((FunctionLiteral)function).Copy();
            Assert.AreEqual(12, copy.Invoke(new object[] { 4 }),
                "Der Klon muss denselben Rumpf ausfuehren wie das Original.");

            // Als Delegat verwendbar, etwa fuer Ereignis-Anbindung.
            var asDelegate = (Func<object, object>)((FunctionLiteral)function)
                .CreateDelegate(typeof(Func<object, object>));
            Assert.AreEqual(15, asDelegate(5));
        }

        [TestMethod]
        public void Increments()
        {
            AssertSameBlock(1, "x=0; x++; return x;");
            AssertSameBlock(0, "x=0; return x++;");
            AssertSameBlock(1, "x=0; return ++x;");
            AssertSameBlock(-1, "x=0; x--; return x;");
            AssertSameBlock(0, "x=0; return x--;");
            AssertSameBlock(-1, "x=0; return --x;");

            // Auf einem Element, nicht nur auf einer Variablen.
            AssertSameBlock(2, "a=[1,2,3]; a[0]++; return a[0];");
        }

        [TestMethod]
        public void NewInstances()
        {
            var vars = new Dictionary<string, object>
            {
                { "Dictionary", typeof(Dictionary<string, object>) },
                { "StringBuilder", typeof(System.Text.StringBuilder) }
            };

            AssertSameBlock(0, "d = new Dictionary(); return d.Count;", vars);
            AssertSameBlock("abc", "s = new StringBuilder(\"abc\"); return s.ToString();", vars);

            // Objekt-Initialisierer auf einer frischen Instanz.
            AssertSameBlock(5, "s = new StringBuilder() { Capacity: 5 }; return s.Capacity;", vars);
        }

        /// <summary>
        /// Prueft die Ausfuehrungsschalter. Bewusst nur gegen den Interpreter.
        /// </summary>
        /// <remarks>
        /// Diese Skripte duerfen nicht durch den ScriptVisitor laufen: dessen Schalter sind
        /// Instanzfelder, die ClearScope nicht zuruecksetzt, und InterpreterBuffer verwendet
        /// die Instanzen wieder - ein Pragma wuerde in nachfolgende, voellig unbeteiligte
        /// Auswertungen durchschlagen. Siehe PragmaStateLeaksAcrossVisitorRuns.
        /// </remarks>
        [TestMethod]
        public void Pragmas()
        {
            // Ohne Typpruefung wendet die Runtime die dynamischen Operatoren direkt an, mit
            // Typpruefung gleicht sie die Operanden vorher an. Bei decimal und float ist das
            // der Unterschied zwischen Ergebnis und Fehler.
            var vars = new Dictionary<string, object> { { "f", 99M }, { "e", .75F } };
            AssertInterpreterBlock(98.25M, "return f-e;", vars);
            Assert.ThrowsException<ScriptException>(
                () => ScriptInterpreter.ParseBlock("\"@@TYPESAFETY OFF\"; return f-e;", Copy(vars)),
                "Ohne Typpruefung muss die Operation an den unvereinbaren Typen scheitern.");

            // Der Schalter liefert weiterhin seinen Text als Wert.
            AssertInterpreterBlock("@@TYPESAFETY OFF", "return \"@@TYPESAFETY OFF\";");

            // Ein Schalter gilt ab seiner Stelle im Ablauf, nicht fuer das ganze Script: in
            // einem nicht genommenen Zweig wirkt er nicht.
            AssertInterpreterBlock(98.25M,
                "if(false) { \"@@TYPESAFETY OFF\"; } return f-e;", vars);

            // Der Inline-Cache darf am Ergebnis nichts aendern.
            AssertInterpreterBlock(6,
                "\"@@LAZYINVOKATION ON\"; s=0; for(i=0;i<3;i=i+1) { s=s+(i+1); } return s;");
        }

        /// <summary>
        /// Haelt einen Fehler des ScriptVisitors fest: ein Pragma wirkt ueber den Lauf hinaus.
        /// </summary>
        /// <remarks>
        /// typeSafety, lazyInvokation und bypassCompatibilityOnLazyInvokation sind Felder der
        /// Visitor-Instanz, und ClearScope setzt sie nicht zurueck. InterpreterBuffer poolt
        /// diese Instanzen - ein Script, das ein Pragma setzt, veraendert damit das Verhalten
        /// spaeterer, voellig unbeteiligter Auswertungen.
        ///
        /// Genau dagegen richtet sich der Umbau: der Interpreter haelt diese Schalter im
        /// ExecutionContext, der pro Lauf entsteht. Der zweite Teil des Tests weist nach, dass
        /// er nicht leckt.
        ///
        /// Der Visitor-Zustand wird am Ende wiederhergestellt, sonst wuerde dieser Test andere
        /// Tests im selben Lauf beschaedigen - was den Fehler zugleich anschaulich macht.
        /// </remarks>
        [TestMethod]
        public void PragmaStateLeaksAcrossVisitorRuns()
        {
            var vars = new Dictionary<string, object> { { "f", 99M }, { "e", .75F } };
            try
            {
                Assert.AreEqual(98.25M, ExpressionParser.Parse("f-e", Copy(vars)),
                    "Vor dem Pragma rechnet der ScriptVisitor mit Typpruefung.");

                ExpressionParser.ParseBlock("\"@@TYPESAFETY OFF\";", new Dictionary<string, object>());

                Assert.ThrowsException<ScriptException>(
                    () => ExpressionParser.Parse("f-e", Copy(vars)),
                    "Der ScriptVisitor traegt den Schalter in den naechsten Lauf hinueber.");

                // Der Interpreter ist davon unberuehrt - eigener Kontext pro Lauf.
                ScriptInterpreter.ParseBlock("\"@@TYPESAFETY OFF\";", new Dictionary<string, object>());
                Assert.AreEqual(98.25M, ScriptInterpreter.Parse("f-e", Copy(vars)),
                    "Der Interpreter darf den Schalter nicht in den naechsten Lauf tragen.");
            }
            finally
            {
                ExpressionParser.ParseBlock("\"@@TYPESAFETY ON\";", new Dictionary<string, object>());
            }
        }

        [TestMethod]
        public void ExplicitTypeHints()
        {
            // Typ-Hinweise waren im abgeloesten Builder wirkungslos, weil der Typpfad beim
            // Bauen verlorenging. Hier muss er ankommen.
            var vars = new Dictionary<string, object> { { "s", "abc" } };
            AssertSameBlock(3, "return s.Length;", vars);
        }

        [TestMethod]
        public void NativeScripting()
        {
            const string script =
                "`E(foo as list -> DEFAULT)::\"List<string>list = Global.list;" +
                "return list.FirstOrDefault(n => n.Equals((string)Global.search));\" with {search:\"schimmel\"}";
            var vars = new Dictionary<string, object>
            {
                { "foo", new List<string> { "hue", "ha", "ho", "halter", "horst", "schimmel" } }
            };

            AssertSame("schimmel", script, vars);

            // Die Policy-Pruefung liegt im Ausfuehrungspfad, nicht im Bauen: derselbe Baum kann
            // unter verschiedenen Policies laufen.
            var denied = ScriptingPolicy.Default.Configure(n => n.NativeScripting = PolicyMode.Deny);
            Assert.ThrowsException<ScriptSecurityException>(
                () => ScriptInterpreter.Parse(script, Copy(vars), policy: denied),
                "Natives Scripting muss von der Policy unterbunden werden koennen.");

            // Derselbe uebersetzte Baum laeuft ohne die Sperre weiterhin.
            Assert.AreEqual("schimmel", ScriptInterpreter.Parse(script, Copy(vars)),
                "Die Sperre darf nicht am Baum haengenbleiben.");
        }

        [TestMethod]
        public void NativeCodeBlock()
        {
            // Die Literal-Variante ohne Zielobjekt: der Code steht zwischen @# und #.
            const string script = "`E(#DEFAULT)::@#return Global.a + Global.b;# with {a:20,b:22}";
            AssertSame(42, script);
        }

        private static void AssertSameBlock(object expected, string script,
            IDictionary<string, object> variables = null)
        {
            object visitorResult = ExpressionParser.ParseBlock(script, Copy(variables));
            object interpreterResult = ScriptInterpreter.ParseBlock(script, Copy(variables));

            Assert.AreEqual(expected, visitorResult,
                $"ScriptVisitor liefert fuer '{script}' nicht den erwarteten Wert.");
            Assert.AreEqual(expected, interpreterResult,
                $"Interpreter liefert fuer '{script}' nicht den erwarteten Wert.");
            Assert.AreEqual(visitorResult, interpreterResult,
                $"Interpreter und ScriptVisitor weichen fuer '{script}' voneinander ab.");
        }

        private static void AssertInterpreterBlock(object expected, string script,
            IDictionary<string, object> variables = null)
        {
            Assert.AreEqual(expected, ScriptInterpreter.ParseBlock(script, Copy(variables)),
                $"Interpreter liefert fuer '{script}' nicht den erwarteten Wert.");
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
