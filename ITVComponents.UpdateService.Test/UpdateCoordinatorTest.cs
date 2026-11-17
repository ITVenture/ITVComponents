using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.UpdateService.Test
{
    /// <summary>
    /// The course of an update with stand-ins for feed, download, check, installer and application.
    /// </summary>
    [TestClass]
    public class UpdateCoordinatorTest
    {
        private ManualClock clock = null!;
        private FakeFeed feed = null!;
        private FakeVerifier verifier = null!;
        private FakeInstaller installer = null!;
        private FakeEvaluator evaluator = null!;
        private FakeApplication application = null!;
        private UpdateServiceOptions options = null!;
        private AgentRegistry registry = null!;
        private InstallStateStore store = null!;
        private UpdateCoordinator coordinator = null!;
        private readonly List<ShutdownRequest> requests = new List<ShutdownRequest>();

        [TestInitialize]
        public void Setup()
        {
            var directory = Directory.CreateTempSubdirectory("itv-update-test").FullName;
            clock = new ManualClock(new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.Zero));
            options = new UpdateServiceOptions
            {
                ProductId = "Shop.Agent",
                FeedUrl = "https://shop.example/updates",
                DataDirectory = directory,
                ApplicationPath = @"C:\Program Files\Shop\Agent.exe",
                RequireApplicationPath = false
            };
            feed = new FakeFeed();
            verifier = new FakeVerifier();
            installer = new FakeInstaller();
            evaluator = new FakeEvaluator();
            application = new FakeApplication { Installed = new Version(1, 4, 1) };
            registry = new AgentRegistry(options);
            store = new InstallStateStore(directory);
            coordinator = new UpdateCoordinator(options, feed, new FakeDownloader(directory), verifier, installer, evaluator, store, registry, application, directory, clock);
            coordinator.ShutdownRequested += r => requests.Add(r);
            installer.Installed = () => application.Installed = new Version(1, 4, 2);
        }

        [TestCleanup]
        public void Cleanup()
        {
            coordinator.Dispose();
        }

        [TestMethod]
        public async Task Without_An_Application_A_New_Version_Is_Installed_Right_Away()
        {
            feed.Offer("1.4.2");

            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.AreEqual(1, installer.Started);
            Assert.AreEqual(UpdatePhase.UpToDate, coordinator.Status.Phase);
            Assert.AreEqual("1.4.2", coordinator.Status.InstalledVersion);
            Assert.IsNull(store.Load().Pending);
            Assert.AreEqual(0, application.Launched.Count, "the application did not run, so it is not started");
        }

        [TestMethod]
        public async Task The_Same_Version_Or_An_Older_One_Is_Not_Installed()
        {
            feed.Offer("1.4.1");
            await coordinator.RunOnceAsync(CancellationToken.None);
            feed.Offer("1.3.0");
            coordinator.CheckNow();
            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.AreEqual(0, installer.Started);
            Assert.AreEqual(UpdatePhase.UpToDate, coordinator.Status.Phase);
        }

        [TestMethod]
        public async Task A_Running_Application_Is_Asked_To_End_When_Idle_And_Started_Again_After_The_Installation()
        {
            feed.Offer("1.4.2");
            var agent = Connect(session: 3);

            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(0, installer.Started, "nothing is installed while the application runs");
            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual(ShutdownReason.Update, requests[0].Reason);
            Assert.IsNull(requests[0].Deadline, "a normal update waits until the application is idle");
            Assert.AreEqual(UpdatePhase.ReadyToInstall, coordinator.Status.Phase);

            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(1, requests.Count, "the request is not repeated");

            registry.Remove(agent.ConnectionId);
            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.AreEqual(1, installer.Started);
            CollectionAssert.AreEqual(new[] { 3 }, application.Launched, "the application runs again in its session");
        }

        [TestMethod]
        public async Task A_Deferral_Holds_Until_It_Ends_Then_The_Application_Is_Asked_Again()
        {
            feed.Offer("1.4.2", published: clock.GetUtcNow());
            var agent = Connect(session: 1);
            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.IsTrue(coordinator.Defer(30, "payment running", agent));
            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(UpdatePhase.Deferred, coordinator.Status.Phase);
            Assert.AreEqual(1, requests.Count);

            clock.Advance(TimeSpan.FromMinutes(31));
            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(2, requests.Count, "after the deferral the application is asked again");
            Assert.AreEqual(0, installer.Started);
        }

        [TestMethod]
        public async Task An_Urgent_Update_Is_Not_Deferred_And_The_Application_Is_Ended_After_The_Deadline()
        {
            feed.Offer("1.4.2", urgent: true);
            var agent = Connect(session: 1);

            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(ShutdownReason.UrgentUpdate, requests[0].Reason);
            Assert.IsNotNull(requests[0].Deadline);
            Assert.IsFalse(coordinator.Defer(30, "busy", agent));

            clock.Advance(TimeSpan.FromMinutes(options.UrgentDeadlineMinutes + 1));
            await coordinator.RunOnceAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { agent.ProcessId }, application.Terminated);
        }

        [TestMethod]
        public async Task An_Update_Published_Longer_Ago_Than_The_Longest_Deferral_Is_Urgent()
        {
            feed.Offer("1.4.2", published: clock.GetUtcNow().AddDays(-(options.MaximumDeferralDays + 1)));
            Connect(session: 1);

            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.AreEqual(ShutdownReason.UrgentUpdate, requests[0].Reason);
        }

        [TestMethod]
        public async Task A_Version_Below_The_Minimum_Is_Updated_Urgently()
        {
            application.Installed = new Version(1, 2, 0);
            feed.Offer("1.4.2", minimumVersion: "1.3.0");
            Connect(session: 1);

            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.AreEqual(ShutdownReason.UrgentUpdate, requests[0].Reason);
        }

        [TestMethod]
        public async Task A_Refused_Package_Is_Not_Tried_Again_Until_Someone_Asks()
        {
            feed.Offer("1.4.2");
            verifier.Refusal = "signed by someone else";

            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(UpdatePhase.Failed, coordinator.Status.Phase);
            Assert.AreEqual("signed by someone else", coordinator.Status.Error);
            Assert.AreEqual("1.4.2", store.Load().FailedVersion);

            verifier.Refusal = null;
            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(0, installer.Started, "the refused version is not taken up again by itself");

            coordinator.CheckNow();
            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(1, installer.Started);
        }

        [TestMethod]
        public async Task A_Busy_Installer_Is_Tried_Again_Later()
        {
            feed.Offer("1.4.2");
            installer.ExitCode = 1618;
            installer.Installed = () => { };

            var wait = await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.AreEqual(UpdatePhase.ReadyToInstall, coordinator.Status.Phase);
            Assert.IsTrue(wait >= TimeSpan.FromMinutes(options.RetryMinutes - 1));
            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(1, installer.Started, "not before the retry time");

            installer.ExitCode = 0;
            installer.Installed = () => application.Installed = new Version(1, 4, 2);
            clock.Advance(TimeSpan.FromMinutes(options.RetryMinutes + 1));
            await coordinator.RunOnceAsync(CancellationToken.None);
            Assert.AreEqual(2, installer.Started);
            Assert.AreEqual(UpdatePhase.UpToDate, coordinator.Status.Phase);
        }

        [TestMethod]
        public async Task After_Its_Own_Restart_The_Service_Evaluates_The_Installation_And_Starts_The_Application_Again()
        {
            store.Save(new InstallState
            {
                Pending = new PendingInstall { PackageId = "Shop.Agent", Version = "1.4.2", LogPath = "install.log", SessionsToRestart = { 2, 5 } }
            });
            evaluator.Awaited = new InstallResult(InstallOutcome.Succeeded, 0, "installed");
            application.Installed = new Version(1, 4, 2);

            await coordinator.RunOnceAsync(CancellationToken.None);

            Assert.IsNull(store.Load().Pending);
            CollectionAssert.AreEqual(new[] { 2, 5 }, application.Launched);
            Assert.AreEqual(UpdatePhase.UpToDate, coordinator.Status.Phase);
        }

        [TestMethod]
        public async Task A_Failed_Installation_Still_Starts_The_Application_Again()
        {
            store.Save(new InstallState
            {
                Pending = new PendingInstall { PackageId = "Shop.Agent", Version = "1.4.2", LogPath = "install.log", SessionsToRestart = { 1 } }
            });
            evaluator.Awaited = new InstallResult(InstallOutcome.Failed, 1603, "rolled back");

            await coordinator.RunOnceAsync(CancellationToken.None);

            CollectionAssert.AreEqual(new[] { 1 }, application.Launched);
            Assert.AreEqual(UpdatePhase.Failed, coordinator.Status.Phase);
            Assert.AreEqual("1.4.2", store.Load().FailedVersion);
        }

        private RegisteredAgent Connect(int session)
        {
            var identity = new ClaimsIdentity(PipeClaimTypes.AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.Name, @"SHOP\kasse"));
            identity.AddClaim(new Claim(PipeClaimTypes.ConnectionId, Guid.NewGuid().ToString()));
            identity.AddClaim(new Claim(PipeClaimTypes.SessionId, session.ToString()));
            identity.AddClaim(new Claim(PipeClaimTypes.ProcessId, (1000 + session).ToString()));
            return registry.Register(identity, "Shop.Agent", "1.4.1", out _)!;
        }

        private sealed class ManualClock : TimeProvider
        {
            private DateTimeOffset now;

            public ManualClock(DateTimeOffset now)
            {
                this.now = now;
            }

            public override DateTimeOffset GetUtcNow() => now;

            public void Advance(TimeSpan by) => now += by;
        }

        private sealed class FakeFeed : IUpdateFeedClient
        {
            private FeedResult? result;

            public void Offer(string version, bool urgent = false, DateTimeOffset? published = null, string? minimumVersion = null)
            {
                var feed = new UpdateFeed();
                feed.Packages.Add(new UpdatePackage
                {
                    Id = "Shop.Agent", Kind = PackageKinds.Base, Version = version, Url = $"Shop.Agent-{version}.msi", Sha256 = "00",
                    Urgent = urgent, Published = published, MinimumVersion = minimumVersion
                });
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
            public string? Refusal { get; set; }

            public string? Verify(string path, UpdatePackage package) => Refusal;
        }

        private sealed class FakeInstaller : IPackageInstaller
        {
            public int Started { get; private set; }

            public int ExitCode { get; set; }

            public Action Installed { get; set; } = () => { };

            public Process Start(string packagePath, UpdatePackage package, string logPath)
            {
                Started++;
                Installed();
                return Process.Start(new ProcessStartInfo("cmd.exe", $"/c exit {ExitCode}") { UseShellExecute = false, CreateNoWindow = true })!;
            }
        }

        private sealed class FakeEvaluator : IInstallResultEvaluator
        {
            private readonly MsiInstallResultEvaluator real = new MsiInstallResultEvaluator();

            public InstallResult Awaited { get; set; } = new InstallResult(InstallOutcome.Unknown, null, "not set");

            public InstallResult FromExitCode(int exitCode) => real.FromExitCode(exitCode);

            public InstallResult FromLog(string logPath) => Awaited;

            public Task<InstallResult> AwaitAsync(PendingInstall pending, TimeSpan timeout, CancellationToken cancellationToken) => Task.FromResult(Awaited);
        }

        private sealed class FakeApplication : IApplicationControl
        {
            public Version Installed { get; set; } = new Version(1, 0);

            public List<int> Launched { get; } = new List<int>();

            public List<int> Terminated { get; } = new List<int>();

            public Version InstalledVersion() => Installed;

            public void Launch(int sessionId) => Launched.Add(sessionId);

            public void Terminate(RegisteredAgent agent) => Terminated.Add(agent.ProcessId);
        }
    }
}
