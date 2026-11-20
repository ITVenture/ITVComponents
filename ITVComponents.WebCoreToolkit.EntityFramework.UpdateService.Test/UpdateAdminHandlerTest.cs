using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Models;
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
        private Agents agents = null!;
        private UpdateAdminHandler<UpdateTestContext, TestClientApp, TestClientAppTemplate> handler = null!;

        [TestInitialize]
        public async Task Setup()
        {
            factory = new UpdateTestContextFactory { TenantId = 7 };
            gate = new Gate();
            agents = new Agents();
            var releases = new UpdateReleaseAdministration<UpdateTestContext, TestClientApp, TestClientAppTemplate>(factory, new DatabaseUpdatePackageStore<UpdateTestContext>(factory));
            var sync = new ClientAppUpdateSync<UpdateTestContext, TestClientApp, TestClientAppTemplate>(factory, agents,
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

        [TestMethod]
        public async Task Deselecting_An_Own_Module_Names_The_Devices_Created_From_It()
        {
            gate.Granted.Add(UpdatePermissions.ClientAppsWrite);
            await using (var db = factory.CreateDbContext())
            {
                db.ClientAppModuleAssignments.Add(new ClientAppModuleAssignment { TenantId = 7, ClientAppId = 1, ModuleProductKey = "Shop.Terminal" });
                await db.SaveChangesAsync();
            }

            agents.Objects["till-1"] =
            [
                new AgentModuleObjectInfo { Name = "Terminal1", ModuleId = "Shop.Terminal", Removable = true },
                new AgentModuleObjectInfo { Name = "terminalChoices", ModuleId = "Shop.Terminal", Removable = false },
                new AgentModuleObjectInfo { Name = "Bondrucker", ModuleId = "Shop.Epson", Removable = true }
            ];

            var keep = await handler.CheckClientAppModuleRemovalAsync(1, new[] { "Shop.Terminal" });
            Assert.AreEqual(0, keep.RemovedModules.Length);
            Assert.IsFalse(keep.NeedsConfirmation);
            Assert.AreEqual(0, agents.Calls, "nothing deselected, nobody asked");

            // Shop.Epson comes with the template and stays; only the own module goes.
            var check = await handler.CheckClientAppModuleRemovalAsync(1, Array.Empty<string>());
            CollectionAssert.AreEqual(new[] { "Shop.Terminal" }, check.RemovedModules);
            var warning = check.Warnings.Single();
            Assert.AreEqual("Kasse 1", warning.ClientName);
            Assert.AreEqual("Shop.Terminal", warning.ModuleProductKey);
            CollectionAssert.AreEqual(new[] { "Terminal1" }, warning.Instances, "infrastructure goes with its module without a warning");
            Assert.IsTrue(check.NeedsConfirmation);

            agents.Unreachable.Add("till-1");
            var off = await handler.CheckClientAppModuleRemovalAsync(1, Array.Empty<string>());
            CollectionAssert.AreEqual(new[] { "Kasse 1" }, off.Unreached.ToArray());
            Assert.IsTrue(off.NeedsConfirmation, "unknown is asked about too");

            agents.Unreachable.Clear();
            agents.Objects["till-1"] = [];
            Assert.IsFalse((await handler.CheckClientAppModuleRemovalAsync(1, Array.Empty<string>())).NeedsConfirmation, "no devices, no question");
        }

        [TestMethod]
        public async Task Deselecting_Names_The_Roles_That_Use_A_Device_Of_The_Module()
        {
            gate.Granted.Add(UpdatePermissions.ClientAppsWrite);
            await using (var db = factory.CreateDbContext())
            {
                db.ClientAppModuleAssignments.Add(new ClientAppModuleAssignment { TenantId = 7, ClientAppId = 1, ModuleProductKey = "Shop.Terminal" });
                db.ClientAppServiceBindings.Add(new ClientAppServiceBinding { TenantId = 7, ClientAppId = 1, Slot = "paymentTerminal", ObjectName = "terminal1" });
                db.ClientAppServiceBindings.Add(new ClientAppServiceBinding { TenantId = 7, ClientAppId = 1, Slot = "terminalLookup", ObjectName = "terminalChoices", Enabled = false });
                db.ClientAppServiceBindings.Add(new ClientAppServiceBinding { TenantId = 7, ClientAppId = 1, Slot = "receiptPrinter", ObjectName = "Bondrucker" });
                // Another till with the same device name is not this till.
                db.ClientAppServiceBindings.Add(new ClientAppServiceBinding { TenantId = 8, ClientAppId = 2, Slot = "other", ObjectName = "Terminal1" });
                await db.SaveChangesAsync();
            }

            agents.Objects["till-1"] =
            [
                new AgentModuleObjectInfo { Name = "Terminal1", ModuleId = "Shop.Terminal", Removable = true },
                new AgentModuleObjectInfo { Name = "terminalChoices", ModuleId = "Shop.Terminal", Removable = false },
                new AgentModuleObjectInfo { Name = "Bondrucker", ModuleId = "Shop.Epson", Removable = true }
            ];

            var warning = (await handler.CheckClientAppModuleRemovalAsync(1, Array.Empty<string>())).Warnings.Single();
            CollectionAssert.AreEqual(new[] { "Terminal1" }, warning.Instances);
            CollectionAssert.AreEqual(new[] { "paymentTerminal", "terminalLookup" }, warning.Bindings.Select(b => b.Slot).ToArray(),
                "a role on infrastructure of the module breaks as well; the printer of the template stays");
            Assert.IsFalse(warning.Bindings.Single(b => b.Slot == "terminalLookup").Enabled);

            // Only infrastructure left, but a role uses it: still asked.
            agents.Objects["till-1"] = [new AgentModuleObjectInfo { Name = "terminalChoices", ModuleId = "Shop.Terminal", Removable = false }];
            var check = await handler.CheckClientAppModuleRemovalAsync(1, Array.Empty<string>());
            Assert.IsTrue(check.NeedsConfirmation);
            Assert.AreEqual(0, check.Warnings.Single().Instances.Length);
            Assert.AreEqual("terminalLookup", check.Warnings.Single().Bindings.Single().Slot);
        }

        [TestMethod]
        public async Task Deselecting_A_Module_Of_A_Template_Asks_Every_Till_That_Loses_It()
        {
            await Assert.ThrowsExactlyAsync<UpdateAdministrationException>(() => handler.CheckTemplateModuleRemovalAsync(3, Array.Empty<string>()));
            gate.Granted.Add(UpdatePermissions.ReleasesWrite);
            agents.Objects["till-1"] = [new AgentModuleObjectInfo { Name = "Bondrucker", ModuleId = "Shop.Epson", Removable = true }];
            agents.Unreachable.Add("till-2");

            var check = await handler.CheckTemplateModuleRemovalAsync(3, Array.Empty<string>());

            CollectionAssert.AreEqual(new[] { "Shop.Epson" }, check.RemovedModules);
            Assert.AreEqual(7, check.Warnings.Single().TenantId);
            CollectionAssert.AreEqual(new[] { "Bondrucker" }, check.Warnings.Single().Instances);
            CollectionAssert.AreEqual(new[] { "Kasse 2" }, check.Unreached.ToArray(), "a till of another tenant is asked as well");

            // A till that has the module of its own keeps it and is not asked.
            await using (var db = factory.CreateDbContext())
            {
                db.ClientAppModuleAssignments.Add(new ClientAppModuleAssignment { TenantId = 8, ClientAppId = 2, ModuleProductKey = "Shop.Epson" });
                await db.SaveChangesAsync();
            }

            Assert.AreEqual(0, (await handler.CheckTemplateModuleRemovalAsync(3, Array.Empty<string>())).Unreached.Count);
            Assert.IsFalse((await handler.CheckTemplateModuleRemovalAsync(3, new[] { "Shop.Epson" })).NeedsConfirmation);
        }

        /// <summary>
        /// The agents: a catalog per till that only lists its module instances, or off.
        /// </summary>
        private sealed class Agents : IAgentConnector
        {
            public Dictionary<string, AgentModuleObjectInfo[]> Objects { get; } = new();

            public HashSet<string> Unreachable { get; } = new();

            public int Calls { get; private set; }

            public Task<IAgentObjectCatalog> GetCatalogAsync(int tenantId, string clientKey, CancellationToken cancellationToken = default)
            {
                Calls++;
                if (Unreachable.Contains(clientKey) || !Objects.TryGetValue(clientKey, out var objects))
                {
                    throw new AgentConnectionException(AgentConnectionFailure.Unreachable, "off");
                }

                return Task.FromResult<IAgentObjectCatalog>(new Catalog(objects));
            }

            public Task<T> GetObjectAsync<T>(int tenantId, string clientKey, string objectName, CancellationToken cancellationToken = default) where T : class
                => throw new AgentConnectionException(AgentConnectionFailure.Unreachable, "off");
        }

        private sealed class Catalog : IAgentObjectCatalog
        {
            private readonly AgentModuleObjectInfo[] objects;

            public Catalog(AgentModuleObjectInfo[] objects)
            {
                this.objects = objects;
            }

            public Task<AgentModuleObjectInfo[]> GetModuleObjectsAsync() => Task.FromResult(objects);

            public Task<AgentObjectInfo[]> GetObjectsAsync(string contractTypeName) => throw new NotSupportedException();

            public Task<SettingDescriptor[]> DescribeSettingsAsync(string objectName) => throw new NotSupportedException();

            public Task<SettingChoice[]> GetChoicesAsync(string objectName, string settingName, string? draftJson) => throw new NotSupportedException();

            public Task<string?> GetSettingsAsync(string objectName) => throw new NotSupportedException();

            public Task<ApplySettingsResult> ApplySettingsAsync(string objectName, string settingsJson) => throw new NotSupportedException();

            public Task<AgentTemplateInfo[]> GetTemplatesAsync(string contractTypeName) => throw new NotSupportedException();

            public Task<AgentObjectChangeResult> CreateObjectAsync(string templateKey, string name, string? settingsJson) => throw new NotSupportedException();

            public Task<AgentObjectChangeResult> RemoveObjectAsync(string name) => throw new NotSupportedException();

            public Task<SettingChoice[]> GetTemplateChoicesAsync(string templateKey, string settingName, string? draftJson) => throw new NotSupportedException();
        }
    }
}
