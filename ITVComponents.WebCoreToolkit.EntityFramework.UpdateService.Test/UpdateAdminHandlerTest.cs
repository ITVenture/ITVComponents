using System;
using System.Collections.Generic;
using System.IO;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Test
{
    /// <summary>
    /// The handler of the update masks checks permission and tenant itself (phase 5c).
    /// </summary>
    [TestClass]
    public class UpdateAdminHandlerTest
    {
        private UpdateTestContextFactory factory = null!;
        private Gate gate = null!;
        private UpdateAdminHandler<UpdateTestContext, TestClientApp, TestClientAppTemplate> handler = null!;

        [TestInitialize]
        public async Task Setup()
        {
            factory = new UpdateTestContextFactory { TenantId = 7 };
            gate = new Gate();
            var releases = new UpdateReleaseAdministration<UpdateTestContext, TestClientApp, TestClientAppTemplate>(factory, new DatabaseUpdatePackageStore<UpdateTestContext>(factory));
            var sync = new ClientAppUpdateSync<UpdateTestContext, TestClientApp, TestClientAppTemplate>(factory, new NoAgents(),
                Microsoft.Extensions.Options.Options.Create(new UpdateAgentOptions()), NullLogger<ClientAppUpdateSync<UpdateTestContext, TestClientApp, TestClientAppTemplate>>.Instance);
            var assignments = new ModuleAssignmentAdministration<UpdateTestContext, TestClientApp, TestClientAppTemplate>(factory, sync,
                NullLogger<ModuleAssignmentAdministration<UpdateTestContext, TestClientApp, TestClientAppTemplate>>.Instance);
            handler = new UpdateAdminHandler<UpdateTestContext, TestClientApp, TestClientAppTemplate>(factory, releases, assignments, sync, gate,
                NullLogger<UpdateAdminHandler<UpdateTestContext, TestClientApp, TestClientAppTemplate>>.Instance);
            await releases.EnsureProductAsync("Shop.Agent", PackageKinds.Base, "Shop agent", null, CancellationToken.None);
            await releases.EnsureProductAsync("Shop.Epson", PackageKinds.Module, "Epson printer", "Shop.Agent", CancellationToken.None);
            await using var db = factory.CreateDbContext();
            db.ClientAppTemplates.Add(new TestClientAppTemplate { ClientAppTemplateId = 3, Name = "Till" });
            db.ClientApps.Add(new TestClientApp { ClientAppId = 1, TenantId = 7, ClientAppTemplateId = 3, ClientName = "Kasse 1", ClientKey = "till-1" });
            db.ClientApps.Add(new TestClientApp { ClientAppId = 2, TenantId = 8, ClientAppTemplateId = 3, ClientName = "Kasse 2", ClientKey = "till-2" });
            db.ClientAppTemplateModuleAssignments.Add(new ClientAppTemplateModuleAssignment { ClientAppTemplateId = 3, ModuleProductKey = "Shop.Epson" });
            await db.SaveChangesAsync();
        }

        [TestMethod]
        public async Task Without_Permission_Nothing_Is_Read_Or_Changed()
        {
            Assert.IsFalse(handler.CanViewReleases());
            await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() => handler.GetProductsAsync());
            await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() => handler.GetClientAppAsync(1));

            gate.Granted.Add(UpdatePermissions.ReleasesView);
            Assert.AreEqual(2, (await handler.GetProductsAsync()).Count);
            Assert.IsFalse(handler.CanManageReleases());
            await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() => handler.UploadAsync(new ReleaseUpload { ProductKey = "Shop.Agent", Version = "1.0", FileName = "a.msi" }, new MemoryStream(new byte[10])));
            await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() => handler.SetTemplateModulesAsync(3, Array.Empty<string>()));
            await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() => handler.GetClientAppAsync(1), "the tab needs the permission on client apps");
        }

        [TestMethod]
        public async Task The_Tab_Shows_A_Till_Of_The_Active_Tenant_Only()
        {
            gate.Granted.Add(UpdatePermissions.ClientAppsView);

            var info = await handler.GetClientAppAsync(1);

            CollectionAssert.AreEqual(new[] { "Shop.Epson" }, info.TemplateModules);
            Assert.AreEqual("Shop.Epson", info.AvailableModules.Single().ProductKey);
            Assert.IsNull(info.State, "never reported");
            var refused = await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() => handler.GetClientAppAsync(2));
            StringAssert.Contains(refused.Message, "does not exist");
            await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() => handler.SetClientAppModulesAsync(1, new[] { "Shop.Epson" }), "reading is not assigning");

            factory.TenantId = null;
            await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() => handler.GetClientAppAsync(1), "outside a tenant");
        }

        [TestMethod]
        public async Task Refused_Input_Becomes_A_Message_And_A_Module_Release_Needs_No_Warning()
        {
            gate.Granted.Add(UpdatePermissions.ReleasesWrite);
            await handler.UploadAsync(new ReleaseUpload { ProductKey = "Shop.Agent", Version = "1.0", FileName = "a.msi" }, new MemoryStream(new byte[10]));

            var twice = await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() =>
                handler.UploadAsync(new ReleaseUpload { ProductKey = "Shop.Agent", Version = "1.0", FileName = "a.msi" }, new MemoryStream(new byte[10])));
            Assert.IsFalse(string.IsNullOrWhiteSpace(twice.Message));
            await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() => handler.CleanupAsync("Shop.Agent", "stable", 0));
            await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() => handler.SetTemplateModulesAsync(3, new[] { "Shop.Unknown" }));

            Assert.AreEqual(0, (await handler.CheckBeforeUploadAsync("Shop.Epson", "stable", "9.0")).Count);
            Assert.AreEqual("Shop.Epson", (await handler.CheckBeforeUploadAsync("Shop.Agent", "stable", "2.0")).Single().ModuleProductKey,
                "an assigned module without any release does not fit");
            Assert.AreEqual("Till", (await handler.GetTemplatesAsync()).Single().Name);
        }

        private sealed class Gate : IUpdatePermissionGate
        {
            public HashSet<string> Granted { get; } = new();

            public bool Holds(params string[] permissions) => permissions.Any(Granted.Contains);
        }

        private sealed class NoAgents : IAgentConnector
        {
            public Task<IAgentObjectCatalog> GetCatalogAsync(int tenantId, string clientKey, CancellationToken cancellationToken = default)
                => throw new AgentConnectionException(AgentConnectionFailure.Unreachable, "off");

            public Task<T> GetObjectAsync<T>(int tenantId, string clientKey, string objectName, CancellationToken cancellationToken = default) where T : class
                => throw new AgentConnectionException(AgentConnectionFailure.Unreachable, "off");
        }
    }
}
