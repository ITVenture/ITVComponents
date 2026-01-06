using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Json;
using ITVComponents.Scripting.CScript.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    [TestClass]
    public class InterpreterTest
    {
        [TestMethod]
        public void TestInterpreter()
        {
            var x = ExpressionInterpreter.ReadScriptFile(@"C:\temp\fubarmp3\books\Script1.its");
            JsonHelper.WriteObject(x,SerializationTypingMode.NativePolymorphism, @"C:\temp\fubarmp3\books\Script1.tree", true);
        }
    }
}
