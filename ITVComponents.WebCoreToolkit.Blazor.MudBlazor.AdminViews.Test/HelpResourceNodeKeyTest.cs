using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test
{
    /// <summary>
    /// Die Kennung einer Zeile in der Ressourcen-Ablage.
    /// </summary>
    /// <remarks>
    /// Sie traegt die ART mit, weil Ordner und Ressourcen je eigene Zaehler haben - ohne das Praefix
    /// zeigte "12" auf zwei verschiedene Dinge, und ein Ablegen landete im falschen. Markup und Handler
    /// muessen sie gleich lesen; genau das steht hier.
    /// </remarks>
    [TestClass]
    public class HelpResourceNodeKeyTest
    {
        [TestMethod]
        public void FolderAndResource_WithTheSameId_AreDifferentKeys()
        {
            Assert.AreNotEqual(HelpResourceNodeKey.For(isFolder: true, 12),
                HelpResourceNodeKey.For(isFolder: false, 12));
        }

        [TestMethod]
        public void RoundTrip_KeepsKindAndId()
        {
            Assert.IsTrue(HelpResourceNodeKey.TryParse(HelpResourceNodeKey.For(true, 7),
                out bool isFolder, out int id));
            Assert.IsTrue(isFolder);
            Assert.AreEqual(7, id);

            Assert.IsTrue(HelpResourceNodeKey.TryParse(HelpResourceNodeKey.For(false, 42),
                out isFolder, out id));
            Assert.IsFalse(isFolder);
            Assert.AreEqual(42, id);
        }

        /// <summary>
        /// Die Wurzel ist KEINE Kennung im Sinne von Art+Id - sie darf beim Deuten nicht durchrutschen,
        /// sonst wanderte ein Eintrag in einen Ordner mit einer erfundenen Nummer.
        /// </summary>
        [TestMethod]
        public void Root_IsNotAParsableNode()
        {
            Assert.IsFalse(HelpResourceNodeKey.TryParse(HelpResourceNodeKey.Root, out _, out _));
        }

        [TestMethod]
        public void Nonsense_IsRejected()
        {
            Assert.IsFalse(HelpResourceNodeKey.TryParse(null, out _, out _));
            Assert.IsFalse(HelpResourceNodeKey.TryParse(string.Empty, out _, out _));
            Assert.IsFalse(HelpResourceNodeKey.TryParse("12", out _, out _));
            Assert.IsFalse(HelpResourceNodeKey.TryParse("x:12", out _, out _));
            Assert.IsFalse(HelpResourceNodeKey.TryParse("f:", out _, out _));
            Assert.IsFalse(HelpResourceNodeKey.TryParse("f:abc", out _, out _));
        }
    }
}
