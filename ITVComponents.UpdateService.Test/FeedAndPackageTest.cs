using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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

        [TestMethod]
        public async Task An_Interrupted_Download_Is_Continued_Where_It_Stopped()
        {
            var (downloader, package, content, server) = Interrupting(honorRange: true);

            await Assert.ThrowsExactlyAsync<IOException>(() => downloader.DownloadAsync(package, new Uri("https://shop.example/a.msi"), CancellationToken.None));
            var path = await downloader.DownloadAsync(package, new Uri("https://shop.example/a.msi"), CancellationToken.None);

            CollectionAssert.AreEqual(content, await File.ReadAllBytesAsync(path));
            var second = server.Requests[1];
            Assert.AreEqual(4000, second.Headers.Range!.Ranges.Single().From, "only the rest is fetched");
            Assert.AreEqual("\"v1\"", second.Headers.IfRange!.EntityTag!.Tag, "conditional on the file of the first attempt");
            Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(path)!).Length, "no part and no notes are left");
        }

        [TestMethod]
        public async Task A_Server_Sending_The_Whole_File_Again_Replaces_The_Part()
        {
            var (downloader, package, content, _) = Interrupting(honorRange: false);

            await Assert.ThrowsExactlyAsync<IOException>(() => downloader.DownloadAsync(package, new Uri("https://shop.example/a.msi"), CancellationToken.None));
            var path = await downloader.DownloadAsync(package, new Uri("https://shop.example/a.msi"), CancellationToken.None);

            CollectionAssert.AreEqual(content, await File.ReadAllBytesAsync(path));
        }

        [TestMethod]
        public async Task A_Part_Of_Another_Package_Is_Not_Continued()
        {
            var (downloader, package, content, server) = Interrupting(honorRange: true);
            await Assert.ThrowsExactlyAsync<IOException>(() => downloader.DownloadAsync(package, new Uri("https://shop.example/a.msi"), CancellationToken.None));

            // Same id and version, other content (re-published after a cleanup).
            var other = content.Select(b => (byte)(b ^ 0xFF)).ToArray();
            server.Content = other;
            var republished = new UpdatePackage { Id = package.Id, Version = package.Version, Size = other.Length, Sha256 = Convert.ToHexString(SHA256.HashData(other)) };
            var path = await downloader.DownloadAsync(republished, new Uri("https://shop.example/a.msi"), CancellationToken.None);

            CollectionAssert.AreEqual(other, await File.ReadAllBytesAsync(path));
            Assert.IsNull(server.Requests.Last().Headers.Range, "from the start");
        }

        private static (PackageDownloader Downloader, UpdatePackage Package, byte[] Content, RangeHandler Server) Interrupting(bool honorRange)
        {
            var content = Enumerable.Range(0, 10_000).Select(i => (byte)(i * 7 % 256)).ToArray();
            var server = new RangeHandler(content, honorRange) { FailFirstAfter = 4000 };
            var downloader = new PackageDownloader(new HttpClient(server), Directory.CreateTempSubdirectory("itv-update-test").FullName);
            var package = new UpdatePackage { Id = "Shop.Agent", Version = "1.4.2", Size = content.Length, Sha256 = Convert.ToHexString(SHA256.HashData(content)) };
            return (downloader, package, content, server);
        }

        /// <summary>
        /// A server with ranges; the first response breaks off after <see cref="FailFirstAfter"/> bytes, like a weak line.
        /// </summary>
        private sealed class RangeHandler : HttpMessageHandler
        {
            private readonly bool honorRange;

            public RangeHandler(byte[] content, bool honorRange)
            {
                Content = content;
                this.honorRange = honorRange;
            }

            public byte[] Content { get; set; }

            public int? FailFirstAfter { get; set; }

            public List<HttpRequestMessage> Requests { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                var tag = new EntityTagHeaderValue("\"v1\"");
                var from = honorRange && request.Headers.Range?.Ranges.Single().From is { } start ? (int)start : 0;
                var body = Content.Skip(from).ToArray();
                Stream stream = Requests.Count == 1 && FailFirstAfter is { } fail ? new BreakingStream(body, fail) : new MemoryStream(body);
                var response = new HttpResponseMessage(from > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK) { Content = new StreamContent(stream) };
                response.Headers.ETag = tag;
                if (from > 0)
                {
                    response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, Content.Length - 1, Content.Length);
                }

                return Task.FromResult(response);
            }
        }

        private sealed class BreakingStream : MemoryStream
        {
            private readonly int failAfter;

            public BreakingStream(byte[] content, int failAfter) : base(content)
            {
                this.failAfter = failAfter;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (Position >= failAfter)
                {
                    throw new IOException("The connection was reset.");
                }

                return base.Read(buffer, offset, (int)Math.Min(count, failAfter - Position));
            }

            public override int Read(Span<byte> buffer)
            {
                if (Position >= failAfter)
                {
                    throw new IOException("The connection was reset.");
                }

                return base.Read(buffer.Slice(0, (int)Math.Min(buffer.Length, failAfter - Position)));
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => Task.FromResult(Read(buffer, offset, count));

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                => ValueTask.FromResult(Read(buffer.Span));
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
