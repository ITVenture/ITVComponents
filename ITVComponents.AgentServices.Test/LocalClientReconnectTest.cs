using System.Collections.Generic;
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
    /// Ein In-Prozess-Client (so, wie das Web die Agenten anspricht) muss sich erholen, wenn der Dienst
    /// kurz weg war - BUG-PRE253-LocalMessageClient-NeverReconnects.
    /// </summary>
    [TestClass]
    public class LocalClientReconnectTest
    {
        private const string ClientKey = "kasse-1";

        private MessageServiceHub hub = null!;
        private PluginFactory factory = null!;
        private PluginAgentObjectCatalog catalog = null!;

        [TestInitialize]
        public void Setup()
        {
            factory = new PluginFactory(ScopeMode.PerAsyncContext);
            factory.RegisterAssembly("agenttest", typeof(TestPrinter).Assembly);
            factory.LoadPlugin<DeviceLoader>("loader", "[agenttest]<ITVComponents.AgentServices.Test.DeviceLoader>");
            catalog = new PluginAgentObjectCatalog(factory, new MemoryStore()) { UniqueName = AgentCatalog.DefaultObjectName };
            hub = new MessageServiceHub();
        }

        [TestCleanup]
        public void Cleanup()
        {
            catalog.Dispose();
            factory.Dispose();
            hub.Dispose();
        }

        private InMemoryServer StartAgent()
        {
            var server = new InMemoryServer((IServiceHubProvider)hub,
                new Dictionary<string, object> { { AgentCatalog.DefaultObjectName, catalog } }, ClientKey);
            server.Initialize();
            return server;
        }

        [TestMethod]
        public void ClientRecoversWhenTheServiceComesBack()
        {
            var server = StartAgent();
            using var client = new InMemoryClient((IServiceHubProvider)hub, ClientKey, false);
            Assert.IsTrue(client.CheckRemoteObjectAvailability(AgentCatalog.DefaultObjectName).Available);

            server.Dispose();
            Assert.IsFalse(client.ValidateConnection(), "The agent is gone.");
            Assert.IsFalse(client.ValidateConnection(), "Still gone - this second test used to break the client for good.");

            server = StartAgent();
            try
            {
                Assert.IsTrue(client.ValidateConnection(), "The agent is back, the client must see it.");
                Assert.IsTrue(client.CheckRemoteObjectAvailability(AgentCatalog.DefaultObjectName).Available);
            }
            finally
            {
                server.Dispose();
            }
        }

        [TestMethod]
        public void ClientCreatedWhileTheServiceIsAbsentConnectsLater()
        {
            using var client = new InMemoryClient((IServiceHubProvider)hub, ClientKey, false);
            Assert.IsFalse(client.ValidateConnection());
            Assert.IsFalse(client.ValidateConnection());

            using var server = StartAgent();
            Assert.IsTrue(client.ValidateConnection());
            Assert.IsTrue(client.CheckRemoteObjectAvailability(AgentCatalog.DefaultObjectName).Available);
        }

        [TestMethod]
        public void BidirectionalClientRegistersItsReturnChannelOnceAndReleasesIt()
        {
            using var server = StartAgent();
            var client = new InMemoryClient((IServiceHubProvider)hub, ClientKey, true);
            Assert.IsTrue(client.ValidateConnection());
            Assert.IsTrue(client.ValidateConnection(), "A second test must not register the return channel again.");
            Assert.IsTrue(client.IsBidirectional);
            Assert.IsTrue(client.CheckRemoteObjectAvailability(AgentCatalog.DefaultObjectName).Available);

            client.Dispose();
        }

        [TestMethod]
        public void LocalServiceCanRegisterAgainAfterDispose()
        {
            var first = StartAgent();
            first.Dispose();

            using var second = StartAgent();
            using var client = new InMemoryClient((IServiceHubProvider)hub, ClientKey, false);
            Assert.IsTrue(client.CheckRemoteObjectAvailability(AgentCatalog.DefaultObjectName).Available);
        }
    }
}
