using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.UpdateService.Feed;
using ITVComponents.UpdateService.Installation;
using ITVComponents.UpdateService.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.UpdateService.Test
{
    /// <summary>
    /// The feed, the download, the result of an installation and the state - each on its own.
    /// </summary>
    [TestClass]
    public class FeedAndPackageTest
    {
        private const string Manifest = """
            {
              "channel": "stable",
              "packages": [
                { "id": "Shop.Agent", "kind": "base", "version": "1.4.2", "url": "packages/Shop.Agent-1.4.2.msi",
                  "sha256": "ABCDEF", "size": 6, "urgent": true, "minimumVersion": "1.3.0" }
              ]
            }
            """;

        [TestMethod]
        public void The_Manifest_Is_Read_And_Package_Addresses_Are_Resolved_Against_It()
        {
            var feed = UpdateFeedClient.Parse(Manifest);
            var package = feed.Packages[0];
            Assert.AreEqual("1.4.2", package.Version);
            Assert.IsTrue(package.Urgent);

            var result = new FeedResult(feed, new Uri("https://shop.example/updates/stable.json"));
            Assert.AreEqual("https://shop.example/updates/packages/Shop.Agent-1.4.2.msi", result.ResolvePackageUri(package, false).ToString());
        }

        [TestMethod]
        public void A_Package_Without_Hash_Makes_The_Manifest_Unusable()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => UpdateFeedClient.Parse("""{ "packages": [ { "id": "A", "version": "1.0", "url": "a.msi" } ] }"""));
        }

        [TestMethod]
        public void Only_Https_Is_Loaded()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => new UpdateFeedClient(new HttpClient(), new UpdateServiceOptions { FeedUrl = "http://shop.example/updates" }));
            var feed = UpdateFeedClient.Parse(Manifest);
            feed.Packages[0].Url = "http://elsewhere.example/evil.msi";
            var result = new FeedResult(feed, new Uri("https://shop.example/updates/stable.json"));
            Assert.ThrowsExactly<InvalidOperationException>(() => result.ResolvePackageUri(feed.Packages[0], false));
        }

        [TestMethod]
        public void Versions_Compare_Without_Regard_To_Missing_Parts_And_Suffixes()
        {
            Assert.IsTrue(VersionText.TryParse("1.4-beta", out var a));
            Assert.IsTrue(VersionText.TryParse("1.4.0", out var b));
            Assert.AreEqual(0, VersionText.Compare(a, b));
            Assert.IsTrue(VersionText.TryParse("2", out var c));
            Assert.IsTrue(VersionText.Compare(c, b) > 0);
        }

        [TestMethod]
        public async Task A_Download_Is_Kept_Only_With_The_Right_Size_And_Hash()
        {
            var directory = Directory.CreateTempSubdirectory("itv-update-test").FullName;
            var content = new byte[] { 1, 2, 3, 4, 5, 6 };
            using var http = new HttpClient(new FixedHandler(content));
            var downloader = new PackageDownloader(http, directory);
            var package = new UpdatePackage { Id = "Shop.Agent", Version = "1.4.2", Size = content.Length, Sha256 = Convert.ToHexString(SHA256.HashData(content)) };

            var path = await downloader.DownloadAsync(package, new Uri("https://shop.example/a.msi"), CancellationToken.None);
            CollectionAssert.AreEqual(content, await File.ReadAllBytesAsync(path));

            var wrong = new UpdatePackage { Id = "Shop.Agent", Version = "1.4.3", Size = content.Length, Sha256 = new string('0', 64) };
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => downloader.DownloadAsync(wrong, new Uri("https://shop.example/b.msi"), CancellationToken.None));
            Assert.AreEqual(1, Directory.GetFiles(directory).Length, "a package with a wrong hash is not kept");
        }

        [TestMethod]
        public void The_Result_Of_An_Installation_Is_Read_From_Its_Log()
        {
            var log = Path.Combine(Directory.CreateTempSubdirectory("itv-update-test").FullName, "install.log");
            var evaluator = new MsiInstallResultEvaluator();
            Assert.AreEqual(InstallOutcome.Unknown, evaluator.FromLog(log).Outcome, "no log yet");

            File.WriteAllText(log, "MSI (s) (4C:10) [10:00:00:000]: Product: Shop -- Installation started.\r\n", System.Text.Encoding.Unicode);
            Assert.AreEqual(InstallOutcome.Unknown, evaluator.FromLog(log).Outcome, "no result yet");

            File.AppendAllText(log, "MSI (s) (4C:A0) [10:00:01:000]: MainEngineThread is returning 1603\r\n"
                                    + "MSI (s) (4C:10) [10:00:02:000]: MainEngineThread is returning 3010\r\n", System.Text.Encoding.Unicode);
            var result = evaluator.FromLog(log);
            Assert.AreEqual(InstallOutcome.SucceededRestartRequired, result.Outcome, "the last engine thread counts");
            Assert.AreEqual(3010, result.ExitCode);

            Assert.AreEqual(InstallOutcome.Busy, evaluator.FromExitCode(1618).Outcome);
            Assert.AreEqual(InstallOutcome.Failed, evaluator.FromExitCode(1603).Outcome);
        }

        [TestMethod]
        public void The_State_Survives_A_Restart()
        {
            var directory = Directory.CreateTempSubdirectory("itv-update-test").FullName;
            var store = new InstallStateStore(directory);
            Assert.IsNull(store.Load().Pending);

            store.Save(new InstallState { Pending = new PendingInstall { PackageId = "Shop.Agent", Version = "1.4.2", SessionsToRestart = { 1, 2 } } });

            var loaded = new InstallStateStore(directory).Load();
            Assert.AreEqual("1.4.2", loaded.Pending!.Version);
            CollectionAssert.AreEqual(new[] { 1, 2 }, loaded.Pending.SessionsToRestart);
        }

        private sealed class FixedHandler : HttpMessageHandler
        {
            private readonly byte[] content;

            public FixedHandler(byte[] content)
            {
                this.content = content;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
            }
        }
    }
}
