using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms the in-memory half of the asset-argument registry — the half every host runs and the half
    /// the persisting implementation builds on. The point that matters most: an unchanged declaration must
    /// be recognizable as unchanged, because that is what keeps a per-request constructor call off the
    /// database.
    /// </summary>
    [TestClass]
    public class AssetArgumentRegistryTests
    {
        [TestMethod]
        public void Declaration_Is_Found_Again()
        {
            var registry = new RecordingRegistry();
            registry.Declare(AssetConsumerKind.Path, "/sales/order/{id}",
                new AssetArgumentDeclaration("id", AssetArgumentType.Int));

            var found = registry.Find(AssetConsumerKind.Path, "/sales/order/{id}");

            Assert.IsNotNull(found);
            Assert.AreEqual(1, found.Arguments.Length);
            Assert.AreEqual("id", found.Arguments[0].Name);
            Assert.AreEqual(AssetArgumentType.Int, found.Arguments[0].Type);
        }

        [TestMethod]
        public void Same_Declaration_Twice_Is_Not_Reported_As_Changed()
        {
            // Der Fall, der auf dem Anfragepfad zaehlt: eine Seite meldet sich bei jedem Aufbau erneut.
            var registry = new RecordingRegistry();
            registry.Declare(AssetConsumerKind.Path, "/x/{id}", new AssetArgumentDeclaration("id"));
            registry.Declare(AssetConsumerKind.Path, "/x/{id}", new AssetArgumentDeclaration("id"));

            CollectionAssert.AreEqual(new[] { true, false }, registry.Changes.ToArray());
        }

        [TestMethod]
        public void Changed_Argument_Set_Replaces_The_Previous_One()
        {
            var registry = new RecordingRegistry();
            registry.Declare(AssetConsumerKind.Path, "/x/{id}", new AssetArgumentDeclaration("id"));
            registry.Declare(AssetConsumerKind.Path, "/x/{id}",
                new AssetArgumentDeclaration("id"), new AssetArgumentDeclaration("mode"));

            var found = registry.Find(AssetConsumerKind.Path, "/x/{id}");

            CollectionAssert.AreEqual(new[] { "id", "mode" }, found.Arguments.Select(n => n.Name).ToArray());
            CollectionAssert.AreEqual(new[] { true, true }, registry.Changes.ToArray());
        }

        [TestMethod]
        public void Dropped_Argument_Disappears()
        {
            // Eine Meldung ist die vollstaendige Liste aus Sicht des Endpunkts - innerhalb eines
            // Konsumenten wird deshalb ersetzt, nicht ergaenzt.
            var registry = new RecordingRegistry();
            registry.Declare(AssetConsumerKind.Path, "/x/{id}",
                new AssetArgumentDeclaration("id"), new AssetArgumentDeclaration("mode"));
            registry.Declare(AssetConsumerKind.Path, "/x/{id}", new AssetArgumentDeclaration("id"));

            CollectionAssert.AreEqual(new[] { "id" },
                registry.Find(AssetConsumerKind.Path, "/x/{id}").Arguments.Select(n => n.Name).ToArray());
        }

        [TestMethod]
        public void Reordering_Counts_As_A_Change()
        {
            // Die Reihenfolge steuert die Teilen-Maske, ist also Teil der Deklaration.
            var registry = new RecordingRegistry();
            registry.Declare(AssetConsumerKind.Path, "/x", new AssetArgumentDeclaration("a"),
                new AssetArgumentDeclaration("b"));
            registry.Declare(AssetConsumerKind.Path, "/x", new AssetArgumentDeclaration("b"),
                new AssetArgumentDeclaration("a"));

            CollectionAssert.AreEqual(new[] { true, true }, registry.Changes.ToArray());
        }

        [TestMethod]
        public void Kind_Separates_Identical_Keys()
        {
            var registry = new RecordingRegistry();
            registry.Declare(AssetConsumerKind.Path, "same", new AssetArgumentDeclaration("a"));
            registry.Declare(AssetConsumerKind.Type, "same", new AssetArgumentDeclaration("b"));

            Assert.AreEqual("a", registry.Find(AssetConsumerKind.Path, "same").Arguments[0].Name);
            Assert.AreEqual("b", registry.Find(AssetConsumerKind.Type, "same").Arguments[0].Name);
            Assert.AreEqual(2, registry.GetConsumers().Count);
        }

        [TestMethod]
        public void Empty_Declarations_Are_Ignored_Instead_Of_Stored()
        {
            var registry = new RecordingRegistry();
            registry.Declare(AssetConsumerKind.Path, null, new AssetArgumentDeclaration("a"));
            registry.Declare(AssetConsumerKind.Path, "   ", new AssetArgumentDeclaration("a"));
            registry.Declare(null);

            Assert.AreEqual(0, registry.GetConsumers().Count);
            Assert.AreEqual(0, registry.Changes.Count);
        }

        [TestMethod]
        public void An_Endpoint_Without_Arguments_Is_Still_A_Consumer()
        {
            var registry = new RecordingRegistry();
            registry.Declare(AssetConsumerKind.Path, "/x");

            Assert.IsNotNull(registry.Find(AssetConsumerKind.Path, "/x"));
            Assert.AreEqual(0, registry.Find(AssetConsumerKind.Path, "/x").Arguments.Length);
        }

        /// <summary>
        /// Macht sichtbar, was die persistierende Fassung als Auftrag bekommt: nur eine Meldung mit
        /// <c>changed == true</c> muss ueberhaupt geschrieben werden.
        /// </summary>
        private sealed class RecordingRegistry : InMemoryAssetArgumentRegistry
        {
            public List<bool> Changes { get; } = new();

            protected override void OnDeclared(AssetConsumerDeclaration declaration, bool changed)
                => Changes.Add(changed);
        }
    }
}
