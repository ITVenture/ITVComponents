using System;
using System.Collections.Generic;
using System.Text;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.Scripting.CScript.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void TestMath()
        {
            Assert.AreEqual(1 + 2 * 3 / 4, ExpressionParser.Parse("1+2*3/4", new Dictionary<string, object>()));
            //Assert.AreEqual(1 + 3 * 3 / 4, ExpressionParser.Parse("1+2*3/4", new Dictionary<string, object>()));
            Assert.AreEqual((1+2)*3 / 4, ExpressionParser.Parse("(1+2)*3/4", new Dictionary<string, object>()));
            Assert.AreEqual(1d + 2D * 3 / 4, ExpressionParser.Parse("1+2D*3/4", new Dictionary<string, object>()));
            Assert.AreEqual(1 + 2M * 3 / 4, ExpressionParser.Parse("1+2M*3/4", new Dictionary<string, object>()));
            Assert.AreEqual(1 + 2F * 3 / 4, ExpressionParser.Parse("1+2F*3/4", new Dictionary<string, object>()));
        }

        [TestMethod]
        public void MixedTestsFromBach()
        {
            var a = 10L;
            int b =  50;
            short c = 5;
            var d = .5;
            var e = .75F;
            var f = 99M;
            var g = true;
            var h = false;
            Dictionary<string, object> vars = new Dictionary<string, object>
            {
                {"a", a},
                {"b", b},
                {"c", c},
                {"d", d},
                {"e", e},
                {"f", f},
                {"g", g},
                {"h", h}
            };
            Assert.AreEqual(a + b, ExpressionParser.Parse("a+b", vars));
            Assert.AreEqual(a * b, ExpressionParser.Parse("a*b", vars));
            Assert.AreEqual(a - b, ExpressionParser.Parse("a-b", vars));
            Assert.AreEqual(a / b, ExpressionParser.Parse("a/b", vars));
            Assert.AreEqual(a ^ b, ExpressionParser.Parse("a^b", vars));
            Assert.AreEqual(-a * b, ExpressionParser.Parse("-a*b", vars));
            Assert.AreEqual(a * -b, ExpressionParser.Parse("a*-b", vars));
            Assert.AreEqual(a << b, ExpressionParser.Parse("a<<b", vars));
            Assert.AreEqual(a >> b, ExpressionParser.Parse("a>>b", vars));
            Assert.AreEqual(a % b, ExpressionParser.Parse("a%b", vars));
            Assert.AreEqual(a & b, ExpressionParser.Parse("a&b", vars));
            Assert.AreEqual(a | b, ExpressionParser.Parse("a|b", vars));
            Assert.AreEqual(!g || b > c && h ^ f - (decimal)e > 90, ExpressionParser.Parse("!g||b>c&&h^f-e>90", vars));
            Assert.AreEqual(a + b * c + (decimal)d - (decimal)e * f, ExpressionParser.Parse("a+b*c+d-e*f", vars));
            Assert.AreEqual(a | b ^ a & b | ~a ^ ~b, ExpressionParser.Parse("a | b ^ a & b | ~a ^ ~b", vars));
            Assert.AreEqual(g | h ^ g & h | !g ^ !h, ExpressionParser.Parse("g|h^g&h|!g^!h", vars));
        }

        [TestMethod]
        public void TestLogic()
        {
            Assert.AreEqual(false, ExpressionParser.Parse("1 + 2 > 3 && 3 / 0 != 5", new Dictionary<string, object>()));
            Assert.AreEqual(true, ExpressionParser.Parse("1 + 2 == 3 && 3 / 1 != 5", new Dictionary<string, object>()));
            Assert.AreEqual(false, ExpressionParser.Parse("1 + 2 > 3 || 3 / 1 == 5", new Dictionary<string, object>()));
            Assert.AreEqual(true, ExpressionParser.Parse("1 + 2 == 3 || 3 / 0 == 5", new Dictionary<string, object>()));
        }

        [TestMethod]
        public void TestScopes()
        {
            Assert.AreEqual(1, ExpressionParser.ParseBlock(@"i = 0
if (i == 0){
    i++;
}
return i;", new Dictionary<string, object>()));
            Assert.AreEqual(null, ExpressionParser.ParseBlock(@"if (true){
    i = 1;
}
return i;", new Dictionary<string, object>()));
        }

        [TestMethod]
        public void TestObjectFunctions()
        {
            Assert.AreEqual(1, ExpressionParser.ParseBlock(@"obj = {Fibonacci : function(number){
if (number <= 2)
    {
        return 1;
    }
    else
    {
        return Fibonacci(number - 2) + Fibonacci(number - 1);
    }
}
};
return obj.Fibonacci(1);", new Dictionary<string, object>()));
            Assert.AreEqual(1, ExpressionParser.ParseBlock(@"obj = {Fibonacci : function(number){
if (number <= 2)
    {
        return 1;
    }
    else
    {
        return Fibonacci(number - 2) + Fibonacci(number - 1);
    }
}
};
return obj.Fibonacci(2);", new Dictionary<string, object>()));
            Assert.AreEqual(2, ExpressionParser.ParseBlock(@"obj = {Fibonacci : function(number){
if (number <= 2)
    {
        return 1;
    }
    else
    {
        return Fibonacci(number - 2) + Fibonacci(number - 1);
    }
}
};
return obj.Fibonacci(3);", new Dictionary<string, object>()));
            Assert.AreEqual(55, ExpressionParser.ParseBlock(@"obj = {Fibonacci : function(number){
if (number <= 2)
    {
        return 1;
    }
    else
    {
        return Fibonacci(number - 2) + Fibonacci(number - 1);
    }
}
};
return obj.Fibonacci(10);", new Dictionary<string, object>()));
            Assert.AreEqual(6765, ExpressionParser.ParseBlock(@"obj = {Fibonacci : function(number){
if (number <= 2)
    {
        return 1;
    }
    else
    {
        return Fibonacci(number - 2) + Fibonacci(number - 1);
    }
}
};
return obj.Fibonacci(20);", new Dictionary<string, object>()));
        }

        [TestMethod]
        public void TestLoops()
        {
            int[] values = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
            Assert.AreEqual(55,
                ExpressionParser.ParseBlock(@"ret = 0;
foreach (i in values){
ret += i;
}
return ret;", new Dictionary<string, object> {{"values", values}}));
            Assert.AreEqual(55,
                ExpressionParser.ParseBlock(@"ret = 0;
for (i =0; i<  values.Length; i++){
ret += values[i];
}
return ret;", new Dictionary<string, object> { { "values", values } }));
            Assert.AreEqual(55,
                ExpressionParser.ParseBlock(@"ret = 0;
i = 0;
do{
ret += values[i];
i++;
}while (i < values.Length);
return ret;", new Dictionary<string, object> { { "values", values } }));
            Assert.AreEqual(55,
                ExpressionParser.ParseBlock(@"ret = 0;
i = 0;
while(i<values.Length){
ret += values[i];
i++;
}
return ret;", new Dictionary<string, object> { { "values", values } }));
        }

        [TestMethod]
        public void TestImplicitObject()
        {
            Dictionary<string,object> tmp1 = new Dictionary<string, object>{{"TestKey", "TestVal"}};
            ExpressionParser.Parse("TestKey = 42", (object)tmp1, s => DefaultCallbacks.PrepareDefaultCallbacks(s.Scope, s.ReplSession));
            Assert.AreEqual(tmp1["TestKey"], 42);
            var test = new TestClass();
            test.TestString = "TestVal";
            ExpressionParser.Parse("TestString = \"The Answer is 42\"", test, s => DefaultCallbacks.PrepareDefaultCallbacks(s.Scope, s.ReplSession));
            Assert.AreEqual(test.TestString, "The Answer is 42");
            ExpressionParser.Parse("TestString = \"The Answer is 42\"", (object)tmp1, s => DefaultCallbacks.PrepareDefaultCallbacks(s.Scope, s.ReplSession));
            Assert.IsFalse(tmp1.ContainsKey("TestString"));
        }
        
        [TestMethod]
        public void NativeTests()
        {
            var test = new TestClass();
            NativeScriptHelper.AddReference("DUMMY","--ROSLYN--");
            ExpressionParser.Parse("TestString = `E(\"A\" as hicks -> DUMMY)::\"return Global.GoFuckYourself();\" with {GoFuckYourself:function(){return \"hallo\";}}", test, s => DefaultCallbacks.PrepareDefaultCallbacks(s.Scope, s.ReplSession));
            Assert.AreEqual(test.TestString, "hallo");
            ExpressionParser.Parse(@"TestString = `E(#DUMMY)::@#
int a = -1;
for (int i = 0; i< 100000; i++){
    a += 10;
    a = a%71;
}

return Global.GoFuckYourself(a);
# with {GoFuckYourself:function(val){return 'System.String'.Format(""velo{0}"",val);}}", test, s => DefaultCallbacks.PrepareDefaultCallbacks(s.Scope, s.ReplSession));
            Assert.AreEqual(test.TestString, "velo35");
        }

        public class TestClass
        {
            public string TestString { get; set; }
        }
    }
}
