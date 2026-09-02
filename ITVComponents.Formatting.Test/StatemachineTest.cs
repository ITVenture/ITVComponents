using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting.Parser;
using ITVComponents.Formatting.ScriptExtensions;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Security.Extensions;
using ITVComponents.Scripting.CScript.Security.Restrictions;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Security;

namespace ITVComponents.Formatting.Test
{
    [TestClass]
    public class StatemachineTest
    {
        public StatemachineTest()
        {
            new
            {
                Val1 = "TEST123",
                Val2 = "HORN",
                Val3 = DateTime.Today
            }.FormatText("Das ist ein [Val1]. Von weitem klingt ein [Val2]. Heute ist der [Val3:dd.MM.yyyy]");
        }

        [TestMethod]
        public void TestTokenizer()
        {
            var tokenizer = new StringFormatParser();
            var tokens = tokenizer.TokenizeString("Hello World!");
            var tokens2 = tokenizer.TokenizeString("Hello [[World]]!");
            var tokens3 = tokenizer.TokenizeString("Hallo [Schnork(World(123,ficken[2])?1:2,\"fuckoff)]\")] horst $[22,-12:000] £[honk:dd.MM.yyyy]{2} $£[return honk:dd.MM.yyyy]{3}-");
            Assert.AreEqual(tokens.Length, tokens2.Length);
            Assert.AreEqual("Hello [World]!", tokens2[0].Content.ToString());
            Assert.AreEqual(9, tokens3.Length);
        }

        [TestMethod]
        public void TestBaseFormats()
        {
            var tokenizer = new StringFormatParser();
            var Target = new
            {
                Val1 = "TEST123",
                Val2 = "HORN",
                Val3 = DateTime.Today
            };
            var format = "Das ist ein [Val1]. Von weitem klingt ein [Val2]. Heute ist der [Val3:dd.MM.yyyy]";
            Assert.AreEqual(tokenizer.FormatString(Target,format,null,null),
                Target.FormatText(format));
        }

        [TestMethod]
        public void TestDirectMethods()
        {
            var directFibonacci = new
            {
                Fib = new Func<int, int>((int ct) => {
                    if (ct <= 1)
                    {
                        return ct;
                    }
                    int fib = 1;
                    int prevFib = 1;

                    for (int i = 2; i < ct; i++)
                    {
                        int temp = fib;
                        fib += prevFib;
                        prevFib = temp;
                    }
                    return fib;
                }),
                Seed = 6
            };

            var format = "Result: [Fib(Seed):000]";
            var tokenizer = new StringFormatParser();
            Assert.AreEqual(tokenizer.FormatString(directFibonacci, format, null, null),
                directFibonacci.FormatText(format));
        }

        [TestMethod]
        public void TestForSO()
        {
            var tokenizer = new StringFormatParser();
            var tmp = "Fruit with name '[error]' does not exist.";
            var tmp2 = "'[error]' has [error.Length] letters, which is an [error.Length%2==0?\"\":\"un\"]-even number.";
            var error = "Apple";
            Assert.AreEqual(tokenizer.FormatString(new{error},tmp,null,null), new { error }.FormatText(tmp));
            Assert.AreEqual(tokenizer.FormatString(new { error }, tmp2, null, null), new { error }.FormatText(tmp2));
        }


        [TestMethod]
        public void TestEntireScript()
        {
            var Target = new
            {
                Val1 = "TEST123",
                Val1L = "[Val1?.Length]",
                Val2 = "HORN",
                Val2L = "[Val2.Length]",
                Val3 = DateTime.Today,
                LengthProp = "Length"
            };
            var format = @"Das ist ein [Val1]. Von weitem klingt ein [Val2]. Heute ist der [Val3:dd.MM.yyyy]
Seit dem 1.1.1900 sind $[dateTime = 'System.DateTime'
dt1 = dateTime.Parse(""01.01.1900"");
return Val3.Subtract(dt1).TotalDays;,-10] Tage vergangen. [Val2] ist £[Val2L] Zeichen (oder auch £[""lng:[[Val2.[LengthProp]]]""]{2}) lang und [Val1] ist $£[return Val1L;] Zeichen lang.";
            var tokenizer = new StringFormatParser();
            Assert.AreEqual(tokenizer.FormatString(Target, format,null,null, TextFormat.DefaultFormatPolicyWithPrimitives),
                Target.FormatText(format, TextFormat.DefaultFormatPolicyWithPrimitives));
        }

        [TestMethod]
        public void TestLengthFormatting()
        {
            var Target = new
            {
                Val1 = "TEST123",
                Val1L = "[Val1.Length]",
                Val2 = "HORN",
                Val2L = "[Val2.Length]",
                Val3 = DateTime.Today
            };
            var poli = TextFormat.DefaultFormatPolicyWithPrimitives
                .WithTypeRestriction(typeof(Convert), TypeAccessMode.Direct | TypeAccessMode.StaticMethod,
                    PolicyMode.Allow);
            var tokenizer = new StringFormatParser();
            var format = @"Das ist ein [Val1]. Von weitem klingt ein [Val2]. Heute ist der [Val3:dd.MM.yyyy]
Seit dem 1.1.1900 sind $[dateTime = 'System.DateTime'
dt1 = dateTime.Parse(""01.01.1900"");
return 'System.Convert'.ToInt64(Val3.Subtract(dt1).TotalDays);,-10:x] Tage vergangen. [Val2] ist £[Val2L] Zeichen lang und [Val1] ist $£[return Val1L;] Zeichen lang.";
            Assert.AreEqual(tokenizer.FormatString(Target,format,null,null, poli),
                Target.FormatText(format, poli));
        }

