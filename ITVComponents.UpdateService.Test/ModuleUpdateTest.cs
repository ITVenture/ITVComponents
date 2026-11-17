using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.NamedPipe.Security;
using ITVComponents.Plugins;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.UpdateService.Agents;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Installation;
using ITVComponents.UpdateService.Modules;
using ITVComponents.UpdateService.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.UpdateService.Test
{
    /// <summary>
    /// Modules (issue update service, P4, M1, M3): assignment, one pass with the application, switching back.
    /// </summary>
    [TestClass]
    public class ModuleUpdateTest
    {
        private string directory = null!;
        private string host = null!;
        private string modulesRoot = null!;
        private UpdateServiceOptions options = null!;
        private Feed feed = null!;
        private Downloader downloader = null!;
        private Installer installer = null!;
        private Application application = null!;
        private AgentRegistry registry = null!;
        private InstallStateStore store = null!;
        private UpdateCoordinator coordinator = null!;
        private readonly List<ShutdownRequest> requests = new List<ShutdownRequest>();

        [TestInitialize]
        public void Setup()
        {
            directory = Directory.CreateTempSubdirectory("itv-modules-update").FullName;
            host = Directory.CreateDirectory(Path.Combine(directory, "app")).FullName;
            modulesRoot = Path.Combine(host, "modules");
            options = new UpdateServiceOptions
            {
                ProductId = "Shop.Agent",
                FeedUrl = "https://shop.example/updates",
                DataDirectory = directory,
                ApplicationPath = Path.Combine(host, "Agent.exe"),
                RequireApplicationPath = false
            };
            feed = new Feed();
            downloader = new Downloader(directory);
            installer = new Installer();
            application = new Application { Installed = new Version(1, 4, 1) };
            registry = new AgentRegistry(options);
            store = new InstallStateStore(directory);
            var modules = new ModuleManager(options, downloader, new ModuleFiles(modulesRoot, host), store);
            coordinator = new UpdateCoordinator(options, feed, downloader, new Verifier(), installer, new MsiInstallResultEvaluator(), store, registry, application, directory, null, modules);
            coordinator.ShutdownRequested += r => requests.Add(r);
            installer.Installed = () => application.Installed = new Version(2, 0, 0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            coordinator.Dispose();
        }

        [TestMethod]
        public void Version_Ranges_Follow_The_NuGet_Notation()
        {
            Assert.IsTrue(VersionRange.Parse("[1.4.0,2.0.0)").Includes(new Version(1, 9, 9)));
            Assert.IsFalse(VersionRange.Parse("[1.4.0,2.0.0)").Includes(new Version(2, 0)));
            Assert.IsTrue(VersionRange.Parse("1.4").Includes(new Version(7, 0)), "a single version is a minimum");
            Assert.IsFalse(VersionRange.Parse("(1.4,]").Includes(new Version(1, 4, 0)));
            Assert.IsTrue(VersionRange.Parse("[1.4.2]").Includes(new Version(1, 4, 2)));
            Assert.ThrowsExactly<FormatException>(() => VersionRange.Parse("[1.4"));
        }

        [TestMethod]
        public void An_Entry_Pointing_Outside_The_Module_Folder_Refuses_The_Package()
        {
            var zip = Path.Combine(directory, "evil.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(archive.CreateEntry("../../evil.dll").Open());
                writer.Write("x");
            }

            Assert.ThrowsExactly<InvalidDataException>(() => new ModuleFiles(modulesRoot, host).Stage(zip, new UpdatePackage { Id = "Evil", Version = "1.0" }));
            Assert.IsFalse(File.Exists(Path.Combine(directory, "evil.dll")));
        }

        [TestMethod]
        public async Task An_Assigned_Module_Is_Installed_When_The_Application_Is_Down_And_Confirmed_By_Its_Report()
        {
            feed.Offer(Module("EpsonTM", "1.0", "[1.4.0,2.0.0)"));
            coordinator.AssignModules(new[] { "EpsonTM" });
            var agent = Connect(session: 2);

            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(1, requests.Count, "the application is asked to end for the module");
            Assert.IsNull(requests[0].Version, "the application itself is not updated");

            registry.Remove(agent.ConnectionId);
            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.IsTrue(File.Exists(Path.Combine(modulesRoot, "EpsonTM", "EpsonTM.dll")));
            Assert.AreEqual("1.0", store.Load().Modules["EpsonTM"].Version);
            Assert.IsNotNull(store.Load().ModuleActivation, "the switch waits for the confirmation");
            CollectionAssert.AreEqual(new[] { 2 }, application.Launched);
            Assert.AreEqual(0, installer.Started, "no MSI for a module");

            coordinator.ReportModules(new[] { new ModuleLoadReport { ModuleId = "EpsonTM", Loaded = true } });
            Assert.IsNull(store.Load().ModuleActivation);
        }

        [TestMethod]
        public async Task A_Module_The_Application_Cannot_Load_Is_Switched_Back_To_Its_Former_Version()
        {
            await InstallModule("EpsonTM", "1.0");
            coordinator.ReportModules(new[] { new ModuleLoadReport { ModuleId = "EpsonTM", Loaded = true } });

            feed.Offer(Module("EpsonTM", "1.0", "[1.4.0,2.0.0)"), Module("EpsonTM", "1.1", "[1.4.0,2.0.0)"));
            coordinator.CheckNow();
            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual("1.1", store.Load().Modules["EpsonTM"].Version);
            Assert.IsTrue(Directory.Exists(Path.Combine(modulesRoot, "EpsonTM.old")), "the former version is kept");

            var agent = Connect(session: 1);
            coordinator.ReportModules(new[] { new ModuleLoadReport { ModuleId = "EpsonTM", Loaded = false, Error = "missing driver" } });
            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.IsTrue(requests.Count > 0, "the application is asked to end so that the switch can be undone");

            registry.Remove(agent.ConnectionId);
            await coordinator.RunOnceAsync(CancellationToken.None);

            var state = store.Load();
            Assert.AreEqual("1.0", state.Modules["EpsonTM"].Version);
            Assert.AreEqual("1.1", state.FailedModules["EpsonTM"].Version);
            StringAssert.Contains(state.FailedModules["EpsonTM"].Reason, "missing driver");
            Assert.IsFalse(Directory.Exists(Path.Combine(modulesRoot, "EpsonTM.old")));
        }

        [TestMethod]
        public async Task An_Application_Ending_Right_After_The_Switch_Without_Report_Counts_As_A_Crash()
        {
            await InstallModule("EpsonTM", "1.0");
            var agent = Connect(session: 1);

            registry.Remove(agent.ConnectionId);
            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.IsFalse(store.Load().Modules.ContainsKey("EpsonTM"), "a new module without former version is removed");
            Assert.AreEqual("1.0", store.Load().FailedModules["EpsonTM"].Version);
            Assert.IsFalse(Directory.Exists(Path.Combine(modulesRoot, "EpsonTM")));
        }

        [TestMethod]
        public async Task A_Newer_Application_Waits_For_A_Fitting_Version_Of_Every_Assigned_Module()
        {
            await InstallModule("EpsonTM", "1.0");
            coordinator.ReportModules(new[] { new ModuleLoadReport { ModuleId = "EpsonTM", Loaded = true } });
            feed.Offer(Base("2.0.0", urgent: true), Module("EpsonTM", "1.0", "[1.4.0,2.0.0)"));
            coordinator.CheckNow();

            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.AreEqual(0, installer.Started, "not even urgent overrules a missing module");
            StringAssert.Contains(coordinator.Status.BlockedBy, "EpsonTM");
        }

        [TestMethod]
        public async Task The_Application_And_Its_Modules_Come_In_One_Pass()
        {
            await InstallModule("EpsonTM", "1.0");
            coordinator.ReportModules(new[] { new ModuleLoadReport { ModuleId = "EpsonTM", Loaded = true } });
            feed.Offer(Base("2.0.0"), Module("EpsonTM", "1.0", "[1.4.0,2.0.0)"), Module("EpsonTM", "2.0", "[2.0.0,3.0.0)"));
            installer.BeforeExit = () =>
            {
                Assert.IsTrue(Directory.Exists(Path.Combine(modulesRoot, "EpsonTM.new")), "the module is prepared before the MSI runs");
                Assert.AreEqual("1.0", store.Load().Modules["EpsonTM"].Version, "and switched only after");
            };
            coordinator.CheckNow();

            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.AreEqual(1, installer.Started);
            Assert.AreEqual("2.0", store.Load().Modules["EpsonTM"].Version);
            Assert.IsFalse(Directory.Exists(Path.Combine(modulesRoot, "EpsonTM.new")));
        }

        [TestMethod]
        public async Task When_The_Application_Fails_To_Update_The_Prepared_Modules_Are_Thrown_Away()
        {
            await InstallModule("EpsonTM", "1.0");
            coordinator.ReportModules(new[] { new ModuleLoadReport { ModuleId = "EpsonTM", Loaded = true } });
            feed.Offer(Base("2.0.0"), Module("EpsonTM", "2.0", "[2.0.0,3.0.0)"));
            installer.ExitCode = 1603;
            installer.Installed = () => { };
            coordinator.CheckNow();

            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.AreEqual("1.0", store.Load().Modules["EpsonTM"].Version);
            Assert.IsFalse(Directory.Exists(Path.Combine(modulesRoot, "EpsonTM.new")));
            Assert.IsTrue(File.Exists(Path.Combine(modulesRoot, "EpsonTM", "EpsonTM.dll")));
        }

        [TestMethod]
        public async Task A_Module_That_Brings_A_Contract_Of_The_Application_Is_Refused()
        {
            var contract = Path.GetFileName(typeof(IPlugin).Assembly.Location);
            File.Copy(typeof(IPlugin).Assembly.Location, Path.Combine(host, contract));
            downloader.Extra[contract] = File.ReadAllBytes(typeof(IPlugin).Assembly.Location);
            feed.Offer(Module("EpsonTM", "1.0", null));
            coordinator.AssignModules(new[] { "EpsonTM" });

            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.IsFalse(Directory.Exists(Path.Combine(modulesRoot, "EpsonTM")));
            StringAssert.Contains(store.Load().FailedModules["EpsonTM"].Reason, contract);
        }

        [TestMethod]
        public async Task A_Module_No_Longer_Assigned_Is_Removed()
        {
            await InstallModule("EpsonTM", "1.0");
            coordinator.ReportModules(new[] { new ModuleLoadReport { ModuleId = "EpsonTM", Loaded = true } });

            coordinator.AssignModules(Array.Empty<string>());
            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.IsFalse(store.Load().Modules.ContainsKey("EpsonTM"));
            Assert.IsFalse(Directory.Exists(Path.Combine(modulesRoot, "EpsonTM")));
        }

        private async Task InstallModule(string id, string version)
        {
            feed.Offer(Module(id, version, "[1.4.0,2.0.0)"));
            coordinator.AssignModules(new[] { id });
            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(version, store.Load().Modules[id].Version);
        }

        private RegisteredAgent Connect(int session)
        {
            var identity = new ClaimsIdentity(PipeClaimTypes.AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.Name, @"SHOP\kasse"));
            identity.AddClaim(new Claim(PipeClaimTypes.ConnectionId, Guid.NewGuid().ToString()));
            identity.AddClaim(new Claim(PipeClaimTypes.SessionId, session.ToString()));
            identity.AddClaim(new Claim(PipeClaimTypes.ProcessId, (1000 + session).ToString()));
            return registry.Register(identity, "Shop.Agent", "1.4.1", false, out _, out _)!;
        }

        private static UpdatePackage Base(string version, bool urgent = false) => new UpdatePackage
        {
            Id = "Shop.Agent", Kind = PackageKinds.Base, Version = version, Url = $"Shop.Agent-{version}.msi", Sha256 = "00", Urgent = urgent
        };

        private static UpdatePackage Module(string id, string version, string? requires) => new UpdatePackage
        {
            Id = id, Kind = PackageKinds.Module, Version = version, Url = $"{id}-{version}.zip", Sha256 = "00",
            Requires = requires == null ? null : new Dictionary<string, string> { { "Shop.Agent", requires } }
        };

        private sealed class Feed : IUpdateFeedClient
        {
            private FeedResult? result;

            public void Offer(params UpdatePackage[] packages)
            {
                var feed = new UpdateFeed();
                feed.Packages.AddRange(packages);
                result = new FeedResult(feed, new Uri("https://shop.example/updates/stable.json"));
            }

            public Task<FeedResult?> GetFeedAsync(CancellationToken cancellationToken) => Task.FromResult(result);
        }

        /// <summary>
        /// Hands out an MSI stand-in for the base package and a zip with a module for every module.
        /// </summary>
        private sealed class Downloader : IPackageDownloader
        {
            private readonly string directory;

            public Downloader(string directory)
            {
                this.directory = directory;
            }

            public Dictionary<string, byte[]> Extra { get; } = new Dictionary<string, byte[]>();

            public Task<string> DownloadAsync(UpdatePackage package, Uri source, CancellationToken cancellationToken)
            {
                var path = Path.Combine(directory, $"{package.Id}-{package.Version}{(package.Kind == PackageKinds.Module ? ".zip" : ".msi")}");
                if (package.Kind != PackageKinds.Module)
                {
                    File.WriteAllText(path, "msi");
                    return Task.FromResult(path);
                }

                File.Delete(path);
                using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
                Add(archive, $"{package.Id}.dll", new byte[] { 1, 2, 3 });
                Add(archive, $"{package.Id}.deps.json", System.Text.Encoding.UTF8.GetBytes("{}"));
                foreach (var extra in Extra)
                {
                    Add(archive, extra.Key, extra.Value);
                }

                return Task.FromResult(path);
            }

            private static void Add(ZipArchive archive, string name, byte[] content)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(content);
            }
        }

        private sealed class Verifier : IPackageVerifier
        {
            public string? Verify(string path, UpdatePackage package) => null;
        }

        private sealed class Installer : IPackageInstaller
        {
            public int Started { get; private set; }

            public int ExitCode { get; set; }

            public Action Installed { get; set; } = () => { };

            public Action BeforeExit { get; set; } = () => { };

            public Process Start(string packagePath, UpdatePackage package, string logPath)
            {
                Started++;
                BeforeExit();
                Installed();
                return Process.Start(new ProcessStartInfo("cmd.exe", $"/c exit {ExitCode}") { UseShellExecute = false, CreateNoWindow = true })!;
            }
        }

        private sealed class Application : IApplicationControl
        {
            public Version Installed { get; set; } = new Version(1, 0);

            public List<int> Launched { get; } = new List<int>();

            public Version InstalledVersion() => Installed;

            public void Launch(int sessionId) => Launched.Add(sessionId);

            public void Terminate(RegisteredAgent agent)
            {
            }
        }
    }
}
