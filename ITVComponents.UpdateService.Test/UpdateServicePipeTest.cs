using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared;
using ITVComponents.InterProcessCommunication.NamedPipe.Hub;
using ITVComponents.InterProcessCommunication.NamedPipe.Server;
using ITVComponents.Json;
using ITVComponents.UpdateService.Abstractions.Contracts;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.UpdateService.Agents;
using ITVComponents.UpdateService.Client;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Installation;
using ITVComponents.UpdateService.Packages;
using ITVComponents.UpdateService.Sessions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.UpdateService.Test
{
    /// <summary>
    /// Application and service over a real pipe: register, be asked to end, defer.
    /// </summary>
    [TestClass]
    public class UpdateServicePipeTest
    {
        private MessageServiceHub hub = null!;
        private NamedPipeHubEndPoint endPoint = null!;
        private NamedPipeServer server = null!;
        private UpdateCoordinator coordinator = null!;
        private AgentRegistry registry = null!;
        private UpdateServiceClient client = null!;
        private Feed feed = null!;
        private string pipeName = null!;

        [TestInitialize]
        public void Setup()
        {
            UpdateContractTypes.RegisterAll((t, alias) => JsonHelper.RegisterManualType(t, alias));
            var directory = Directory.CreateTempSubdirectory("itv-update-pipe").FullName;
            using var self = Process.GetCurrentProcess();
            var options = new UpdateServiceOptions
            {
                ProductId = "Shop.Agent",
                FeedUrl = "https://shop.example/updates",
                DataDirectory = directory,
                ApplicationPath = self.MainModule!.FileName, // the test host is "the application": its folder is the allowed one
                RequireApplicationPath = true
            };
            pipeName = $"itv-update-test-{Guid.NewGuid():N}";
            hub = new MessageServiceHub();
            endPoint = new NamedPipeHubEndPoint(pipeName, hub);
            endPoint.Initialize();
            registry = new AgentRegistry(options);
            endPoint.ClientDisconnected += (_, e) => registry.Remove(e.Client.ConnectionId);
            feed = new Feed();
            coordinator = new UpdateCoordinator(options, feed, new Downloader(directory), new Verifier(), new Installer(), new MsiInstallResultEvaluator(),
                new InstallStateStore(directory), registry, new Application(), directory);
            var facade = new UpdateServiceFacade(coordinator, registry, new RoleCoordinator(registry, new OneSession(), new Application(), () => false));
            server = new NamedPipeServer(hub, new Dictionary<string, object> { { UpdateContractTypes.FacadeObjectName, facade } }, options.HubServiceName);
            server.Initialize();
            client = new UpdateServiceClient(new UpdateServiceClientOptions
            {
                PipeName = pipeName,
                ProductId = "Shop.Agent",
                Version = "1.4.1",
                ExpectedServiceAccount = null // the test hub runs as the test user, not as LocalSystem
            });
        }

        [TestCleanup]
        public void Cleanup()
        {
            client.Dispose();
            coordinator.Dispose();
            server.Dispose();
            endPoint.Dispose();
            hub.Dispose();
        }

        [TestMethod]
        public void The_Application_Registers_And_The_Service_Knows_Its_Session_And_Process()
        {
            client.Start();

            Assert.IsTrue(client.Connected);
            using var self = Process.GetCurrentProcess();
            Assert.AreEqual(self.SessionId, client.Registration!.SessionId);
            Assert.AreEqual(self.Id, client.Registration.ProcessId);
            Assert.AreEqual(1, registry.Active.Count);
        }

        [TestMethod]
        public async Task The_Application_Is_Asked_To_End_And_May_Defer()
        {
            using var asked = new ManualResetEventSlim();
            ShutdownRequest? request = null;
            client.ShutdownRequested += r =>
            {
                request = r;
                asked.Set();
            };
            client.Start();
            feed.Offer("1.4.2");

            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.IsTrue(asked.Wait(TimeSpan.FromSeconds(10)), "the request to end must reach the application");
            Assert.AreEqual(ShutdownReason.Update, request!.Reason);
            Assert.AreEqual("1.4.2", request.Version);
            Assert.IsTrue(await client.DeferAsync(TimeSpan.FromMinutes(20), "payment running"));
            Assert.AreEqual(UpdatePhase.Deferred, (await client.GetStatusAsync())!.Phase);
        }

        [TestMethod]
        public void A_Single_Instance_Is_Active_And_A_Second_One_In_The_Same_Session_Is_Refused()
        {
            client.Start();
            Assert.AreEqual(AgentRole.Active, client.Role);

            using var second = new UpdateServiceClient(new UpdateServiceClientOptions
            {
                PipeName = pipeName, ProductId = "Shop.Agent", Version = "1.4.1", ExpectedServiceAccount = null
            });
            second.Start();

            Assert.IsFalse(second.Connected);
            Assert.IsTrue(second.AlreadyRunning, "the same session runs the application already");
            Assert.AreEqual(1, registry.Active.Count);
        }

        [TestMethod]
        public void A_Process_Outside_The_Application_Folder_Cannot_Register()
        {
            client.Dispose();
            var directory = Directory.CreateTempSubdirectory("itv-update-pipe").FullName;
            var strict = new AgentRegistry(new UpdateServiceOptions { ProductId = "Shop.Agent", ApplicationPath = @"C:\Program Files\Shop\Agent.exe" });
            var facade = new UpdateServiceFacade(new UpdateCoordinator(new UpdateServiceOptions { ProductId = "Shop.Agent", FeedUrl = "https://x", DataDirectory = directory, ApplicationPath = @"C:\Program Files\Shop\Agent.exe" },
                feed, new Downloader(directory), new Verifier(), new Installer(), new MsiInstallResultEvaluator(), new InstallStateStore(directory), strict, new Application(), directory), strict, new RoleCoordinator(strict, new OneSession(), new Application(), () => false));
            using var other = new NamedPipeServer(hub, new Dictionary<string, object> { { UpdateContractTypes.FacadeObjectName, facade } }, "StrictUpdateService");
            other.Initialize();
            using var outsider = new UpdateServiceClient(new UpdateServiceClientOptions
            {
                PipeName = pipeName, HubServiceName = "StrictUpdateService", ProductId = "Shop.Agent", Version = "1.0", ExpectedServiceAccount = null
            });

            outsider.Start();

            Assert.IsFalse(outsider.Connected);
            Assert.AreEqual(0, strict.Active.Count);
            StringAssert.Contains(outsider.RefusalReason, "does not run from the application's folder");
        }

        private sealed class OneSession : ISessionInfo
        {
            public int ConsoleSessionId() => Process.GetCurrentProcess().SessionId;

            public bool IsConnected(int sessionId) => true;
        }

        private sealed class Feed : IUpdateFeedClient
        {
            private FeedResult? result;

            public void Offer(string version)
            {
                var feed = new UpdateFeed();
                feed.Packages.Add(new UpdatePackage { Id = "Shop.Agent", Kind = PackageKinds.Base, Version = version, Url = "a.msi", Sha256 = "00", Published = DateTimeOffset.UtcNow });
                result = new FeedResult(feed, new Uri("https://shop.example/updates/stable.json"));
            }

            public Task<FeedResult?> GetFeedAsync(CancellationToken cancellationToken) => Task.FromResult(result);
        }

        private sealed class Downloader : IPackageDownloader
        {
            private readonly string directory;

            public Downloader(string directory)
            {
                this.directory = directory;
            }

            public Task<string> DownloadAsync(UpdatePackage package, Uri source, CancellationToken cancellationToken)
            {
                var path = Path.Combine(directory, "a.msi");
                File.WriteAllText(path, "package");
                return Task.FromResult(path);
            }
        }

        private sealed class Verifier : IPackageVerifier
        {
            public string? Verify(string path, UpdatePackage package) => null;
        }

        private sealed class Installer : IPackageInstaller
        {
            public Process Start(string packagePath, UpdatePackage package, string logPath) => throw new AssertFailedException("nothing is installed while the application runs");
        }

        private sealed class Application : IApplicationControl
        {
            public Version InstalledVersion() => new Version(1, 4, 1);

            public void Launch(int sessionId)
            {
            }

            public void Terminate(RegisteredAgent agent)
            {
            }
        }
    }
}
