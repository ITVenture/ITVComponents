using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Administration;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Feed;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.UpdateService.Test
{
    /// <summary>
    /// Releases, package stores, the feed and its delivery (update service, phase 5a).
    /// </summary>
    [TestClass]
    public class UpdateReleaseTest
    {
        private UpdateTestContextFactory factory = null!;
        private IUpdatePackageStore store = null!;
        private UpdateReleaseAdministration<UpdateTestContext, TestClientApp, TestClientAppTemplate> admin = null!;
        private UpdateFeedBuilder<UpdateTestContext> builder = null!;

        [TestInitialize]
        public async Task Setup()
        {
            factory = new UpdateTestContextFactory();
            store = new DatabaseUpdatePackageStore<UpdateTestContext>(factory);
            admin = new UpdateReleaseAdministration<UpdateTestContext, TestClientApp, TestClientAppTemplate>(factory, store);
            builder = new UpdateFeedBuilder<UpdateTestContext>(factory);
            await admin.EnsureProductAsync("Shop.Agent", PackageKinds.Base, "Shop agent", null, CancellationToken.None);
            await admin.EnsureProductAsync("Shop.Epson", PackageKinds.Module, "Epson printer", "Shop.Agent", CancellationToken.None);
        }

        [TestMethod]
        public async Task The_Database_Store_Writes_And_Reads_A_Package_Block_By_Block()
        {
            await AssertStoreRoundTrip(store);
        }

        [TestMethod]
        public async Task The_File_Store_Writes_And_Reads_A_Package()
        {
            await AssertStoreRoundTrip(new FileUpdatePackageStore(Directory.CreateTempSubdirectory("itv-update-store").FullName));
        }

        [TestMethod]
        public async Task The_Feed_Offers_The_Newest_Application_And_Every_Module_Release()
        {
            await Upload("Shop.Agent", "1.0");
            await Upload("Shop.Agent", "1.1");
            await Upload("Shop.Epson", "1.0", requires: "[1.0,2.0)");

            var feed = await Feed();

            var app = feed.Packages.Single(p => p.Kind == PackageKinds.Base);
            Assert.AreEqual("1.1", app.Version);
            StringAssert.StartsWith(app.Url, "packages/");
            var module = feed.Packages.Single(p => p.Kind == PackageKinds.Module);
            Assert.AreEqual("[1.0,2.0)", module.Requires!["Shop.Agent"]);
            Assert.AreEqual(64, app.Sha256!.Length);
        }

        [TestMethod]
        public async Task Withdrawing_A_Release_Shows_The_Former_One_Again()
        {
            await Upload("Shop.Agent", "1.0");
            var newest = await Upload("Shop.Agent", "1.1");

            await admin.SetWithdrawnAsync(newest.UpdateReleaseId, true, CancellationToken.None);

            Assert.AreEqual("1.0", (await Feed()).Packages.Single(p => p.Kind == PackageKinds.Base).Version);
            Assert.IsNull(await builder.FindOfferedReleaseAsync("Shop.Agent", newest.UpdateReleaseId, CancellationToken.None), "a withdrawn package is not delivered");
        }

        [TestMethod]
        public async Task A_Version_Is_Published_Once_And_A_Range_Must_Be_Readable()
        {
            await Upload("Shop.Agent", "1.0");

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Upload("Shop.Agent", "1.0"));
            await Assert.ThrowsExactlyAsync<FormatException>(() => Upload("Shop.Epson", "1.0", requires: "[1.0"));
        }

        [TestMethod]
        public async Task Cleanup_Keeps_The_Newest_Releases_And_Deletes_The_Packages_Of_The_Others()
        {
            var oldest = await Upload("Shop.Agent", "1.0");
            await Upload("Shop.Agent", "1.1");
            await Upload("Shop.Agent", "1.2");

            Assert.AreEqual(1, await admin.CleanupAsync("Shop.Agent", "stable", 2, CancellationToken.None));

            await using var db = factory.CreateDbContext();
            CollectionAssert.AreEquivalent(new[] { "1.1", "1.2" }, db.UpdateReleases.Select(r => r.Version).ToArray());
            Assert.IsNull(await store.OpenAsync(oldest.StorageKey, CancellationToken.None));
        }

        [TestMethod]
        public async Task Publishing_An_Application_Version_Warns_About_Assigned_Modules_Without_A_Fitting_Release()
        {
            await Upload("Shop.Epson", "1.0", requires: "[1.0,2.0)");
            await using (var db = factory.CreateDbContext())
            {
                db.ClientApps.Add(new TestClientApp { ClientAppId = 1, TenantId = 7, ClientAppTemplateId = 3, ClientName = "Kasse 1" });
                db.ClientApps.Add(new TestClientApp { ClientAppId = 2, TenantId = 8, ClientAppTemplateId = 4, ClientName = "Kasse 2" });
                db.ClientApps.Add(new TestClientApp { ClientAppId = 3, TenantId = 8, ClientAppTemplateId = 4, ClientName = "Kasse 3" });
                db.ClientAppTemplateModuleAssignments.Add(new ClientAppTemplateModuleAssignment { ClientAppTemplateId = 3, ModuleProductKey = "Shop.Epson" });
                db.ClientAppModuleAssignments.Add(new ClientAppModuleAssignment { TenantId = 8, ClientAppId = 2, ModuleProductKey = "Shop.Epson" });
                await db.SaveChangesAsync();
            }

            Assert.AreEqual(0, (await admin.CheckModulesForAsync("Shop.Agent", "stable", "1.5", CancellationToken.None)).Count);

            var warning = (await admin.CheckModulesForAsync("Shop.Agent", "stable", "2.0", CancellationToken.None)).Single();
            Assert.AreEqual("Shop.Epson", warning.ModuleProductKey);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, warning.ClientApps.Select(a => a.ClientAppId).ToArray());
        }

        [TestMethod]
        public async Task The_Feed_Is_Delivered_With_ETag_Signature_And_Ranges()
        {
            var release = await Upload("Shop.Agent", "1.0", size: 3 * 1024 * 1024);
            using var certificate = SignerCertificate();
            using var signer = new CertificateFeedSigner(certificate);
            using var host = await Host(signer);
            var http = host.GetTestClient();

            var manifest = await http.GetAsync("/updates/Shop.Agent/stable.json");
            Assert.AreEqual(HttpStatusCode.OK, manifest.StatusCode);
            var tag = manifest.Headers.ETag!;
            var bytes = await manifest.Content.ReadAsByteArrayAsync();

            var again = new HttpRequestMessage(HttpMethod.Get, "/updates/Shop.Agent/stable.json");
            again.Headers.IfNoneMatch.Add(tag);
            Assert.AreEqual(HttpStatusCode.NotModified, (await http.SendAsync(again)).StatusCode);

            var signature = await http.GetStringAsync("/updates/Shop.Agent/stable.json.sig");
            Assert.IsTrue(FeedSignature.Verify(bytes, signature, new[] { signer.Thumbprint }, out var reason), reason);

            var url = JsonSerializer.Deserialize<UpdateFeed>(bytes)!.Packages.Single().Url!;
            var range = new HttpRequestMessage(HttpMethod.Get, "/updates/Shop.Agent/" + url);
            range.Headers.Range = new RangeHeaderValue(2 * 1024 * 1024 - 5, 2 * 1024 * 1024 + 4);
            var part = await http.SendAsync(range);
            Assert.AreEqual(HttpStatusCode.PartialContent, part.StatusCode);
            CollectionAssert.AreEqual(Content(3 * 1024 * 1024).Skip(2 * 1024 * 1024 - 5).Take(10).ToArray(), await part.Content.ReadAsByteArrayAsync(), "a range across two blocks");

            Assert.AreEqual(HttpStatusCode.NotFound, (await http.GetAsync($"/updates/Other.App/packages/{release.UpdateReleaseId}/{release.FileName}")).StatusCode, "a package only in the feed of its product");
        }

        private static async Task AssertStoreRoundTrip(IUpdatePackageStore target)
        {
            var content = Content(2 * 1024 * 1024 + 500_000);
            var stored = await target.SaveAsync("pkg-1", new MemoryStream(content), CancellationToken.None);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(content)), stored.Sha256);
            Assert.AreEqual(content.LongLength, stored.Size);

            await using (var read = (await target.OpenAsync("pkg-1", CancellationToken.None))!)
            {
                Assert.AreEqual(content.LongLength, read.Length);
                var all = new MemoryStream();
                await read.CopyToAsync(all);
                CollectionAssert.AreEqual(content, all.ToArray());

                read.Seek(1_500_000, SeekOrigin.Begin);
                var part = new byte[1000];
                await read.ReadExactlyAsync(part);
                CollectionAssert.AreEqual(content.Skip(1_500_000).Take(1000).ToArray(), part);
            }

            await target.DeleteAsync("pkg-1", CancellationToken.None);
            Assert.IsNull(await target.OpenAsync("pkg-1", CancellationToken.None));
        }

        private Task<UpdateRelease> Upload(string product, string version, string? requires = null, int size = 1000)
        {
            return admin.UploadReleaseAsync(new ReleaseUpload
            {
                ProductKey = product, Version = version, Requires = requires,
                FileName = $"{product}-{version}{(product == "Shop.Agent" ? ".msi" : ".zip")}", UploadedBy = "test"
            }, new MemoryStream(Content(size)), CancellationToken.None);
        }

        private async Task<UpdateFeed> Feed()
        {
            var built = await builder.BuildAsync("Shop.Agent", "stable", CancellationToken.None);
            return JsonSerializer.Deserialize<UpdateFeed>(built!.Manifest)!;
        }

        private async Task<IHost> Host(IFeedSigner signer)
        {
            var app = WebApplication.CreateBuilder();
            app.WebHost.UseTestServer();
            app.Services.AddSingleton<IDbContextFactory<UpdateTestContext>>(factory);
            app.Services.AddSingleton(store);
            app.Services.AddSingleton(builder);
            app.Services.AddSingleton(signer);
            var web = app.Build();
            web.MapUpdateFeed<UpdateTestContext>();
            await web.StartAsync();
            return web;
        }

        private static byte[] Content(int size)
        {
            var content = new byte[size];
            for (var i = 0; i < size; i++)
            {
                content[i] = (byte)(i * 31 % 251);
            }

            return content;
        }

        private static X509Certificate2 SignerCertificate()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            return new CertificateRequest("CN=Feed", key, HashAlgorithmName.SHA256).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        }
    }
}
