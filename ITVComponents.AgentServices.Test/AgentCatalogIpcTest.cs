using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.InterProcessCommunication.InMemory.Client;
using ITVComponents.InterProcessCommunication.InMemory.Server;
using ITVComponents.InterProcessCommunication.MessagingShared;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.Plugins;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.AgentServices.Test
{
    /// <summary>
    /// Der Katalog über die Interprozess-Kommunikation (InMemory-Transport, voller JSON-Round-Trip): so,
    /// wie die Web-Anwendung ihn am Agenten anspricht.
    /// </summary>
    [TestClass]
    public class AgentCatalogIpcTest
    {
        private const string ClientKey = "kasse-1";

        private MessageServiceHub hub = null!;
        private PluginFactory factory = null!;
        private PluginAgentObjectCatalog catalog = null!;
        private InMemoryServer server = null!;
        private InMemoryClient client = null!;
        private IAgentObjectCatalog remote = null!;

        [TestInitialize]
        public void Setup()
        {
            factory = new PluginFactory(ScopeMode.PerAsyncContext);
            factory.RegisterAssembly("agenttest", typeof(TestPrinter).Assembly);
            factory.LoadPlugin<DeviceLoader>("loader", "[agenttest]<ITVComponents.AgentServices.Test.DeviceLoader>");
            catalog = new PluginAgentObjectCatalog(factory, new MemoryStore()) { UniqueName = AgentCatalog.DefaultObjectName };

            hub = new MessageServiceHub();
            server = new InMemoryServer((IServiceHubProvider)hub,
                new Dictionary<string, object> { { AgentCatalog.DefaultObjectName, catalog } }, ClientKey);
            server.Initialize();

            client = new InMemoryClient((IServiceHubProvider)hub, ClientKey, false);
            remote = client.CreateProxy<IAgentObjectCatalog>(AgentCatalog.DefaultObjectName);
        }

        [TestCleanup]
        public void Cleanup()
        {
            client.Dispose();
            server.Dispose();
            catalog.Dispose();
            factory.Dispose();
            hub.Dispose();
        }

        [TestMethod]
        public async Task TheWholeSetupRoundTripsOverIpc()
        {
            var printers = await remote.GetObjectsAsync(typeof(IReceiptPrinter).FullName!);
            Assert.AreEqual("Epson an der Theke", printers.Single(p => p.Name == "epson").DisplayName);

            var fields = await remote.DescribeSettingsAsync("epson");
            Assert.AreEqual(SettingKind.Choice, fields.Single(f => f.Name == "printerName").Kind, "Enums must round-trip.");
            Assert.AreEqual("42", fields.Single(f => f.Name == "columns").DefaultValue);

            var choices = await remote.GetChoicesAsync("epson", "printerName", null);
            Assert.AreEqual("EPSON TM-T20III Receipt", choices[0].Label);

            Assert.IsNull(await remote.GetSettingsAsync("epson"));

            var refused = await remote.ApplySettingsAsync("epson", "{\"secret\":\"x\"}");
            Assert.IsFalse(refused.Success);
            Assert.IsTrue(refused.Errors.Any(e => e.SettingName == "secret"), "Field errors must round-trip.");

            var applied = await remote.ApplySettingsAsync("epson", "{\"printerName\":\"EPSON\"}");
            Assert.IsTrue(applied.Success, applied.Message);
            Assert.IsNotNull(await remote.GetSettingsAsync("epson"));
        }
    }
}
