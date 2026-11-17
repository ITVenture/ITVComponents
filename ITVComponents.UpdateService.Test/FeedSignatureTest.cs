using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.UpdateService.Abstractions.Feed;
using ITVComponents.UpdateService.Feed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.UpdateService.Test
{
    /// <summary>
    /// The signature of the feed (P3): optional, but when it is there it must fit.
    /// </summary>
    [TestClass]
    public class FeedSignatureTest
    {
        private static readonly byte[] Manifest = Encoding.UTF8.GetBytes("""{ "channel": "stable", "packages": [ { "id": "Shop.Agent", "version": "1.4.2", "url": "a.msi", "sha256": "00" } ] }""");

        [TestMethod]
        public void A_Manifest_Signed_With_An_Rsa_Or_An_Ec_Key_Is_Accepted()
        {
            using var rsa = Rsa();
            using var ec = Ec();

            Assert.IsTrue(FeedSignature.Verify(Manifest, FeedSignature.Sign(Manifest, rsa), new[] { rsa.Thumbprint }, out var reason), reason);
            Assert.IsTrue(FeedSignature.Verify(Manifest, FeedSignature.Sign(Manifest, ec), new[] { "other", ec.Thumbprint.ToLowerInvariant() }, out reason), reason);
        }

        [TestMethod]
        public void A_Changed_Manifest_Is_Refused()
        {
            using var rsa = Rsa();
            var signature = FeedSignature.Sign(Manifest, rsa);
            var changed = (byte[])Manifest.Clone();
            changed[^3] ^= 1;

            Assert.IsFalse(FeedSignature.Verify(changed, signature, new[] { rsa.Thumbprint }, out var reason));
            StringAssert.Contains(reason, "changed after signing");
        }

        [TestMethod]
        public void A_Signature_By_An_Untrusted_Certificate_Is_Refused()
        {
            using var trusted = Rsa();
            using var stranger = Rsa();

            Assert.IsFalse(FeedSignature.Verify(Manifest, FeedSignature.Sign(Manifest, stranger), new[] { trusted.Thumbprint }, out var reason));
            StringAssert.Contains(reason, "not a trusted feed signer");
        }

        [TestMethod]
        public void The_Signature_File_Carries_No_Private_Key()
        {
            using var rsa = Rsa();
            var signature = System.Text.Json.JsonSerializer.Deserialize<FeedSignature>(FeedSignature.Sign(Manifest, rsa))!;

            using var carried = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(signature.Certificate!));
            Assert.IsFalse(carried.HasPrivateKey);
        }

        [TestMethod]
        public async Task A_Required_Signature_Must_Be_There()
        {
            using var rsa = Rsa();
            var client = Client(rsa, require: true, signature: null);

            var refused = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetFeedAsync(CancellationToken.None));
            StringAssert.Contains(refused.Message, "not signed");
        }

        [TestMethod]
        public async Task A_Valid_Signature_Lets_The_Manifest_Through()
        {
            using var rsa = Rsa();
            var client = Client(rsa, require: true, signature: FeedSignature.Sign(Manifest, rsa));

            var result = await client.GetFeedAsync(CancellationToken.None);

            Assert.AreEqual("1.4.2", result!.Feed.Packages[0].Version);
        }

        [TestMethod]
        public async Task Without_Requirement_A_Missing_Signature_Is_Fine_But_A_Wrong_One_Is_Not()
        {
            using var rsa = Rsa();
            using var stranger = Rsa();

            Assert.IsNotNull(await Client(rsa, require: false, signature: null).GetFeedAsync(CancellationToken.None));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Client(rsa, require: false, signature: FeedSignature.Sign(Manifest, stranger)).GetFeedAsync(CancellationToken.None));
        }

        [TestMethod]
        public void Requiring_A_Signature_Without_Trusted_Signers_Is_A_Configuration_Error()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => new UpdateFeedClient(new HttpClient(), new UpdateServiceOptions { FeedUrl = "https://shop.example/updates", RequireFeedSignature = true }));
        }

        private static UpdateFeedClient Client(X509Certificate2 trusted, bool require, string? signature)
        {
            var http = new HttpClient(new FeedHandler(signature));
            return new UpdateFeedClient(http, new UpdateServiceOptions
            {
                FeedUrl = "https://shop.example/updates",
                RequireFeedSignature = require,
                FeedSigningThumbprints = { trusted.Thumbprint }
            });
        }

        private static X509Certificate2 Rsa()
        {
            using var key = RSA.Create(2048);
            return new CertificateRequest("CN=Feed signer", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        }

        private static X509Certificate2 Ec()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            return new CertificateRequest("CN=Feed signer EC", key, HashAlgorithmName.SHA256)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        }

        private sealed class FeedHandler : HttpMessageHandler
        {
            private readonly string? signature;

            public FeedHandler(string? signature)
            {
                this.signature = signature;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUri!.AbsolutePath.EndsWith(".sig", StringComparison.Ordinal))
                {
                    return Task.FromResult(signature == null
                        ? new HttpResponseMessage(HttpStatusCode.NotFound)
                        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(signature) });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Manifest) });
            }
        }
    }
}
