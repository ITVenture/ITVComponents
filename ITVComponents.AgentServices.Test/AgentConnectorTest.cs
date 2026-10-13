using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.InterProcessCommunication.InMemory.Client;
using ITVComponents.InterProcessCommunication.InMemory.Server;
using ITVComponents.InterProcessCommunication.MessagingShared;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.EntityFramework.AgentServices;
using ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.AgentServices.Test
{
    /// <summary>Eine schlanke ClientApp — nur die Core-Schnittstelle, ohne Rechte-Kette.</summary>
    public class TestClientApp : IClientAppCore
    {
        public int ClientAppId { get; set; }
        public int TenantId { get; set; }
        public int ClientAppTemplateId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientKey { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public DateTime? LastSeenUtc { get; set; }
    }

    public class TestClientAppTemplate : IClientAppTemplateCore
    {
        public int ClientAppTemplateId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CatalogObjectName { get; set; } = null!;
        public string TypeKey { get; set; } = null!;
    }

    public class AgentTestContext : DbContext, IAgentServiceBindingContext,
        IClientAppContext<TestClientApp, TestClientAppTemplate>, ITenantScopeContext
    {
        public AgentTestContext(DbContextOptions<AgentTestContext> options, int? tenantId) : base(options)
        {
            CurrentTenantId = tenantId;
        }

        public int? CurrentTenantId { get; }

        public DbSet<TestClientApp> ClientApps { get; set; } = null!;

        public DbSet<TestClientAppTemplate> ClientAppTemplates { get; set; } = null!;

        public DbSet<ClientAppServiceBinding> ClientAppServiceBindings { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestClientApp>().HasKey(a => a.ClientAppId);
            modelBuilder.Entity<TestClientAppTemplate>().HasKey(t => t.ClientAppTemplateId);
            modelBuilder.Entity<TestClientAppTemplate>().Property(t => t.CatalogObjectName).IsRequired(false);
            modelBuilder.Entity<TestClientAppTemplate>().Property(t => t.TypeKey).IsRequired(false);
            modelBuilder.ConfigureAgentServices<Tenant, TestClientApp>();
        }
    }

    public class AgentTestContextFactory : IDbContextFactory<AgentTestContext>
    {
        private readonly DbContextOptions<AgentTestContext> options;

        public AgentTestContextFactory(string database, int? tenantId)
        {
            options = new DbContextOptionsBuilder<AgentTestContext>().UseInMemoryDatabase(database).Options;
            TenantId = tenantId;
        }

        public int? TenantId { get; set; }

        public AgentTestContext CreateDbContext() => new(options, TenantId);
    }

    public class HubClientFactory : IAgentClientFactory
    {
        private readonly IServiceHubProvider hub;
        private readonly Dictionary<string, InMemoryClient> clients = new();

        public HubClientFactory(IServiceHubProvider hub)
        {
            this.hub = hub;
        }

        public IBaseClient GetClient(string serviceName)
        {
            if (!clients.TryGetValue(serviceName, out var client))
            {
                client = new InMemoryClient(hub, serviceName, false);
                clients[serviceName] = client;
            }

            return client;
        }

        public void Dispose()
        {
            foreach (var c in clients.Values)
            {
                c.Dispose();
            }
        }
    }

    /// <summary>
    /// Der Weg der Web-Anwendung zum Agenten — über einen echten (InMemory-)Verteiler mit zwei Kassen in zwei
    /// Mandanten. Der wichtigste Satz hier: eine Kasse eines fremden Mandanten gibt es nicht.
    /// </summary>
    [TestClass]
    public class AgentConnectorTest
    {
        private const string KeyA = "kasse-a";
        private const string KeyB = "kasse-b";

        private MessageServiceHub hub = null!;
        private PluginFactory factory = null!;
        private PluginAgentObjectCatalog catalog = null!;
        private readonly List<InMemoryServer> servers = new();
        private HubClientFactory clients = null!;
        private AgentTestContextFactory db = null!;

        [TestInitialize]
        public void Setup()
        {
            factory = new PluginFactory(ScopeMode.PerAsyncContext);
            factory.RegisterAssembly("agenttest", typeof(TestPrinter).Assembly);
            factory.LoadPlugin<DeviceLoader>("loader", "[agenttest]<ITVComponents.AgentServices.Test.DeviceLoader>");
            catalog = new PluginAgentObjectCatalog(factory, new MemoryStore());

            hub = new MessageServiceHub();
            // Kasse A stellt ihren Katalog unter einem ERSATZ-Namen bereit (so steht es an ihrem Template),
            // Kasse B unter dem Standardnamen.
            Serve(KeyA, new Dictionary<string, object>
            {
                { "SpareCatalog", catalog },
                { "epson", new TestPrinter(new PrinterSettings { PrinterName = "A-EPSON" }) }
            });
            Serve(KeyB, new Dictionary<string, object>
            {
                { AgentCatalog.DefaultObjectName, catalog },
                { "epson", new TestPrinter(new PrinterSettings { PrinterName = "B-EPSON" }) }
            });
            clients = new HubClientFactory(hub);

            db = new AgentTestContextFactory(Guid.NewGuid().ToString(), 1);
            using var ctx = db.CreateDbContext();
            ctx.ClientAppTemplates.Add(new TestClientAppTemplate { ClientAppTemplateId = 1, Name = "pos-spare", CatalogObjectName = "SpareCatalog" });
            ctx.ClientAppTemplates.Add(new TestClientAppTemplate { ClientAppTemplateId = 2, Name = "pos" });
            ctx.ClientApps.Add(new TestClientApp { ClientAppId = 10, TenantId = 1, ClientAppTemplateId = 1, ClientKey = KeyA, ClientName = "Kasse A" });
            ctx.ClientApps.Add(new TestClientApp { ClientAppId = 20, TenantId = 2, ClientAppTemplateId = 2, ClientKey = KeyB, ClientName = "Kasse B" });
            ctx.ClientApps.Add(new TestClientApp { ClientAppId = 11, TenantId = 1, ClientAppTemplateId = 2, ClientKey = "kasse-off", ClientName = "Aus", Enabled = false });
            ctx.SaveChanges();
        }

        [TestCleanup]
        public void Cleanup()
        {
            clients.Dispose();
            foreach (var s in servers)
            {
                s.Dispose();
            }

            catalog.Dispose();
            factory.Dispose();
            hub.Dispose();
        }

        private void Serve(string serviceName, Dictionary<string, object> exposed)
        {
            var server = new InMemoryServer((IServiceHubProvider)hub, exposed, serviceName);
            server.Initialize();
            servers.Add(server);
        }

        private AgentConnector<AgentTestContext, TestClientApp, TestClientAppTemplate> Connector(bool withTransport = true)
            => new(db, NullLogger<AgentConnector<AgentTestContext, TestClientApp, TestClientAppTemplate>>.Instance,
                withTransport ? clients : null);

        [TestMethod]
        public async Task TheCatalogNameComesFromTheTemplate()
        {
            var a = await Connector().GetCatalogAsync(1, KeyA);
            Assert.IsTrue((await a.GetObjectsAsync(typeof(IReceiptPrinter).FullName!)).Any(),
                "Till A offers its catalog only under the name its template names.");

            var b = await Connector().GetCatalogAsync(2, KeyB);
            Assert.IsTrue((await b.GetObjectsAsync(typeof(IReceiptPrinter).FullName!)).Any(),
                "Without a name at the template the default name applies.");
        }

        [TestMethod]
        public async Task ATillOfAnotherTenantDoesNotExist()
        {
            var ex = await Assert.ThrowsExactlyAsync<AgentConnectionException>(() => Connector().GetCatalogAsync(1, KeyB));
            Assert.AreEqual(AgentConnectionFailure.UnknownAgent, ex.Failure,
                "The right ClientKey from the wrong tenant must not reach the agent.");

            await Assert.ThrowsExactlyAsync<AgentConnectionException>(
                () => Connector().GetObjectAsync<IReceiptPrinter>(1, KeyB, "epson"));
        }

        [TestMethod]
        public async Task ADisabledTillIsRefused()
        {
            var ex = await Assert.ThrowsExactlyAsync<AgentConnectionException>(() => Connector().GetCatalogAsync(1, "kasse-off"));
            Assert.AreEqual(AgentConnectionFailure.Disabled, ex.Failure);
        }

        [TestMethod]
        public async Task WithoutTransportTheFailureSaysSo()
        {
            var ex = await Assert.ThrowsExactlyAsync<AgentConnectionException>(() => Connector(false).GetCatalogAsync(1, KeyA));
            Assert.AreEqual(AgentConnectionFailure.NotConfigured, ex.Failure);
        }

        [TestMethod]
        public async Task AnObjectTheAgentDoesNotOfferIsUnreachable()
        {
            var ex = await Assert.ThrowsExactlyAsync<AgentConnectionException>(
                () => Connector().GetObjectAsync<IReceiptPrinter>(1, KeyA, "nosuchdevice"));
            Assert.AreEqual(AgentConnectionFailure.Unreachable, ex.Failure);
        }

        [TestMethod]
        public async Task TheResolverNamesEveryReasonSeparately()
        {
            var slots = new[]
            {
                new AgentServiceSlot("receiptPrinter", "Bondrucker", typeof(IReceiptPrinter)),
                new AgentServiceSlot("cashDrawer", "Schublade", typeof(ICashDrawer))
            };
            var resolver = new AgentServiceResolver<AgentTestContext, TestClientApp, TestClientAppTemplate>(db, Connector(),
                slots, NullLogger<AgentServiceResolver<AgentTestContext, TestClientApp, TestClientAppTemplate>>.Instance);

            Assert.AreEqual(AgentServiceStatus.UnknownSlot, (await resolver.GetAsync<IReceiptPrinter>(10, "label")).Status);
            Assert.AreEqual(AgentServiceStatus.ContractMismatch, (await resolver.GetAsync<IReceiptPrinter>(10, "cashDrawer")).Status);
            Assert.AreEqual(AgentServiceStatus.NotBound, (await resolver.GetAsync<IReceiptPrinter>(10, "receiptPrinter")).Status);
            Assert.AreEqual(AgentServiceStatus.UnknownClientApp, (await resolver.GetAsync<IReceiptPrinter>(20, "receiptPrinter")).Status,
                "Till B belongs to tenant 2; in tenant 1 it does not exist.");

            using (var ctx = db.CreateDbContext())
            {
                ctx.ClientAppServiceBindings.Add(new ClientAppServiceBinding { TenantId = 1, ClientAppId = 10, Slot = "receiptPrinter", ObjectName = "epson", Enabled = false });
                ctx.SaveChanges();
            }

            Assert.AreEqual(AgentServiceStatus.Disabled, (await resolver.GetAsync<IReceiptPrinter>(10, "receiptPrinter")).Status);

            using (var ctx = db.CreateDbContext())
            {
                ctx.ClientAppServiceBindings.Single().Enabled = true;
                ctx.SaveChanges();
            }

            var handle = await resolver.GetAsync<IReceiptPrinter>(10, "receiptPrinter");
            Assert.AreEqual(AgentServiceStatus.Ok, handle.Status, handle.Message);
            Assert.AreEqual("A-EPSON/42:x", await handle.Service!.PrintAsync("x"), "The call goes over the hub to till A's device.");

            db.TenantId = null;
            Assert.AreEqual(AgentServiceStatus.NoTenant, (await resolver.GetAsync<IReceiptPrinter>(10, "receiptPrinter")).Status);
        }

        [TestMethod]
        public async Task TheRemoteObjectCatalogAsksInTheActiveTenant()
        {
            var remote = new AgentRemoteObjectCatalog<AgentTestContext>(db, Connector(),
                NullLogger<AgentRemoteObjectCatalog<AgentTestContext>>.Instance);
            var objects = await remote.GetObjectsAsync(KeyA, typeof(IReceiptPrinter), CancellationToken.None);
            Assert.IsTrue(objects.Any(o => o.Name == "epson" && o.DisplayName == "Epson an der Theke"));

            await Assert.ThrowsExactlyAsync<AgentConnectionException>(
                () => remote.GetObjectsAsync(KeyB, typeof(IReceiptPrinter), CancellationToken.None));
        }
    }
}
