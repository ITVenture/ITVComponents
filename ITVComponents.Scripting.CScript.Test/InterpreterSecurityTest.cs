using System;
using System.Collections.Generic;
using ITVComponents.DataAccess;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Interpreter;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Scripting.CScript.Security.Extensions;
using ITVComponents.Scripting.CScript.Security.Restrictions;
using ITVComponents.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Scripting.CScript.Test
{
    /// <summary>
    /// Prueft, dass der Interpreter dieselben Sperren durchsetzt wie der ScriptVisitor.
    /// </summary>
    /// <remarks>
    /// Spiegelt SecurityTests. Eine Ausfuehrungsmaschine, die schneller oder sauberer ist, aber
    /// eine Sperre durchlaesst, waere unbrauchbar - deshalb wird hier jede Sperre einzeln gegen
    /// beide Maschinen geprueft.
    ///
    /// Wichtig fuer den Interpreter: die Policy gehoert in den Ausfuehrungspfad, nicht in die
    /// Bauphase. Derselbe zwischengespeicherte Baum kann unter verschiedenen Policies laufen -
    /// jeder Test, der eine Sperre nachweist, prueft deshalb auch, dass sie nicht am Baum
    /// haengenbleibt.
    /// </remarks>
    [TestClass]
    public class InterpreterSecurityTest
    {
        [TestMethod]
        public void DeniedMembersAreBlocked()
        {
            var vars = new Dictionary<string, object>
            {
                { "foo", new Dictionary<string, object>() },
                { "DBNull", typeof(DBNull) }
            };

            var policy = ScriptingPolicy.Default
                .WithMethodAccessRestriction<object>(n => n.ToString(), PolicyMode.Deny)
                .WithMethodAccessRestriction<IDictionary<string, object>>(o => o.Add(default, default),
                    PolicyMode.Deny)
                .WithPropertyAccessRestriction<IDictionary<string, object>>(o => o.Count,
                    PropertyAccessMode.Read, PolicyMode.Deny)
                .WithFieldAccessRestriction(() => DBNull.Value, FieldAccessMode.Read, PolicyMode.Deny);

            AssertBothDeny("foo.ToString()", vars, policy);
            AssertBothDeny("foo.Add(\"Hallo\",42)", vars, policy);
            AssertBothDeny("bar = foo.Count", vars, policy);
            AssertBothDeny("bar = DBNull.Value", vars, policy);
        }

        [TestMethod]
        public void DeniedMembersRemainAllowedWithoutPolicy()
        {
            // Die Gegenprobe zu DeniedMembersAreBlocked: ohne Sperre muessen dieselben
            // Ausdruecke laufen. Sonst bewiese der Sperrtest nur, dass irgendetwas schiefgeht.
            var vars = new Dictionary<string, object> { { "foo", new Dictionary<string, object>() } };
            Assert.AreEqual(0, ScriptInterpreter.Parse("foo.Count", Copy(vars)));
            Assert.AreEqual(0, ExpressionParser.Parse("foo.Count", Copy(vars)));
        }

        [TestMethod]
        public void TypeAndAssemblyAccess()
        {
            var vars = new Dictionary<string, object>
            {
                { "foo", new Dictionary<string, object>() },
                { "DBNull", typeof(DBNull) }
            };

            var policy = ScriptingPolicy.Default
                .Configure(n => { n.TypeLoading = PolicyMode.Deny; })
                .WithAssemblyRestriction(typeof(PasswordSecurity), PolicyMode.Allow)
                .WithTypeRestriction(typeof(IDbWrapper), TypeAccessMode.Direct, PolicyMode.Allow);

            // Ausdruecklich erlaubte Assembly: der Aufruf muss durchgehen.
            const string encrypt =
                "bar = 'ITVComponents.Security.PasswordSecurity@@\"ITVComponents.dll\"'.Encrypt(\"Huhu!\")";
            Assert.AreEqual("Huhu!", ((string)ScriptInterpreter.Parse(encrypt, Copy(vars), policy: policy)).Decrypt());

            // Ausdruecklich erlaubter Typ.
            const string typeAccess =
                "bar = 'ITVComponents.DataAccess.IDbWrapper@@\"ITVComponents.DataAccess.dll\"'";
            Assert.AreEqual(typeof(IDbWrapper), ScriptInterpreter.Parse(typeAccess, Copy(vars), policy: policy));

            // Nicht freigegebener Typ: TypeLoading steht auf Deny.
            AssertBothDeny(
                "bar = new 'ITVComponents.DataAccess.Where@@\"ITVComponents.DataAccess.dll\"'()", vars, policy);
        }

        [TestMethod]
        public void PolicyIsNotBakedIntoTheCompiledTree()
        {
            // Der Uebersetzungs-Zwischenspeicher haelt den Baum ueber Laeufe hinweg. Wuerde die
            // Policy beim Bauen ausgewertet, entschiede der erste Lauf ueber alle spaeteren -
            // je nach Reihenfolge entweder eine unterlaufene Sperre oder eine falsche Sperrung.
            var vars = new Dictionary<string, object> { { "foo", new Dictionary<string, object>() } };
            var denied = ScriptingPolicy.Default
                .WithPropertyAccessRestriction<IDictionary<string, object>>(o => o.Count,
                    PropertyAccessMode.Read, PolicyMode.Deny);

            Assert.AreEqual(0, ScriptInterpreter.Parse("foo.Count", Copy(vars)));
            Assert.ThrowsException<ScriptSecurityException>(
                () => ScriptInterpreter.Parse("foo.Count", Copy(vars), policy: denied),
                "Die Sperre muss auch fuer einen bereits uebersetzten Baum greifen.");
            Assert.AreEqual(0, ScriptInterpreter.Parse("foo.Count", Copy(vars)),
                "Die Sperre darf nicht am Baum haengenbleiben.");
        }

        [TestMethod]
        public void ScriptMethodsCanBeDenied()
        {
            var denied = ScriptingPolicy.Default.Configure(n => n.ScriptMethods = PolicyMode.Deny);
            Assert.ThrowsException<ScriptSecurityException>(
                () => ScriptInterpreter.ParseBlock("function f() { return 1; } return f();",
                    new Dictionary<string, object>(), policy: denied),
                "Das Definieren von Script-Methoden muss unterbunden werden koennen.");
        }

        /// <summary>
        /// Prueft, dass beide Maschinen denselben Ausdruck unter derselben Policy abweisen.
        /// </summary>
        /// <remarks>
        /// Geprueft wird auf ScriptException einschliesslich Unterklassen, nicht auf den genauen
        /// Typ: der Interpreter reicht ScriptSecurityException durch, waehrend der ScriptVisitor
        /// sie in eine ScriptException verpackt. Siehe SecurityExceptionsArePropagatedUnwrapped.
        /// </remarks>
        private static void AssertBothDeny(string expression, IDictionary<string, object> variables,
            ScriptingPolicy policy)
        {
            AssertThrows(() => ExpressionParser.Parse(expression, Copy(variables), policy: policy),
                $"ScriptVisitor sollte '{expression}' abweisen.");
            AssertThrows(() => ScriptInterpreter.Parse(expression, Copy(variables), policy: policy),
                $"Interpreter sollte '{expression}' abweisen.");
        }

        private static void AssertThrows(Action action, string message)
        {
            try
            {
                action();
            }
            catch (ScriptException)
            {
                return;
            }

            Assert.Fail(message);
        }

        /// <summary>
        /// Sicherheitsausnahmen werden unverpackt durchgereicht - ueber beide Einstiegspunkte.
        /// </summary>
        /// <remarks>
        /// Der abgeloeste ScriptVisitor fing beim Methodenaufruf jede Ausnahme und verpackte sie
        /// in eine ScriptException ("Method-Call failed!"); der Sicherheitsgrund landete in der
        /// InnerException, und wer gezielt auf ScriptSecurityException pruefte, sah ihn nicht.
        ///
        /// Der Interpreter reicht ScriptSecurityException unveraendert durch, und seit
        /// ExpressionParser ueber den Interpreter ausfuehrt, gilt das auch dort. Da
        /// ScriptSecurityException von ScriptException erbt, faengt bestehender Code sie
        /// weiterhin - betroffen war nur, wer auf den genauen Typ prueft, und der sieht jetzt
        /// ueberall den praezisen Typ.
        /// </remarks>
        [TestMethod]
        public void SecurityExceptionsArePropagatedUnwrapped()
        {
            var vars = new Dictionary<string, object> { { "foo", new Dictionary<string, object>() } };
            var policy = ScriptingPolicy.Default
                .WithMethodAccessRestriction<IDictionary<string, object>>(o => o.Add(default, default),
                    PolicyMode.Deny);

            var fromInterpreter = Assert.ThrowsException<ScriptSecurityException>(
                () => ScriptInterpreter.Parse("foo.Add(\"Hallo\",42)", Copy(vars), policy: policy),
                "Der Interpreter soll den Sicherheitsgrund direkt melden.");
            Assert.IsTrue(fromInterpreter.Message.Contains("denied"),
                "Die Meldung soll den Grund nennen.");

            // ExpressionParser fuehrt ueber denselben Interpreter aus: auch hier kommt die
            // ScriptSecurityException unverpackt an, mit demselben Grund in der Meldung.
            var fromExpressionParser = Assert.ThrowsException<ScriptSecurityException>(
                () => ExpressionParser.Parse("foo.Add(\"Hallo\",42)", Copy(vars), policy: policy),
                "ExpressionParser soll den Sicherheitsgrund ebenfalls direkt melden.");
            Assert.IsTrue(fromExpressionParser.Message.Contains("denied"),
                "Die Meldung soll den Grund nennen.");
        }

        private static Dictionary<string, object> Copy(IDictionary<string, object> variables)
        {
            return new Dictionary<string, object>(variables);
        }
    }
}
