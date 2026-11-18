using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Administration;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Agents;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Test
{
    /// <summary>
    /// The exchange with the agents: the backend pushes the assignment of modules and stores the state (phase 5b).
    /// </summary>
    [TestClass]
    public class AgentSyncTest
    {
        private UpdateTestContextFactory factory = null!;
        private Connector connector = null!;
        private ClientAppUpdateSync<UpdateTestContext, TestClientApp, TestClientAppTemplate> sync = null!;
        private ModuleAssignmentAdministration<UpdateTestContext, TestClientApp, TestClientAppTemplate> assignments = null!;

        [TestInitialize]
        public async Task Setup()
        {
            factory = new UpdateTestContextFactory();
            connector = new Connector();
            sync = new ClientAppUpdateSync<UpdateTestContext, TestClientApp, TestClientAppTemplate>(factory, connector, Microsoft.Extensions.Options.Options.Create(new UpdateAgentOptions()),
                NullLogger<ClientAppUpdateSync<UpdateTestContext, TestClientApp, TestClientAppTemplate>>.Instance);
            assignments = new ModuleAssignmentAdministration<UpdateTestContext, TestClientApp, TestClientAppTemplate>(factory, sync,
                NullLogger<ModuleAssignmentAdministration<UpdateTestContext, TestClientApp, TestClientAppTemplate>>.Instance);
            var admin = new UpdateReleaseAdministration<UpdateTestContext, TestClientApp, TestClientAppTemplate>(factory, new DatabaseUpdatePackageStore<UpdateTestContext>(factory));
            await admin.EnsureProductAsync("Shop.Agent", PackageKinds.Base, "Shop agent", null, CancellationToken.None);
            await admin.EnsureProductAsync("Shop.Epson", PackageKinds.Module, "Epson printer", "Shop.Agent", CancellationToken.None);
            await admin.EnsureProductAsync("Shop.Terminal", PackageKinds.Module, "Card terminal", "Shop.Agent", CancellationToken.None);
            await using var db = factory.CreateDbContext();
            db.ClientApps.Add(new TestClientApp { ClientAppId = 1, TenantId = 7, ClientAppTemplateId = 3, ClientName = "Kasse 1", ClientKey = "till-1" });
            db.ClientApps.Add(new TestClientApp { ClientAppId = 2, TenantId = 8, ClientAppTemplateId = 3, ClientName = "Kasse 2", ClientKey = "till-2" });
            db.ClientApps.Add(new TestClientApp { ClientAppId = 3, TenantId = 8, ClientAppTemplateId = 3, ClientName = "Kasse 3", ClientKey = "till-3", Enabled = false });
            await db.SaveChangesAsync();
        }

        [TestMethod]
        public async Task A_Till_Gets_The_Modules_Of_Its_Template_And_Its_Own_And_The_State_Is_Stored()
        {
            await assignments.SetTemplateModulesAsync(3, new[] { "Shop.Epson" }, CancellationToken.None);
            connector.Bridge("till-1").Report.Status = new UpdateStatus
            {
                Phase = UpdatePhase.ReadyToInstall, InstalledVersion = "1.4.1", AvailableVersion = "2.0.0", BlockedBy = "waits for Shop.Terminal",
                Modules = new[] { new ModuleState { Id = "Shop.Epson", InstalledVersion = "1.0", Assigned = true } }
            };

            var result = await assignments.SetClientAppModulesAsync(7, 1, new[] { "shop.terminal" }, CancellationToken.None);

            Assert.IsTrue(result.Reached, result.Message);
            CollectionAssert.AreEqual(new[] { "Shop.Epson", "Shop.Terminal" }, connector.Bridge("till-1").Assigned, "template and own, in the spelling of the product");
            await using var db = factory.CreateDbContext();
            var state = db.ClientAppUpdateStates.Single(s => s.ClientAppId == 1);
            Assert.AreEqual(7, state.TenantId);
            Assert.AreEqual("ReadyToInstall", state.Phase);
            Assert.AreEqual("2.0.0", state.AvailableVersion);
            Assert.AreEqual("waits for Shop.Terminal", state.BlockedBy);
            Assert.AreEqual("1.0", state.ReadModules().Single().InstalledVersion);
        }

        [TestMethod]
        public async Task A_Template_Change_Reaches_Every_Enabled_Till_Of_It_In_All_Tenants()
        {
            connector.Unreachable.Add("till-2");

            var results = await assignments.SetTemplateModulesAsync(3, new[] { "Shop.Epson" }, CancellationToken.None);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, results.Select(r => r.ClientAppId).ToArray(), "the disabled till is left out");
            Assert.IsTrue(results.Single(r => r.ClientAppId == 1).Reached);
            Assert.AreEqual(AgentConnectionFailure.Unreachable, results.Single(r => r.ClientAppId == 2).Failure, "an agent that is off gets it on connect");
            CollectionAssert.AreEqual(new[] { "Shop.Epson" }, await assignments.GetTemplateModulesAsync(3, CancellationToken.None));
        }

        [TestMethod]
        public async Task An_Unknown_Module_Is_Not_Assigned()
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => assignments.SetClientAppModulesAsync(7, 1, new[] { "Shop.Unknown" }, CancellationToken.None));
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => assignments.SetClientAppModulesAsync(7, 1, new[] { "Shop.Agent" }, CancellationToken.None), "the application is no module");
            Assert.AreEqual(0, connector.Calls);
        }

        [TestMethod]
        public async Task A_Till_Of_Another_Tenant_Is_Not_Reached()
        {
            var result = await sync.RefreshStateAsync(8, 1, CancellationToken.None);

            Assert.IsFalse(result.Reached);
            Assert.AreEqual(AgentConnectionFailure.UnknownAgent, result.Failure);
            Assert.AreEqual(0, connector.Calls);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => assignments.SetClientAppModulesAsync(8, 1, new[] { "Shop.Epson" }, CancellationToken.None));
        }

        [TestMethod]
        public async Task An_Unreachable_Agent_Leaves_The_Stored_State_And_One_Without_Service_Says_So()
        {
            connector.Bridge("till-1").Report.Status = new UpdateStatus { Phase = UpdatePhase.UpToDate, InstalledVersion = "1.4.1" };
            await sync.RefreshStateAsync(7, 1, CancellationToken.None);
            connector.Unreachable.Add("till-1");

            Assert.IsFalse((await sync.RefreshStateAsync(7, 1, CancellationToken.None)).Reached);
            await using (var db = factory.CreateDbContext())
            {
                Assert.AreEqual("UpToDate", db.ClientAppUpdateStates.Single().Phase);
            }

            connector.Unreachable.Clear();
            connector.Bridge("till-1").Report = new AgentUpdateReport { ApplicationVersion = "1.4.1", ServiceConnected = false };
            var result = await sync.RefreshStateAsync(7, 1, CancellationToken.None);
            Assert.AreEqual(ClientAppUpdateStateExtensions.NoServicePhase, result.State!.Phase);
            Assert.AreEqual("1.4.1", result.State.InstalledVersion, "the version of the running application");
        }

        private sealed class Connector : IAgentConnector
        {
            private readonly Dictionary<string, Bridge> bridges = new();

            public HashSet<string> Unreachable { get; } = new();

            public int Calls { get; private set; }

            public Bridge Bridge(string clientKey)
            {
                if (!bridges.TryGetValue(clientKey, out var bridge))
                {
                    bridge = new Bridge();
                    bridges[clientKey] = bridge;
                }

                return bridge;
            }

            public Task<IAgentObjectCatalog> GetCatalogAsync(int tenantId, string clientKey, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<T> GetObjectAsync<T>(int tenantId, string clientKey, string objectName, CancellationToken cancellationToken = default) where T : class
            {
                Calls++;
                Assert.AreEqual(UpdateContractTypes.BridgeObjectName, objectName);
                if (Unreachable.Contains(clientKey))
                {
                    throw new AgentConnectionException(AgentConnectionFailure.Unreachable, $"Till '{clientKey}' cannot be reached.");
                }

                return Task.FromResult((T)(object)Bridge(clientKey));
            }
        }

        private sealed class Bridge : IAgentUpdateBridge
        {
            public AgentUpdateReport Report { get; set; } = new() { ServiceConnected = true, Status = new UpdateStatus() };

            public string[]? Assigned { get; private set; }

            public Task<AgentUpdateReport> GetReportAsync() => Task.FromResult(Report);

            public Task<AgentUpdateReport> AssignModulesAsync(string[] moduleIds)
            {
                Assigned = moduleIds;
                return Task.FromResult(Report);
            }

            public Task<bool> CheckNowAsync() => Task.FromResult(Report.ServiceConnected);
        }
    }
}
