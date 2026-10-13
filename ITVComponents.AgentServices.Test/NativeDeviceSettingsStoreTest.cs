using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.Plugins;
using ITVComponents.Settings.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.AgentServices.Test
{
    /// <summary>
    /// Die Ablage in den nativen Einstellungen: geschrieben mit JsonUserSettings in die Benutzerdatei, gelesen
    /// über NativeSettings unter ITVenture:AgentServices:Devices:&lt;Name&gt;.
    /// </summary>
    /// <remarks>NativeSettings ist statisch - deshalb nicht parallel, und jeder Test setzt es zurück.</remarks>
    [TestClass]
    [DoNotParallelize]
    public class NativeDeviceSettingsStoreTest
    {
        private string directory = null!;
        private string file = null!;

        [TestInitialize]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "agentsettings-" + Guid.NewGuid().ToString("N"));
            file = Path.Combine(directory, "appsettings.json");
            Directory.CreateDirectory(directory);

            // Was schon in der Datei steht, darf beim Schreiben der Geraete nicht verloren gehen: ein flacher
            // Schluessel (so schreibt MiniStore heute) und ein verschachtelter Abschnitt unter demselben Wurzelnamen.
            File.WriteAllText(file, "{\"ITVenture:KassaAgent\":{\"PrinterName\":\"Alt\"},\"ITVenture\":{\"Other\":{\"Value\":\"1\"}}}");
            NativeSettings.Init(true);
            NativeSettings.AddUserSettings(file);
        }

        [TestCleanup]
        public void Cleanup()
        {
            NativeSettings.Init(true);
            Directory.Delete(directory, true);
        }

        [TestMethod]
        public void WrittenSettingsAreReadBackThroughNativeSettings()
        {
            var store = new NativeDeviceSettingsStore();
            store.Write("epson", new PrinterSettings { PrinterName = "EPSON", Columns = 48, PrintLogo = true, Secret = "leak" });

            var read = (PrinterSettings)store.Read("epson", typeof(PrinterSettings));
            Assert.AreEqual("EPSON", read.PrinterName);
            Assert.AreEqual(48, read.Columns);
            Assert.IsTrue(read.PrintLogo);
            Assert.AreEqual("untouched", read.Secret, "Only fields are stored.");

            Assert.AreEqual("EPSON", NativeSettings.GetSection<PrinterSettings>("ITVenture:AgentServices:Devices:epson").PrinterName,
                "The section path is the agreed one.");
            Assert.IsNull(store.Read("star", typeof(PrinterSettings)), "Not set up = null, not defaults.");
        }

        [TestMethod]
        public void WritingAgainKeepsEverythingElseAndIsVisibleAtOnce()
        {
            var store = new NativeDeviceSettingsStore();
            store.Write("epson", new PrinterSettings { PrinterName = "A" });
            store.Write("star", new PrinterSettings { PrinterName = "Star", Columns = 32 });
            store.Write("epson", new PrinterSettings { PrinterName = "B" });

            Assert.AreEqual("B", ((PrinterSettings)store.Read("epson", typeof(PrinterSettings))).PrinterName,
                "A change is visible on the next read, not only when the file watcher fires.");
            Assert.AreEqual(32, ((PrinterSettings)store.Read("star", typeof(PrinterSettings))).Columns);

            var content = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            Assert.AreEqual("Alt", content["ITVenture:KassaAgent"]!["PrinterName"]!.GetValue<string>(),
                "A flat key already in the file survives.");
            Assert.AreEqual("1", content["ITVenture"]!["Other"]!["Value"]!.GetValue<string>(),
                "A section beside the devices survives.");
        }

        [TestMethod]
        public void WithoutAUserSettingsFileWritingFailsLoudly()
        {
            NativeSettings.Init(true);
            Assert.ThrowsExactly<InvalidOperationException>(() => new NativeDeviceSettingsStore().Write("epson", new PrinterSettings()));
        }

        [TestMethod]
        public void ADeviceNameMustNotOpenAnotherSection()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new NativeDeviceSettingsStore().Write("a:b", new PrinterSettings()));
        }

        [TestMethod]
        public async Task TheCatalogBuildsDevicesFromTheNativeSettings()
        {
            using var factory = new PluginFactory(ScopeMode.PerAsyncContext);
            factory.RegisterAssembly("agenttest", typeof(TestPrinter).Assembly);
            factory.LoadPlugin<DeviceLoader>("loader", "[agenttest]<ITVComponents.AgentServices.Test.DeviceLoader>");
            using var catalog = new PluginAgentObjectCatalog(factory) { UniqueName = AgentCatalog.DefaultObjectName };

            var result = await catalog.ApplySettingsAsync("epson", "{\"printerName\":\"EPSON\",\"columns\":\"48\",\"printLogo\":\"true\"}");
            Assert.IsTrue(result.Success, result.Message);

            using var scope = factory.NewScope(new Dictionary<string, object>(), null, false);
            var epson = (TestPrinter)scope["epson", true];
            Assert.AreEqual("EPSON/48:x", await epson.PrintAsync("x"));
            Assert.IsTrue(epson.Settings.PrintLogo);
        }
    }
}
