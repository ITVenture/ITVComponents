using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms the value half of object security: what a share points at, whether it was supplied
    /// completely, and — the part that decides everything later — when two values count as the same one.
    /// A comparison that answers "04711" and 4711 differently comes back half a year later as "the link
    /// sometimes does not work".
    /// </summary>
    [TestClass]
    public class AssetArgumentValuesTests
    {
        [TestMethod]
        public void Missing_Required_Argument_Is_Refused_And_Named()
        {
            var ok = AssetArgumentValues.TryCreate(
                new[] { new AssetArgumentDeclaration("orderId", AssetArgumentType.Int) },
                new Dictionary<string, string>(), out _, out var error);

            Assert.IsFalse(ok);
            StringAssert.Contains(error, "orderId", "the message has to name the argument, not just fail");
        }

        [TestMethod]
        public void Optional_Argument_May_Stay_Empty()
        {
            var ok = AssetArgumentValues.TryCreate(
                new[] { new AssetArgumentDeclaration("note", AssetArgumentType.String, Required: false) },
                new Dictionary<string, string>(), out var values, out _);

            Assert.IsTrue(ok);
            Assert.IsTrue(values.IsEmpty);
        }

        [TestMethod]
        public void Value_Of_The_Wrong_Type_Is_Refused()
        {
            var ok = AssetArgumentValues.TryCreate(
                new[] { new AssetArgumentDeclaration("orderId", AssetArgumentType.Int) },
                new Dictionary<string, string> { ["orderId"] = "not-a-number" }, out _, out var error);

            Assert.IsFalse(ok);
            StringAssert.Contains(error, "Int");
        }

        [TestMethod]
        public void Undeclared_Value_Is_Refused_Instead_Of_Carried_Along()
        {
            // Ein Wert, den die Vorlage nicht kennt, wuerde spaeter von niemandem geprueft - er waere eine
            // stille Luecke und keine Zusatzangabe.
            var ok = AssetArgumentValues.TryCreate(
                new[] { new AssetArgumentDeclaration("orderId", AssetArgumentType.Int) },
                new Dictionary<string, string> { ["orderId"] = "4711", ["tenantId"] = "9" }, out _, out var error);

            Assert.IsFalse(ok);
            StringAssert.Contains(error, "tenantId");
        }

        [TestMethod]
        public void Leading_Zeroes_Do_Not_Change_The_Object()
        {
            var values = Create(("orderId", AssetArgumentType.Int, "04711"));

            Assert.IsTrue(values.Matches("orderId", 4711, AssetArgumentType.Int));
            Assert.IsTrue(values.Matches("orderId", "4711", AssetArgumentType.Int));
        }

        [TestMethod]
        public void A_Different_Number_Does_Not_Match()
        {
            var values = Create(("orderId", AssetArgumentType.Int, "4711"));

            Assert.IsFalse(values.Matches("orderId", 4712, AssetArgumentType.Int));
        }

        [TestMethod]
        public void Guid_Notation_Is_Not_Content()
        {
            var id = Guid.NewGuid();
            var values = Create(("fileId", AssetArgumentType.Guid, id.ToString("B")));

            Assert.IsTrue(values.Matches("fileId", id, AssetArgumentType.Guid));
            Assert.IsTrue(values.Matches("fileId", id.ToString("N"), AssetArgumentType.Guid));
        }

        [TestMethod]
        public void A_Date_Share_Does_Not_Fail_On_The_Time_Of_Day()
        {
            var values = Create(("day", AssetArgumentType.Date, "2026-08-27"));

            Assert.IsTrue(values.Matches("day", new DateTime(2026, 8, 27, 16, 45, 0), AssetArgumentType.Date));
            Assert.IsFalse(values.Matches("day", new DateTime(2026, 8, 28), AssetArgumentType.Date));
        }

        [TestMethod]
        public void Strings_Compare_Without_Regard_To_Case()
        {
            var values = Create(("code", AssetArgumentType.String, "AbC-1"));

            Assert.IsTrue(values.Matches("code", "abc-1", AssetArgumentType.String));
            Assert.IsFalse(values.Matches("code", "abc-2", AssetArgumentType.String));
        }

        [TestMethod]
        public void An_Unknown_Argument_Never_Matches()
        {
            var values = Create(("orderId", AssetArgumentType.Int, "4711"));

            Assert.IsFalse(values.Matches("somethingElse", 4711, AssetArgumentType.Int));
        }

        [TestMethod]
        public void Values_Survive_Storage()
        {
            var values = Create(("orderId", AssetArgumentType.Int, "04711"), ("code", AssetArgumentType.String, "AbC"));

            var restored = AssetArgumentValues.FromJson(values.ToJson());

            Assert.IsTrue(restored.Matches("orderId", 4711, AssetArgumentType.Int));
            Assert.IsTrue(restored.Matches("code", "abc", AssetArgumentType.String));
        }

        [TestMethod]
        public void Broken_Storage_Yields_An_Empty_Set_Instead_Of_Throwing()
        {
            // Leer heisst: nichts passt. Das ist die sichere Antwort - der Aufrufer entscheidet, ob es ein
            // Fehler ist.
            var restored = AssetArgumentValues.FromJson("{not json");

            Assert.IsTrue(restored.IsEmpty);
            Assert.IsFalse(restored.Matches("orderId", 4711, AssetArgumentType.Int));
        }

        private static AssetArgumentValues Create(params (string Name, AssetArgumentType Type, string Value)[] items)
        {
            var declarations = new List<AssetArgumentDeclaration>();
            var input = new Dictionary<string, string>();
            foreach (var item in items)
            {
                declarations.Add(new AssetArgumentDeclaration(item.Name, item.Type));
                input[item.Name] = item.Value;
            }

            Assert.IsTrue(AssetArgumentValues.TryCreate(declarations, input, out var values, out var error), error);
            return values;
        }
    }
}
