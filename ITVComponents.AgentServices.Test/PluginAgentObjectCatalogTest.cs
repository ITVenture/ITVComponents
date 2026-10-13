using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.Plugins;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.AgentServices.Test
{
    /// <summary>
    /// Der Katalog am Agenten: anbieten, beschreiben, einrichten — und dabei nie ein Gerät bauen.
    /// </summary>
    [TestClass]
    public class PluginAgentObjectCatalogTest
    {
        private PluginFactory factory = null!;
        private MemoryStore store = null!;
        private PluginAgentObjectCatalog catalog = null!;

        [TestInitialize]
        public void Setup()
        {
            factory = new PluginFactory(ScopeMode.PerAsyncContext);
            factory.RegisterAssembly("agenttest", typeof(TestPrinter).Assembly);
            factory.LoadPlugin<DeviceLoader>("loader", "[agenttest]<ITVComponents.AgentServices.Test.DeviceLoader>");
            store = new MemoryStore();
            catalog = new PluginAgentObjectCatalog(factory, store) { UniqueName = AgentCatalog.DefaultObjectName };
        }

        [TestCleanup]
        public void Cleanup()
        {
            catalog.Dispose();
            factory.Dispose();
        }

        [TestMethod]
        public async Task GetObjects_FiltersByContractAndHidesDisabled()
        {
            var printers = await catalog.GetObjectsAsync(typeof(IReceiptPrinter).FullName!);
            CollectionAssert.AreEquivalent(new[] { "epson", "star", "exploding" }, printers.Select(p => p.Name).ToArray(),
                "Only printers, and never the locally disabled one.");
            Assert.AreEqual("Epson an der Theke", printers.Single(p => p.Name == "epson").DisplayName);
            Assert.IsNull(printers.Single(p => p.Name == "star").DisplayName, "No display name configured = none.");
            Assert.IsTrue(printers.All(p => p.HasSettings));

            var drawers = await catalog.GetObjectsAsync(typeof(ICashDrawer).FullName!);
            Assert.AreEqual("drawer", drawers.Single().Name);
            Assert.IsFalse(drawers.Single().HasSettings);

            Assert.AreEqual(0, (await catalog.GetObjectsAsync("No.Such.Contract")).Length,
                "An unknown contract is not an error, just nothing.");
        }

        [TestMethod]
        public async Task ADeviceWhoseConstructorThrowsCanStillBeListedAndDescribed()
        {
            // Der Beweis, dass der Katalog nichts baut: dieser Konstruktor wirft.
            var listed = await catalog.GetObjectsAsync(typeof(IReceiptPrinter).FullName!);
            Assert.IsTrue(listed.Any(p => p.Name == "exploding"));

            var fields = await catalog.DescribeSettingsAsync("exploding");
            Assert.AreEqual(3, fields.Length);
        }

        [TestMethod]
        public async Task DescribeSettings_FollowsTheSettingsTypeOnly()
        {
            var fields = await catalog.DescribeSettingsAsync("epson");
            CollectionAssert.AreEqual(new[] { "printerName", "columns", "printLogo" }, fields.Select(f => f.Name).ToArray(),
                "Only properties carrying [DeviceSetting] are fields - 'Secret' is none.");

            var printer = fields[0];
            Assert.AreEqual(SettingKind.Choice, printer.Kind, "A choice source makes a choice.");
            Assert.AreEqual("printers", printer.ChoiceSource);
            Assert.IsTrue(printer.Required);

            Assert.AreEqual(SettingKind.Number, fields[1].Kind);
            Assert.AreEqual("42", fields[1].DefaultValue);
            Assert.AreEqual(SettingKind.Boolean, fields[2].Kind);
            Assert.AreEqual("false", fields[2].DefaultValue);

            Assert.AreEqual(0, (await catalog.DescribeSettingsAsync("drawer")).Length, "No settings type = no fields.");
            Assert.AreEqual(0, (await catalog.DescribeSettingsAsync("nobody")).Length);
        }

        [TestMethod]
        public async Task GetChoices_AsksTheProviderNotTheDevice()
        {
            var choices = await catalog.GetChoicesAsync("exploding", "printerName", "{\"columns\":\"48\"}");
            Assert.AreEqual(2, choices.Length, "The choice provider answers; the exploding device is never built.");
            Assert.AreEqual("Star (draft: 48)", choices[1].Label, "The draft values reach the provider.");
        }

        [TestMethod]
        public async Task ApplySettings_RejectsWhatIsNoField()
        {
            var result = await catalog.ApplySettingsAsync("epson", "{\"printerName\":\"EPSON\",\"secret\":\"x\"}");
            Assert.IsFalse(result.Success);
            Assert.AreEqual("secret", result.Errors.Single().SettingName,
                "A key that is no field must be refused - otherwise construction could sneak in through the settings.");
            Assert.AreEqual(0, store.Values.Count, "Nothing is stored when anything is wrong.");
        }

        [TestMethod]
        public async Task ApplySettings_InsistsOnRequiredValuesWithoutDefault()
        {
            var result = await catalog.ApplySettingsAsync("epson", "{\"printLogo\":true}");
            Assert.IsFalse(result.Success);
            Assert.AreEqual("printerName", result.Errors.Single().SettingName,
                "Columns is required too, but its default counts as a value.");
        }

        [TestMethod]
        public async Task ApplySettings_RejectsAValueOfTheWrongKind()
        {
            var result = await catalog.ApplySettingsAsync("epson", "{\"printerName\":\"EPSON\",\"columns\":\"many\"}");
            Assert.IsFalse(result.Success);
            Assert.AreEqual("columns", result.Errors.Single().SettingName);
        }

        [TestMethod]
        public async Task ApplySettings_StoresOnlyTheFieldsAndReadsThemBack()
        {
            var result = await catalog.ApplySettingsAsync("epson", "{\"printerName\":\"EPSON TM-T20III\",\"columns\":\"48\"}");
            Assert.IsTrue(result.Success, result.Message);

            var stored = await catalog.GetSettingsAsync("epson");
            Assert.IsNotNull(stored);
            using var document = JsonDocument.Parse(stored!);
            Assert.AreEqual(48, document.RootElement.GetProperty("columns").GetInt32(), "Text from a form is stored as a number.");
            Assert.IsFalse(document.RootElement.TryGetProperty("secret", out _), "Only fields are stored.");

            Assert.IsNull(await catalog.GetSettingsAsync("star"), "Not set up = null, not an empty object.");
        }

        [TestMethod]
        public async Task ApplySettings_AcceptsSwitchesAsText()
        {
            // Eine Maske liefert alles als Text - auch "true".
            var result = await catalog.ApplySettingsAsync("epson", "{\"printerName\":\"EPSON\",\"printLogo\":\"True\"}");
            Assert.IsTrue(result.Success, result.Message);
            using var document = JsonDocument.Parse((await catalog.GetSettingsAsync("epson"))!);
            Assert.IsTrue(document.RootElement.GetProperty("printLogo").GetBoolean());

            var refused = await catalog.ApplySettingsAsync("epson", "{\"printerName\":\"EPSON\",\"printLogo\":\"maybe\"}");
            Assert.AreEqual("printLogo", refused.Errors.Single().SettingName);
        }

        [TestMethod]
        public async Task ApplySettings_UnknownDeviceIsRefusedWithAMessage()
        {
            var result = await catalog.ApplySettingsAsync("nobody", "{}");
            Assert.IsFalse(result.Success);
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));

            var drawer = await catalog.ApplySettingsAsync("drawer", "{}");
            Assert.IsFalse(drawer.Success, "A device without settings has nothing to store.");
        }

        [TestMethod]
        public async Task EachDeviceIsBuiltWithItsOwnSettings()
        {
            await catalog.ApplySettingsAsync("epson", "{\"printerName\":\"EPSON\",\"columns\":48}");
            await catalog.ApplySettingsAsync("star", "{\"printerName\":\"Star\",\"columns\":32}");

            using var scope = factory.NewScope(new Dictionary<string, object>(), null, false);
            var epson = (TestPrinter)scope["epson", true];
            var star = (TestPrinter)scope["star", true];

            Assert.AreEqual("EPSON/48:x", await epson.PrintAsync("x"), "Two devices of the same type, each with its own settings.");
            Assert.AreEqual("Star/32:x", await star.PrintAsync("x"));
            Assert.AreEqual("untouched", epson.Settings.Secret);
        }

        [TestMethod]
        public void AnUnconfiguredDeviceIsBuiltWithItsDefaults()
        {
            using var scope = factory.NewScope(new Dictionary<string, object>(), null, false);
            var star = (TestPrinter)scope["star", true];
            Assert.AreEqual(42, star.Settings.Columns);
        }

        [TestMethod]
        public async Task ChangedSettingsApplyToTheNextScope()
        {
            await catalog.ApplySettingsAsync("epson", "{\"printerName\":\"A\"}");
            using (var first = factory.NewScope(new Dictionary<string, object>(), null, false))
            {
                Assert.AreEqual("A", ((TestPrinter)first["epson", true]).Settings.PrinterName);
            }

            await catalog.ApplySettingsAsync("epson", "{\"printerName\":\"B\"}");
            using var second = factory.NewScope(new Dictionary<string, object>(), null, false);
            Assert.AreEqual("B", ((TestPrinter)second["epson", true]).Settings.PrinterName,
                "Scoped devices live as long as their scope; the next one reads the new settings.");
        }
    }
}
