using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.Workflow.Instances;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Haelt die Feldlisten des <see cref="Token"/>-Zustands zusammen: <c>CopyStateFrom</c> und
    /// <c>SameState</c> muessen JEDES Feld behandeln, sonst faellt beim naechsten neuen Feld eine der
    /// beiden durch.
    /// </summary>
    /// <remarks>
    /// Ein vergessenes Feld ist hier nicht zu sehen, sondern nur zu merken: der Zweig-Commit uebertraegt
    /// es dann nicht, und die Spalte bleibt in der Datenbank leer. Genau so verschwanden schon einmal
    /// Benutzer-Aufgaben aus der Arbeitsliste (Stempel im Diff, aber nicht im Merge) - der Fehler kostete
    /// deutlich mehr Zeit als dieser Test.
    /// <para>
    /// Der Test schreibt in jedes Feld einen von der Vorgabe verschiedenen Wert und prueft beide
    /// Richtungen. Er braucht daher keine gepflegte Feldliste - er liest sie per Reflexion und faellt
    /// automatisch ueber ein neues Feld.
    /// </para></remarks>
    [TestClass]
    public class TokenStateContractTest
    {
        /// <summary>Die Id gehoert bewusst NICHT zum Zustand (sie identifiziert, sie beschreibt nicht).</summary>
        private static readonly string[] NotPartOfState = { nameof(Token.Id) };

        private static IEnumerable<PropertyInfo> StateProperties()
            => typeof(Token).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && !NotPartOfState.Contains(p.Name));

        [TestMethod]
        public void CopyStateFrom_TransfersEveryProperty()
        {
            foreach (PropertyInfo property in StateProperties())
            {
                var source = new Token { Id = "source" };
                object distinct = DistinctValue(property);
                property.SetValue(source, distinct);

                var target = new Token { Id = "target" };
                target.CopyStateFrom(source);

                Assert.AreEqual(Comparable(distinct), Comparable(property.GetValue(target)),
                    $"Token.CopyStateFrom does not transfer '{property.Name}' - add it there. Without it the " +
                    "branch commit drops the value and the column stays empty.");
            }
        }

        [TestMethod]
        public void SameState_NoticesEveryProperty()
        {
            foreach (PropertyInfo property in StateProperties())
            {
                var a = new Token { Id = "same" };
                var b = new Token { Id = "same" };
                Assert.IsTrue(Token.SameState(a, b), "two fresh tokens must be equal to begin with.");

                property.SetValue(b, DistinctValue(property));

                Assert.IsFalse(Token.SameState(a, b),
                    $"Token.SameState ignores '{property.Name}' - add it there. Without it a diff misses the " +
                    "change that the merge would have transferred.");
            }
        }

        /// <summary>Ein Wert, der sich garantiert von der Vorgabe des Feldes unterscheidet.</summary>
        private static object DistinctValue(PropertyInfo property)
        {
            Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (type == typeof(string))
            {
                return "x-" + property.Name;
            }

            if (type == typeof(DateTime))
            {
                return new DateTime(2031, 4, 5, 6, 7, 8, DateTimeKind.Utc);
            }

            if (type == typeof(int))
            {
                return 4711;
            }

            if (type == typeof(bool))
            {
                return true;
            }

            if (type.IsEnum)
            {
                // Der ZWEITE Wert - der erste ist bei den meisten Aufzaehlungen die Vorgabe.
                Array values = Enum.GetValues(type);
                return values.GetValue(values.Length > 1 ? 1 : 0);
            }

            if (type == typeof(Dictionary<string, object>))
            {
                return new Dictionary<string, object> { { "k", "v" } };
            }

            Assert.Fail($"The contract test has no distinct value for type '{type.FullName}' " +
                        $"(property '{property.Name}'). Extend DistinctValue.");
            return null;
        }

        /// <summary>Vergleichbare Form - der Zweig-Scope wird kopiert, ist also nicht referenzgleich.</summary>
        private static object Comparable(object value)
            => value is Dictionary<string, object> map
                ? string.Join(";", map.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"))
                : value;
    }
}