        [TestMethod]
        public void TestObjFormatting()
        {
            var Target = new
            {
                Hicks = new Dictionary<string, object> { { "v1", 1234 }, { "abvv2", DateTime.Parse("01.01.2017") }, { "asdfv3", "haha" }, { "v4", 1234 }, { "hohov5", null } }
            };
            var tokenizer = new StringFormatParser();
            var format = @"£[@""Hicks besteht aus: """"[Hicks:kv]""""""]";
            string s1 = tokenizer.FormatString(Target,format,null,null);
            string s2 = Target.FormatText(format);
            Assert.AreEqual(s1,
                s2);
        }

        [TestMethod]
        public void TestSelfFormatting()
        {
            var Hicks = new Dictionary<string, object>
            {
                {"v1", 1234},
                {"abvv2", DateTime.Parse("01.01.2017")},
                {"asdfv3", "haha"},
                {"v4", 1234},
                {"hohov5", null},
                {"honk", new[]{"fubar"}}
            };
            var format1 = @"[.:kv][v4][dasgibtsgarnicht]";
            var format2 = @"[$data[""asdfv3""]]";
            var format3 = @"a[(honk.Length!=0?honk[0].Substring(2,1):"""")]a";
            var format4 = @"[v1 has Length]";
            var tokenizer = new StringFormatParser();
            Assert.AreEqual(tokenizer.FormatString(Hicks,format1,null,null),
                Hicks.FormatText(format1));

            Assert.AreEqual(tokenizer.FormatString(Hicks,format2,null,null),
                Hicks.FormatText(format2));

            Assert.AreEqual(tokenizer.FormatString(Hicks,format3,null,null),
                Hicks.FormatText(format3));

            Assert.AreEqual(tokenizer.FormatString(Hicks,format4,null,null),
                Hicks.FormatText(format4));
        }

        [TestMethod]
        public void TestScripting()
        {
            ScriptExtensionHelper.Register();
            Assert.AreEqual("01.01.2018",
                ExpressionParser.Parse("$$(\"[date:dd.MM.yyyy]\")",
                    new Dictionary<string, object> { { "date", new DateTime(2018, 1, 1) } }));
        }

        [TestMethod]
        public void TestEscaping()
        {
            var Target = new
            {
                Val1 = "TEST123",
                Val1L = "[Val1.Length]",
                Val2 = "HORN",
                Val2L = "[Val2.Length]",
                Val3 = DateTime.Today
            };
            var pol = TextFormat.DefaultFormatPolicyWithPrimitives
                .WithTypeRestriction(typeof(Convert), TypeAccessMode.Direct | TypeAccessMode.StaticMethod,
                    PolicyMode.Allow);
            var format = @"Das ist ein [Val1]. Von weitem klingt ein [Val2]. Heute ist der [Val3:dd.MM.yyyy]
Seit dem 1.1.1900 sind $[dateTime = 'System.DateTime'
dt1 = dateTime.Parse(""01.01.1900"");
return 'System.Convert'.ToInt64(Val3.Subtract(dt1).TotalDays);,-10:x] Tage vergangen. [Val2] ist ££[Val2L] Zeichen lang und [Val1] ist $$£[Val1L] Zeichen lang.";
            var tokenizer = new StringFormatParser();
            Assert.AreEqual(tokenizer.FormatString(Target,format,null,null,pol),
                Target.FormatText(format,
                    pol));
        }

        [TestMethod]
        public void TestInlineValues()
        {
            PasswordSecurity.InitializeAes("Weneee e die Russn kommn!");
            string encrypted = "bladibla".Encrypt();
            string format = $"[\"{encrypted}\":decrypt]";
            var tokenizer = new StringFormatParser();
            var decrypted = new object().FormatText(format);
            var decrypted2 = tokenizer.FormatString(tokenizer, format, null, null);
            Assert.AreEqual(decrypted, decrypted2);
        }

        [TestMethod]
        public void TestScriptDenial()
        {
            var t = new
            {
                Object = typeof(object),
                List = new List<string> { "Hü", "Ha", "Ho" }
            };
            var tokenizer = new StringFormatParser();

            // Auf ScriptException einschliesslich Unterklassen pruefen: seit die Formatierung
            // ueber den Interpreter ausfuehrt, kommt die Verweigerung als ScriptSecurityException
            // (Unterklasse von ScriptException) unverpackt an, statt wie beim ScriptVisitor als
            // generische ScriptException. Auf den genauen Subtyp festzunageln waere bruechig.
            void AssertDenied(Func<object> format)
            {
                try
                {
                    format();
                }
                catch (ScriptException)
                {
                    return;
                }

                Assert.Fail("Die Formatierung haette abgewiesen werden muessen.");
            }

            AssertDenied(() => tokenizer.FormatString(t, "[new Object()]", null, null).ToString());
            AssertDenied(() => tokenizer.FormatString(t,
                "[(`E(t as t->DEFAULT)::\"var t = Global.t;return t.List.First();\" with {})]", null, null).ToString());
            AssertDenied(() => tokenizer.FormatString(t, "['System.Object']", null, null).ToString());
        }
    }
}
