using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.NamedPipe.Security;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.UpdateService.Agents;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Installation;
using ITVComponents.UpdateService.Packages;
using ITVComponents.UpdateService.Sessions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.UpdateService.Test
{
    /// <summary>
    /// Several applications at one update service (issue ModulePlugins-Instances, D): the configuration of the targets, the
    /// choice of the target at the registration, and what the targets share.
    /// </summary>
    [TestClass]
    public class UpdateTargetsTest
    {
        private const string PosFolder = @"C:\Program Files\Shop\Pos";
        private const string TaskFolder = @"C:\Program Files\Shop\Tasks";

        private readonly List<string> directories = new List<string>();

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var directory in directories)
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Test folder {directory} could not be removed: {ex}");
                }
            }
        }

        [TestMethod]
        public void Without_Targets_The_Single_Values_Form_The_Target_Default()
        {
            var options = new UpdateServiceOptions { ProductId = "Shop.Agent", ApplicationPath = PosFolder + @"\Agent.exe", UpgradeCode = Guid.NewGuid().ToString() };

            var targets = options.ResolveTargets();

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(UpdateServiceOptions.DefaultTargetId, targets[0].TargetId);
            Assert.AreEqual("Shop.Agent", targets[0].ProductId);
        }

        [TestMethod]
        public void Without_Targets_ProductId_And_ApplicationPath_Are_Needed()
        {
            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => new UpdateServiceOptions { FeedUrl = "https://x" }.ResolveTargets());
            StringAssert.Contains(ex.Message, "ProductId");
        }

        [TestMethod]
        public void A_Target_Takes_Its_Own_Values_And_Shares_The_Rest()
        {
            var options = Shared();
            options.RequireApplicationPath = false;
            options.Targets.Add(Target("Pos", "Shop.Pos", PosFolder));
            options.Targets.Add(Target("Tasks", "Shop.Tasks", TaskFolder, requireApplicationPath: true));

            var targets = options.ResolveTargets();

            Assert.AreEqual(2, targets.Count);
            Assert.AreEqual("Pos", targets[0].TargetId);
            Assert.AreEqual("Shop.Pos", targets[0].ProductId);
            Assert.AreEqual(PosFolder + @"\App.exe", targets[0].ApplicationPath);
            Assert.IsFalse(targets[0].RequireApplicationPath, "without a value of its own, the target takes the shared one");
            Assert.IsTrue(targets[1].RequireApplicationPath);
            Assert.AreEqual("https://shop.example/updates", targets[1].FeedUrl, "the feed is shared");
            Assert.AreEqual(0, targets[1].Targets.Count, "a resolved target carries no targets of its own");
            Assert.AreEqual(2, options.Targets.Count, "the configuration itself is not changed");
        }

        [TestMethod]
        public void A_Windows_Service_Target_Needs_Its_Service_Name()
        {
            var options = Shared();
            var target = Target("Tasks", "Shop.Tasks", TaskFolder);
            target.Kind = "windowsservice";
            options.Targets.Add(target);

            StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => options.ResolveTargets()).Message, "ServiceName");

            target.ServiceName = " ShopTasks ";
            var resolved = options.ResolveTargets().Single();
            Assert.IsTrue(resolved.IsWindowsService);
            Assert.AreEqual(UpdateTargetKinds.WindowsService, resolved.TargetKind);
            Assert.AreEqual("ShopTasks", resolved.ServiceName);
        }

        [TestMethod]
        public void Two_Targets_Never_Name_The_Same_Service()
        {
            var options = Shared();
            options.Targets.Add(Service("Tasks", "Shop.Tasks", TaskFolder, "ShopTasks"));
            options.Targets.Add(Service("Tasks2", "Shop.Tasks2", PosFolder, "shoptasks"));

            StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => options.ResolveTargets()).Message, "same service");
        }

        [TestMethod]
        public void An_Unknown_Kind_Is_Refused()
        {
            var options = Shared();
            var target = Target("Tasks", "Shop.Tasks", TaskFolder);
            target.Kind = "Daemon";
            options.Targets.Add(target);

            StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => options.ResolveTargets()).Message, "unknown");
        }

        [TestMethod]
        public void A_Service_Registers_Only_From_Its_Own_Process()
        {
            var services = new FakeServices { Info = new WindowsServiceInfo { State = WindowsServiceRunState.Running, ProcessId = 4711, OwnProcess = true } };
            var registry = ServiceRegistry(services);

            Assert.IsNull(registry.Register(Identity(0, TaskFolder + @"\App.exe", 4712), "Shop.Tasks", "1.0", false, out var other, out _));
            StringAssert.Contains(other, "is not the service");

            Assert.IsNotNull(registry.Register(Identity(0, TaskFolder + @"\App.exe", 4711), "Shop.Tasks", "1.0", false, out var reason, out _), reason);
        }

        [TestMethod]
        public void A_Service_That_Does_Not_Run_Or_Shares_Its_Process_Is_Refused()
        {
            var services = new FakeServices();
            var registry = ServiceRegistry(services);

            Assert.IsNull(registry.Register(Identity(0, TaskFolder + @"\App.exe", 4711), "Shop.Tasks", "1.0", false, out var missing, out _));
            StringAssert.Contains(missing, "not installed");

            services.Info = new WindowsServiceInfo { State = WindowsServiceRunState.Stopped, ProcessId = 0, OwnProcess = true };
            Assert.IsNull(registry.Register(Identity(0, TaskFolder + @"\App.exe", 4711), "Shop.Tasks", "1.0", false, out var stopped, out _));
            StringAssert.Contains(stopped, "does not run");

            services.Info = new WindowsServiceInfo { State = WindowsServiceRunState.Running, ProcessId = 4711, OwnProcess = false };
            Assert.IsNull(registry.Register(Identity(0, TaskFolder + @"\App.exe", 4711), "Shop.Tasks", "1.0", false, out var shared, out _));
            StringAssert.Contains(shared, "shares its process");

            services.Throws = true;
            Assert.IsNull(registry.Register(Identity(0, TaskFolder + @"\App.exe", 4711), "Shop.Tasks", "1.0", false, out var broken, out _));
            StringAssert.Contains(broken, "could not be checked");
        }

        [TestMethod]
        public void A_Service_Target_Without_The_Service_Control_Manager_Admits_Nobody()
        {
            var registry = new AgentRegistry(ResolvedService());

            Assert.IsNull(registry.Register(Identity(0, TaskFolder + @"\App.exe", 4711), "Shop.Tasks", "1.0", false, out var reason, out _));
            StringAssert.Contains(reason, "cannot check");
        }

        [TestMethod]
        public async Task A_Service_Is_Always_Active_And_Has_No_Roles()
        {
            var services = new FakeServices { Info = new WindowsServiceInfo { State = WindowsServiceRunState.Running, ProcessId = 4711, OwnProcess = true } };
            var world = Build(services, Target("Pos", "Shop.Pos", PosFolder), Service("Tasks", "Shop.Tasks", TaskFolder, "ShopTasks"));
            var caller = Identity(0, TaskFolder + @"\App.exe", 4711);

            var registration = await world.Facade.RegisterAsync("Shop.Tasks", "1.0", false, caller);
            await world.Facade.ReportReleasedAsync(caller);

            Assert.IsTrue(registration.Accepted, registration.Reason);
            Assert.AreEqual("Tasks", registration.TargetId);
            Assert.AreEqual(AgentRole.Active, registration.Role);
            Assert.IsNull(world.Targets[1].Roles);
            Assert.IsNotNull(world.Targets[0].Roles, "an application in a session keeps its roles");
        }

        [TestMethod]
        public void A_Service_Is_Started_Unless_It_Runs_Already()
        {
            var services = new FakeServices { Info = new WindowsServiceInfo { State = WindowsServiceRunState.Running, ProcessId = 4711, OwnProcess = true } };
            var control = new ServiceApplicationControl(ResolvedService(), services, new FakeApplication());

            control.Launch(0);
            Assert.AreEqual(0, services.Started, "the installer started it already");

            services.Info = new WindowsServiceInfo { State = WindowsServiceRunState.Stopped, OwnProcess = true };
            control.Launch(3);
            Assert.AreEqual(1, services.Started, "the session does not matter for a service");
        }

        [TestMethod]
        public void A_Service_That_Does_Not_End_In_Time_Is_Stopped_And_Then_Ended()
        {
            var services = new FakeServices { Info = new WindowsServiceInfo { State = WindowsServiceRunState.Running, ProcessId = 4711, OwnProcess = true } };
            var killed = new List<int>();
            var agent = new RegisteredAgent { ProcessId = 4711, TargetId = "Tasks" };

            services.StopsOnRequest = true;
            new ServiceApplicationControl(ResolvedService(), services, new FakeApplication(), TimeSpan.FromSeconds(2), killed.Add).Terminate(agent);
            Assert.AreEqual(1, services.Stopped);
            Assert.AreEqual(0, killed.Count, "it stopped within the time");

            services.Info = new WindowsServiceInfo { State = WindowsServiceRunState.Running, ProcessId = 4711, OwnProcess = true };
            services.StopsOnRequest = false;
            new ServiceApplicationControl(ResolvedService(), services, new FakeApplication(), TimeSpan.FromMilliseconds(600), killed.Add).Terminate(agent);
            CollectionAssert.AreEqual(new[] { 4711 }, killed, "a service that hangs in stopping is ended");
        }

        [TestMethod]
        public void Targets_That_Share_An_Msi_Are_Refused()
        {
            var options = Shared();
            var pos = Target("Pos", "Shop.Pos", PosFolder);
            var tasks = Target("Tasks", "Shop.Tasks", TaskFolder);
            tasks.UpgradeCode = pos.UpgradeCode.ToUpperInvariant();
            options.Targets.Add(pos);
            options.Targets.Add(tasks);

            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => options.ResolveTargets());
            StringAssert.Contains(ex.Message, "UpgradeCode");
        }

        [TestMethod]
        public void Targets_In_The_Same_Folder_Are_Refused()
        {
            var options = Shared();
            options.Targets.Add(Target("Pos", "Shop.Pos", PosFolder));
            options.Targets.Add(Target("Pos2", "Shop.Pos2", PosFolder + @"\"));

            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => options.ResolveTargets());
            StringAssert.Contains(ex.Message, "same folder");
        }

        [TestMethod]
        public void A_Target_Id_Is_Unique_And_Usable_As_Folder()
        {
            var twice = Shared();
            twice.Targets.Add(Target("Pos", "Shop.Pos", PosFolder));
            twice.Targets.Add(Target("POS", "Shop.Tasks", TaskFolder));
            StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => twice.ResolveTargets()).Message, "used twice");

            var bad = Shared();
            bad.Targets.Add(Target(@"..\Pos", "Shop.Pos", PosFolder));
            StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => bad.ResolveTargets()).Message, "not valid");
        }

        [TestMethod]
        public async Task An_Application_Is_Registered_With_The_Target_Of_Its_Folder()
        {
            // The same product twice (e.g. a test installation next to the real one): the folder decides.
            var world = Build(Target("Pos", "Shop.Pos", PosFolder), Target("PosTest", "Shop.Pos", TaskFolder));

            var registration = await world.Facade.RegisterAsync("Shop.Pos", "1.0", false, Identity(1, TaskFolder + @"\App.exe"));

            Assert.IsTrue(registration.Accepted, registration.Reason);
            Assert.AreEqual("PosTest", registration.TargetId);
            Assert.AreEqual(0, world.Targets[0].Registry.Active.Count);
            Assert.AreEqual(1, world.Targets[1].Registry.Active.Count);
            Assert.AreEqual("PosTest", registration.Status!.TargetId);
        }

        [TestMethod]
        public async Task A_Product_No_Target_Keeps_Is_Refused()
        {
            var world = Build(Target("Pos", "Shop.Pos", PosFolder), Target("Tasks", "Shop.Tasks", TaskFolder));

            var registration = await world.Facade.RegisterAsync("Shop.Other", "1.0", false, Identity(1, PosFolder + @"\App.exe"));

            Assert.IsFalse(registration.Accepted);
            StringAssert.Contains(registration.Reason, "Shop.Pos, Shop.Tasks");
        }

        [TestMethod]
        public async Task Two_Applications_Of_Different_Targets_Run_In_The_Same_Session()
        {
            var world = Build(Target("Pos", "Shop.Pos", PosFolder), Target("Tasks", "Shop.Tasks", TaskFolder));

            var pos = await world.Facade.RegisterAsync("Shop.Pos", "1.0", false, Identity(1, PosFolder + @"\App.exe"));
            var tasks = await world.Facade.RegisterAsync("Shop.Tasks", "1.0", false, Identity(1, TaskFolder + @"\App.exe"));

            Assert.IsTrue(pos.Accepted, pos.Reason);
            Assert.IsTrue(tasks.Accepted, tasks.Reason);
            Assert.IsFalse(tasks.AlreadyRunning, "another application in the session is not the same application");
        }

        [TestMethod]
        public async Task A_Request_To_End_Names_Its_Target_And_A_Deferral_Stays_In_It()
        {
            var world = Build(Target("Pos", "Shop.Pos", PosFolder), Target("Tasks", "Shop.Tasks", TaskFolder));
            var requests = new List<ShutdownRequest>();
            world.Facade.ShutdownRequested += r => requests.Add(r);
            var posCaller = Identity(1, PosFolder + @"\App.exe");
            await world.Facade.RegisterAsync("Shop.Pos", "1.0", false, posCaller);
            await world.Facade.RegisterAsync("Shop.Tasks", "1.0", false, Identity(1, TaskFolder + @"\App.exe"));
            world.Feeds[0].Offer("Shop.Pos", "2.0");

            await world.Targets[0].Coordinator.RunOnceAsync(CancellationToken.None);
            var deferred = await world.Facade.DeferAsync(30, "customer at the till", posCaller);

            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual("Pos", requests[0].TargetId);
            Assert.IsTrue(deferred);
            Assert.IsNotNull(world.Stores[0].Load().DeferredUntil);
            Assert.IsNull(world.Stores[1].Load().DeferredUntil, "the other target has its own state");
        }

        [TestMethod]
        public async Task Only_One_Target_Installs_At_A_Time()
        {
            var world = Build(Target("Pos", "Shop.Pos", PosFolder), Target("Tasks", "Shop.Tasks", TaskFolder));
            world.Feeds[1].Offer("Shop.Tasks", "2.0");
            world.Gate.Wait();

            var pass = world.Targets[1].Coordinator.RunOnceAsync(CancellationToken.None);
            await Task.Delay(300);

            Assert.IsFalse(pass.IsCompleted, "the installation waits while the other target installs");
            Assert.AreEqual(0, world.Installers[1].Started);

            world.Gate.Release();
            await pass;

            Assert.AreEqual(1, world.Installers[1].Started);
            Assert.AreEqual(1, world.Gate.CurrentCount, "the gate is free again after the installation");
        }

        private static UpdateServiceOptions Shared() => new UpdateServiceOptions
        {
            FeedUrl = "https://shop.example/updates",
            DataDirectory = @"C:\ProgramData\Shop\Updates"
        };

        private static UpdateTargetOptions Target(string id, string productId, string folder, bool? requireApplicationPath = null) => new UpdateTargetOptions
        {
            Id = id,
            ProductId = productId,
            ApplicationPath = folder.TrimEnd('\\') + @"\App.exe",
            UpgradeCode = Guid.NewGuid().ToString(),
            RequireApplicationPath = requireApplicationPath
        };

        private static UpdateTargetOptions Service(string id, string productId, string folder, string serviceName)
        {
            var target = Target(id, productId, folder);
            target.Kind = UpdateTargetKinds.WindowsService;
            target.ServiceName = serviceName;
            return target;
        }

        private static UpdateServiceOptions ResolvedService()
        {
            var options = Shared();
            options.Targets.Add(Service("Tasks", "Shop.Tasks", TaskFolder, "ShopTasks"));
            return options.ResolveTargets().Single();
        }

        private static AgentRegistry ServiceRegistry(IWindowsServices services) => new AgentRegistry(ResolvedService(), services);

        private World Build(params UpdateTargetOptions[] targetOptions) => Build(new FakeServices(), targetOptions);

        private World Build(IWindowsServices services, params UpdateTargetOptions[] targetOptions)
        {
            var directory = Directory.CreateTempSubdirectory("itv-update-targets").FullName;
            directories.Add(directory);
            var options = Shared();
            options.DataDirectory = directory;
            options.Targets.AddRange(targetOptions);
            var world = new World();
            foreach (var resolved in options.ResolveTargets())
            {
                var targetDirectory = Directory.CreateDirectory(Path.Combine(directory, "targets", resolved.TargetId)).FullName;
                var feed = new FakeFeed();
                var installer = new FakeInstaller();
                var store = new InstallStateStore(targetDirectory);
                var registry = new AgentRegistry(resolved, services);
                var application = new FakeApplication();
                var coordinator = new UpdateCoordinator(resolved, feed, new FakeDownloader(targetDirectory), new FakeVerifier(), installer,
                    new MsiInstallResultEvaluator(), store, registry, application, targetDirectory, null, null, world.Gate);
                var roles = resolved.IsWindowsService ? null : new RoleCoordinator(registry, new OneSession(), application, () => false);
                world.Targets.Add(new UpdateTarget(resolved, coordinator, registry, roles, null));
                world.Feeds.Add(feed);
                world.Installers.Add(installer);
                world.Stores.Add(store);
            }

            world.Facade = new UpdateServiceFacade(world.Targets);
            return world;
        }

        private static ClaimsIdentity Identity(int session, string processPath) => Identity(session, processPath, Random.Shared.Next(1000, 60000));

        private static ClaimsIdentity Identity(int session, string processPath, int processId)
        {
            var identity = new ClaimsIdentity(PipeClaimTypes.AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.Name, $@"SHOP\user{session}"));
            identity.AddClaim(new Claim(PipeClaimTypes.ConnectionId, Guid.NewGuid().ToString()));
            identity.AddClaim(new Claim(PipeClaimTypes.SessionId, session.ToString()));
            identity.AddClaim(new Claim(PipeClaimTypes.ProcessId, processId.ToString()));
            identity.AddClaim(new Claim(PipeClaimTypes.ProcessPath, processPath));
            return identity;
        }

        private sealed class World
        {
            public SemaphoreSlim Gate { get; } = new SemaphoreSlim(1, 1);

            public List<UpdateTarget> Targets { get; } = new List<UpdateTarget>();

            public List<FakeFeed> Feeds { get; } = new List<FakeFeed>();

            public List<FakeInstaller> Installers { get; } = new List<FakeInstaller>();

            public List<InstallStateStore> Stores { get; } = new List<InstallStateStore>();

            public UpdateServiceFacade Facade { get; set; } = null!;
        }

        private sealed class FakeFeed : IUpdateFeedClient
        {
            private FeedResult? result;

            public void Offer(string productId, string version)
            {
                var feed = new UpdateFeed();
                feed.Packages.Add(new UpdatePackage { Id = productId, Kind = PackageKinds.Base, Version = version, Url = $"{productId}-{version}.msi", Sha256 = "00" });
                result = new FeedResult(feed, new Uri("https://shop.example/updates/stable.json"));
            }

            public Task<FeedResult?> GetFeedAsync(CancellationToken cancellationToken) => Task.FromResult(result);
        }

        private sealed class FakeDownloader : IPackageDownloader
        {
            private readonly string directory;

            public FakeDownloader(string directory)
            {
                this.directory = directory;
            }

            public Task<string> DownloadAsync(UpdatePackage package, Uri source, CancellationToken cancellationToken)
            {
                var path = Path.Combine(directory, $"{package.Id}-{package.Version}.msi");
                File.WriteAllText(path, "package");
                return Task.FromResult(path);
            }
        }

        private sealed class FakeVerifier : IPackageVerifier
        {
            public string? Verify(string path, UpdatePackage package) => null;
        }

        private sealed class FakeInstaller : IPackageInstaller
        {
            public int Started { get; private set; }

            public Process Start(string packagePath, UpdatePackage package, string logPath)
            {
                Started++;
                return Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0") { UseShellExecute = false, CreateNoWindow = true })!;
            }
        }

        private sealed class FakeApplication : IApplicationControl
        {
            public Version InstalledVersion() => new Version(1, 0);

            public void Launch(int sessionId)
            {
            }

            public void Terminate(RegisteredAgent agent)
            {
            }
        }

        private sealed class FakeServices : IWindowsServices
        {
            public WindowsServiceInfo? Info { get; set; }

            public bool Throws { get; set; }

            public bool StopsOnRequest { get; set; }

            public int Started { get; private set; }

            public int Stopped { get; private set; }

            public WindowsServiceInfo? Query(string serviceName)
            {
                if (Throws)
                {
                    throw new System.ComponentModel.Win32Exception(5, "Access denied (test).");
                }

                return Info;
            }

            public bool Start(string serviceName)
            {
                Started++;
                Info = new WindowsServiceInfo { State = WindowsServiceRunState.Running, ProcessId = 4711, OwnProcess = true };
                return true;
            }

            public bool Stop(string serviceName)
            {
                Stopped++;
                Info = new WindowsServiceInfo
                {
                    State = StopsOnRequest ? WindowsServiceRunState.Stopped : WindowsServiceRunState.StopPending,
                    ProcessId = StopsOnRequest ? 0 : 4711,
                    OwnProcess = true
                };
                return true;
            }
        }

        private sealed class OneSession : ISessionInfo
        {
            public int ConsoleSessionId() => 1;

            public bool IsConnected(int sessionId) => sessionId == 1;
        }
    }
}
