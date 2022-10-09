using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Scripting.CScript.Security.Extensions;
using ITVComponents.Scripting.CScript.Security.Restrictions;
using ITVComponents.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    [TestClass]
    public class SecurityTests
    {
        [TestMethod]
        public void TestDenySingleMethod()
        {
            Dictionary<string, object> tmp1 = new Dictionary<string, object>
            {
                {"foo",new Dictionary<string,object>()},
                {"DBNull",typeof(DBNull)}
            };
            var policy = ScriptingPolicy.Default.WithMethodAccessRestriction<object>(n => n.ToString(),
                    PolicyMode.Deny)
                .WithMethodAccessRestriction<IDictionary<string, object>>(o => o.Add(default, default),
                    PolicyMode.Deny)
                .WithPropertyAccessRestriction<IDictionary<string,Object>>(o => o.Count, PropertyAccessMode.Read, PolicyMode.Deny)
                .WithFieldAccessRestriction( ()=>DBNull.Value, FieldAccessMode.Read, PolicyMode.Deny);
            Assert.ThrowsException<ScriptException>(() =>
            {
                var tmp = ExpressionParser.Parse("foo.ToString()", tmp1,
                    policy: policy);
            });

            Assert.ThrowsException<ScriptException>(() =>
            {
                var tmp = ExpressionParser.Parse("foo.Add(\"Hallo\",42)", tmp1,
                    policy: policy);
            });

            Assert.ThrowsException<ScriptSecurityException>(() =>
            {
                var tmp = ExpressionParser.Parse("bar = foo.Count", tmp1,
                    policy: policy);
            });

            Assert.ThrowsException<ScriptSecurityException>(() =>
            {
                var tmp = ExpressionParser.Parse("bar = DBNull.Value", tmp1,
                    policy: policy);
            });
        }

        [TestMethod]
        public void AssemblyAndTypeAccess()
        {
            Dictionary<string, object> tmp1 = new Dictionary<string, object>
            {
                {"foo",new Dictionary<string,object>()},
                {"DBNull",typeof(DBNull)}
            };

            var policy = ScriptingPolicy.Default.Configure(n =>
            {
                n.TypeLoading = PolicyMode.Deny;
            }).WithAssemblyRestriction(typeof(PasswordSecurity), PolicyMode.Allow)
                .WithTypeRestriction(typeof(IDbWrapper),TypeAccessMode.Direct, PolicyMode.Allow);

            var c1 = ExpressionParser.Parse(
                "bar = 'ITVComponents.Security.PasswordSecurity@@\"ITVComponents.dll\"'.Encrypt(\"Huhu!\")", tmp1,
                policy: policy);
            Assert.AreEqual("Huhu!",((string)c1).Decrypt());
            var c2 = ExpressionParser.Parse("bar = 'ITVComponents.DataAccess.IDbWrapper@@\"ITVComponents.DataAccess.dll\"'",
                tmp1, policy:policy);
            Assert.AreEqual(c2, typeof(IDbWrapper));
            Assert.ThrowsException<ScriptException>(() =>
            {
                var tmp = ExpressionParser.Parse(
                    "bar = new 'ITVComponents.DataAccess.Where@@\"ITVComponents.DataAccess.dll\"'()",tmp1, policy:policy);
            });

        }

        [TestMethod]
        public void NativeScripting()
        {
            Dictionary<string, object> tmp1 = new Dictionary<string, object>
            {
                {"foo",new List<string>{
                    "hü",
                    "ha",
                    "ho",
                    "halter",
                    "horst",
                    "schimmel",
                    "hühaho"
                } },
                {"DBNull",typeof(DBNull)}
            };
            var policy = ScriptingPolicy.Default.Configure(n => n.NativeScripting = PolicyMode.Deny);
            var s = ExpressionParser.Parse("`E(foo as list -> DEFAULT)::\"List<string>list = Global.list;return list.FirstOrDefault(n => n.Equals((string)Global.search));\" with {search:\"schimmel\"}",tmp1);
            Assert.AreEqual("schimmel",s);
            Assert.ThrowsException<ScriptException>(() =>
            {
                var tmp = ExpressionParser.Parse(
                    "`E(foo as list -> DEFAULT)::\"List<string>list = Global.list;return list.FirstOrDefault(n => n.Equals((string)Global.search));\" with {search:\"schimmel\"}",
                    tmp1, policy:policy);
            });

        }

    }
}
