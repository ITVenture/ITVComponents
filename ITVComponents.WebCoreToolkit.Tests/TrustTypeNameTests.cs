using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Der Zerleger fuer assembly-qualifizierte Typnamen und die daraus gebaute Diagnose.
    /// <para>
    /// Der Fall dahinter: in <c>TrustedFullAccessComponents</c> stehen assembly-qualifizierte Typnamen,
    /// und im Namen eines generischen Typs steckt die Stelligkeit. Bekommt oder verliert der
    /// Sicherheitskontext eine Entitaet, verschiebt sie sich bei jedem Typ, der den Kontext generisch
    /// fuehrt - der geseedete Eintrag passt nicht mehr, das Nachschlagen liefert nichts, und der Aufrufer
    /// gilt als nicht vertraut. Der Build ist gruen, die Migration laeuft, die Anmeldung gelingt; erst
    /// danach fehlen Daten.
    /// </para>
    /// <para>
    /// Diese Tests halten fest, dass der Zerleger den Fall <b>erkennt</b> - und dass er an der Form
    /// scheitert, an der ein <c>Split(',')</c> scheitert: ein geschlossener generischer Typ traegt seine
    /// Typargumente in eckigen Klammern VOR dem Assembly-Teil, jedes davon selbst qualifiziert.
    /// </para>
    /// </summary>
    [TestClass]
    public class TrustTypeNameTests
    {
        [TestMethod]
        public void A_Closed_Generic_Splits_At_The_Comma_Outside_The_Brackets()
        {
            var name = typeof(Dictionary<string, int>).AssemblyQualifiedName;

            Assert.IsTrue(TrustTypeName.TrySplit(name, out var typeName, out var assembly));
            // Das erste Komma im Namen steht INNERHALB der Argumentliste - wer dort trennt, bekommt
            // "System.Collections.Generic.Dictionary`2[[System.String" und haelt das fuer einen Typnamen.
            StringAssert.StartsWith(typeName, "System.Collections.Generic.Dictionary`2[[");
            StringAssert.EndsWith(typeName, "]]");
            StringAssert.StartsWith(assembly, "System.Private.CoreLib");
        }

        [TestMethod]
        public void Nested_Arguments_From_Other_Assemblies_Do_Not_Confuse_The_Split()
        {
            var name = typeof(Dictionary<string, List<Uri>>).AssemblyQualifiedName;

            Assert.AreEqual("System.Collections.Generic.Dictionary", TrustTypeName.GetArityFreeName(name));
            Assert.AreEqual("System.Private.CoreLib", TrustTypeName.GetAssemblySimpleName(name));
            Assert.AreEqual(2, TrustTypeName.GetGenericArity(name));
        }

        [TestMethod]
        public void An_Open_Generic_Definition_Keeps_Its_Arity_And_Loses_It_In_The_Key()
        {
            var name = typeof(Dictionary<,>).AssemblyQualifiedName;

            Assert.AreEqual(2, TrustTypeName.GetGenericArity(name));
            Assert.AreEqual("System.Collections.Generic.Dictionary", TrustTypeName.GetArityFreeName(name));
        }

        [TestMethod]
        public void A_Non_Generic_Type_Has_No_Arity()
        {
            var name = typeof(string).AssemblyQualifiedName;

            Assert.IsNull(TrustTypeName.GetGenericArity(name));
            Assert.AreEqual("System.String", TrustTypeName.GetArityFreeName(name));
        }

        [TestMethod]
        public void A_Nested_Type_Keeps_The_Plus_So_Outer_And_Inner_Stay_Apart()
        {
            var name = typeof(Outer<int>.Inner).AssemblyQualifiedName;

            // Nur die Zahl hinter dem Backtick faellt weg, nicht das '+': sonst hiessen "Outer" und
            // "Outer+Inner" dasselbe.
            StringAssert.Contains(TrustTypeName.GetArityFreeName(name), "+Inner");
        }

        [TestMethod]
        public void The_Same_Type_With_A_Different_Arity_Yields_The_Same_Comparison_Key()
        {
            var current = typeof(Dictionary<,>).AssemblyQualifiedName;
            var stale = current.Replace("Dictionary`2", "Dictionary`3");

            Assert.AreEqual(TrustTypeName.GetComparisonKey(current), TrustTypeName.GetComparisonKey(stale));
            StringAssert.Contains(TrustTypeName.DescribeDifference(current, stale), "different generic arity");
        }

        [TestMethod]
        public void An_Unqualified_Name_Survives_The_Split()
        {
            Assert.IsTrue(TrustTypeName.TrySplit("Some.Namespace.Type`4", out var typeName, out var assembly));
            Assert.AreEqual("Some.Namespace.Type`4", typeName);
            Assert.AreEqual(string.Empty, assembly);
            Assert.AreEqual("Some.Namespace.Type", TrustTypeName.GetArityFreeName("Some.Namespace.Type`4"));
        }

        [TestMethod]
        public void An_Empty_Name_Is_Not_A_Name()
        {
            Assert.IsFalse(TrustTypeName.TrySplit(null, out _, out _));
            Assert.IsFalse(TrustTypeName.TrySplit("   ", out _, out _));
            Assert.AreEqual(string.Empty, TrustTypeName.GetArityFreeName(null));
        }

        [TestMethod]
        public void A_Stale_Arity_Resolves_To_Nothing_But_Names_The_Type_That_Is_Loaded()
        {
            var stale = typeof(Dictionary<,>).AssemblyQualifiedName.Replace("Dictionary`2", "Dictionary`3");

            var diagnostic = TrustTypeResolver.Check(stale, "trusted type");

            Assert.IsFalse(diagnostic.Resolvable);
            Assert.IsFalse(diagnostic.Ok);
            // Das ist der Punkt der ganzen Uebung: die Meldung nennt den Typ, der tatsaechlich geladen ist.
            StringAssert.Contains(diagnostic.Hint, "different generic arity");
            StringAssert.Contains(diagnostic.ActualName, "Dictionary`2");
        }

        [TestMethod]
        public void A_Name_That_Matches_The_Runtime_Exactly_Is_Ok()
        {
            var diagnostic = TrustTypeResolver.Check(typeof(Dictionary<,>).AssemblyQualifiedName, "trusted type");

            Assert.IsTrue(diagnostic.Ok);
            Assert.IsNull(diagnostic.Hint);
        }

        [TestMethod]
        public void An_Empty_Target_Can_Never_Match_And_Says_So()
        {
            var diagnostic = TrustTypeResolver.Check(string.Empty, "target type");

            Assert.IsFalse(diagnostic.Ok);
            StringAssert.Contains(diagnostic.Hint, "can never match");
        }

        [TestMethod]
        public void A_Result_Without_Findings_Is_Valid_And_Says_How_Many_It_Checked()
        {
            var result = new TrustEntryValidationResult(true, new[]
            {
                new TrustEntryDiagnostic
                {
                    EntryId = 1,
                    Description = "ok",
                    TrustedType = TrustTypeResolver.Check(typeof(Dictionary<,>).AssemblyQualifiedName, "trusted type"),
                    TrustingType = TrustTypeResolver.Check(typeof(string).AssemblyQualifiedName, "target type")
                }
            });

            Assert.IsTrue(result.AllValid);
            StringAssert.Contains(result.Describe(), "1 trust entries checked, 0 of them");
        }

        [TestMethod]
        public void A_Provider_That_Cannot_Check_Is_Not_The_Same_As_A_Clean_Result()
        {
            // Sonst liest sich "keine Befunde" als Freibrief, wo in Wahrheit nie geprueft wurde.
            Assert.IsFalse(TrustEntryValidationResult.NotSupported.AllValid);
            Assert.IsFalse(TrustEntryValidationResult.NotSupported.Supported);
        }

        private class Outer<T>
        {
            internal class Inner
            {
            }
        }
    }
}
