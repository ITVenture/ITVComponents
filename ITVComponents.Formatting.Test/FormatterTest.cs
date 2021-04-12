using System;
using System.Collections.Generic;
using ITVComponents.Formatting.ScriptExtensions;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Formatting.Test
{
    [TestClass]
    public class FormatterTest
    {
        [TestMethod]
        public void TestBaseFormats()
        {
            var Target = new
            {
                Val1 = "TEST123",
                Val2 = "HORN",
                Val3 = DateTime.Today
            };
            Assert.AreEqual(string.Format("Das ist ein TEST123. Von weitem klingt ein HORN. Heute ist der {0:dd.MM.yyyy}",DateTime.Today),
                Target.FormatText("Das ist ein [Val1]. Von weitem klingt ein [Val2]. Heute ist der [Val3:dd.MM.yyyy]"));
        }

        [TestMethod]
        public void TestDirectMethods()
        {
            var directFibonacci = new { Fib = new Func<int,int>((int ct) =>{
                if(ct <= 1) {
                    return ct;
                }
                int fib = 1;
                int prevFib = 1;
		
                for(int i=2; i<ct; i++) {
                    int temp = fib;
                    fib+= prevFib;
                    prevFib = temp;
                }
                return fib;
            }),
                Seed=6
            };

            Assert.AreEqual("Result: 008", directFibonacci.FormatText("Result: [Fib(Seed):000]"));
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
                Val3 = DateTime.Today
            };
            var differ = Target.Val3.Subtract(DateTime.Parse("01.01.1900")).TotalDays;
            Assert.AreEqual(string.Format(@"Das ist ein TEST123. Von weitem klingt ein HORN. Heute ist der {0:dd.MM.yyyy}
Seit dem 1.1.1900 sind {1,-10} Tage vergangen. HORN ist 4 Zeichen lang und TEST123 ist 7 Zeichen lang.", DateTime.Today,differ),
                Target.FormatText(@"Das ist ein [Val1]. Von weitem klingt ein [Val2]. Heute ist der [Val3:dd.MM.yyyy]
Seit dem 1.1.1900 sind $[dateTime = 'System.DateTime'
dt1 = dateTime.Parse(""01.01.1900"");
return Val3.Subtract(dt1).TotalDays;,-10] Tage vergangen. [Val2] ist £[Val2L] Zeichen lang und [Val1] ist $£[return Val1L;] Zeichen lang."));
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
            var differ = (long)Target.Val3.Subtract(DateTime.Parse("01.01.1900")).TotalDays;
            Assert.AreEqual(string.Format(@"Das ist ein TEST123. Von weitem klingt ein HORN. Heute ist der {0:dd.MM.yyyy}
Seit dem 1.1.1900 sind {1,-10:x} Tage vergangen. HORN ist 4 Zeichen lang und TEST123 ist 7 Zeichen lang.", DateTime.Today, differ),
                Target.FormatText(@"Das ist ein [Val1]. Von weitem klingt ein [Val2]. Heute ist der [Val3:dd.MM.yyyy]
Seit dem 1.1.1900 sind $[dateTime = 'System.DateTime'
dt1 = dateTime.Parse(""01.01.1900"");
return 'System.Convert'.ToInt64(Val3.Subtract(dt1).TotalDays);,-10:x] Tage vergangen. [Val2] ist £[Val2L] Zeichen lang und [Val1] ist $£[return Val1L;] Zeichen lang."));
        }

        [TestMethod]
        public void TestObjFormatting()
        {
            var Target = new
            {
                Hicks = new Dictionary<string, object> { {"v1",1234}, { "abvv2", DateTime.Parse("01.01.2017")}, { "asdfv3", "haha"}, { "v4", 1234 }, { "hohov5", null } }
            };
            Assert.AreEqual(@"Hicks besteht aus: ""abvv2  = 01.01.2017 00:00:00
asdfv3 = haha
hohov5 = 
v1     = 1234
v4     = 1234""",
                Target.FormatText(@"£[@""Hicks besteht aus: """"[Hicks:kv]""""""]"));
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
                {"hohov5", null}
            };
            
            Assert.AreEqual(@"abvv2  = 01.01.2017 00:00:00
asdfv3 = haha
hohov5 = 
v1     = 1234
v4     = 12341234",
                Hicks.FormatText(@"[.:kv][v4][dasgibtsgarnicht]"));

            Assert.AreEqual(@"haha",
                Hicks.FormatText(@"[$data[""asdfv3""]]"));
        }

        [TestMethod]
        public void TestScripting()
        {
            ScriptExtensionHelper.Register();
            Assert.AreEqual("01.01.2018",
                ExpressionParser.Parse("$$(\"[date:dd.MM.yyyy]\")",
                    new Dictionary<string, object> {{"date", new DateTime(2018, 1, 1)}}));
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
            var differ = (long)Target.Val3.Subtract(DateTime.Parse("01.01.1900")).TotalDays;
            Assert.AreEqual(string.Format(@"Das ist ein TEST123. Von weitem klingt ein HORN. Heute ist der {0:dd.MM.yyyy}
Seit dem 1.1.1900 sind {1,-10:x} Tage vergangen. HORN ist £[Val2.Length] Zeichen lang und TEST123 ist $7 Zeichen lang.", DateTime.Today, differ),
                Target.FormatText(@"Das ist ein [Val1]. Von weitem klingt ein [Val2]. Heute ist der [Val3:dd.MM.yyyy]
Seit dem 1.1.1900 sind $[dateTime = 'System.DateTime'
dt1 = dateTime.Parse(""01.01.1900"");
return 'System.Convert'.ToInt64(Val3.Subtract(dt1).TotalDays);,-10:x] Tage vergangen. [Val2] ist ££[Val2L] Zeichen lang und [Val1] ist $$£[Val1L] Zeichen lang."));
        }

        [TestMethod]
        public void TestInlineValues()
        {
            PasswordSecurity.InitializeAes("Weneee e die Russn kommn!");
            string encrypted = "bladibla".Encrypt();
            var decrypted = new object().FormatText($"[\"{encrypted}\":decrypt]");
            Assert.AreEqual(decrypted,"bladibla");
        }
        
        [TestMethod]
        public void TestOriginInlineValues()
        {
            //PasswordSecurity.InitializeAes("Weneeee die Russn kommn!");
            var decrypted = "IBDPdW6+ENeoseBcPa79++s5sFpt3HDsXNDhfFR4JI+sgez1FFWgVct9g6nGYPYvg5dnL+seC3wtDn9ZHCJh4qNa".Decrypt("Isch werd' hier nosch zum Alki! Bald... *hicks*");
            Assert.AreEqual(decrypted,"gintonic");
        }
    }
}
