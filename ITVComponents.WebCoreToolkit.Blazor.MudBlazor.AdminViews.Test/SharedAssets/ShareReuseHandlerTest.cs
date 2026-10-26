using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test.SharedAssets
{
    /// <summary>
    /// "Eine Freigabe je Ort" am Handler: <c>CreateAsync</c> fragt den Adapter nach der bestehenden Freigabe -
    /// aber nur fuer eine gespeicherte, anonyme Anfrage ohne Empfaenger. Ob die Vorlage ueberhaupt
    /// wiederverwendet, entscheidet der Adapter (siehe <c>SharedAssetReuseTest</c>).
    /// </summary>
    /// <remarks>
    /// Die Regel steht im Handler und nicht erst am Knopf, damit ein Host, der im Code ausstellt
    /// (MiniStore: die Selbstbedienungs-Station), nicht an ihr vorbei eine zweite Freigabe anlegt.
    /// </remarks>
    [TestClass]
    public class ShareReuseHandlerTest
    {
        private const string Origin = "https://shop.example.com";

        [TestMethod]
        public async Task AnAnonymousShare_OfAPlaceThatHasOne_ShowsTheExistingOne()
        {
            var adapter = new FakeAdapter { Existing = Stored("existing", "Shop entrance") };
            var handler = Handler(adapter);

            var result = await handler.CreateAsync(User(), Anonymous(), Origin);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Created);
            Assert.IsTrue(result.Anonymous);
            Assert.AreEqual("existing", result.AssetKey);
            Assert.AreEqual("anon:existing", result.Link);
            Assert.AreEqual("Shop entrance", result.Title, "der gespeicherte Titel steht auf dem Aushang");
            Assert.AreEqual(0, adapter.Created, "kein zweites Geheimnis im Umlauf");
        }

        [TestMethod]
        public async Task AnAnonymousShare_OfANewPlace_IsIssued()
        {
            var adapter = new FakeAdapter();
            var handler = Handler(adapter);

            var result = await handler.CreateAsync(User(), Anonymous(), Origin);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Created);
            Assert.AreEqual(1, adapter.Lookups);
            Assert.AreEqual(1, adapter.Created);
            Assert.AreEqual("anon:new", result.Link);
        }

        [TestMethod]
        public async Task AShareWithRecipients_IsAlwaysIssued_WithoutLookingForAnExistingOne()
        {
            var adapter = new FakeAdapter { Existing = Stored("existing", "x") };
            var handler = Handler(adapter);
            var request = new ShareRequestViewModel
            {
                RequestPath = "/checkout/start", TemplateKey = "pos-start", Title = "For Anna",
                Reach = ShareReach.Recipients, UserFilters = { "anna" }
            };

            var result = await handler.CreateAsync(User(), request, Origin);

            Assert.IsTrue(result.Created);
            Assert.AreEqual(0, adapter.Lookups, "wer mit Empfaengern teilt, meint beim zweiten Mal einen anderen");
        }

        [TestMethod]
        public async Task AnAnonymousShare_ThatAlsoReachesEveryoneSignedIn_IsNotTheSamePlace()
        {
            var adapter = new FakeAdapter { Existing = Stored("existing", "x") };
            var handler = Handler(adapter);
            var request = Anonymous();
            request.Reach = ShareReach.AnyoneSignedIn;

            var result = await handler.CreateAsync(User(), request, Origin);

            Assert.IsTrue(result.Created);
            Assert.AreEqual(0, adapter.Lookups);
        }

        [TestMethod]
        public async Task AnAdHocTicket_IsNeverReused()
        {
            var adapter = new FakeAdapter { Existing = Stored("existing", "x") };
            var handler = Handler(adapter);
            var request = Anonymous();
            request.AdHoc = true;

            var result = await handler.CreateAsync(User(), request, Origin);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Created);
            Assert.IsNull(result.AssetKey, "ein Ticket steht nirgends - es gibt nichts zu erneuern");
            Assert.AreEqual(0, adapter.Lookups);
        }

        [TestMethod]
        public async Task AnExistingShare_WithoutAnAnonymousLink_IsNotShown()
        {
            var adapter = new FakeAdapter { Existing = Stored("existing", "x"), AnonymousLinks = false };
            var handler = Handler(adapter);

            var found = await handler.FindReusableAsync(User(), "/checkout/start", "pos-start",
                new Dictionary<string, string>(), Origin);

            Assert.IsNull(found, "dann wie gewohnt ausstellen - ein leerer QR hilft niemandem");
        }

        [TestMethod]
        public async Task FindReusable_HandsTheArgumentsThrough()
        {
            var adapter = new FakeAdapter { Existing = Stored("till-2", "Station 2") };
            var handler = Handler(adapter);

            var found = await handler.FindReusableAsync(User(), "/checkout/kiosk", "pos-kiosk",
                new Dictionary<string, string> { ["clientAppId"] = "2" }, Origin);

            Assert.AreEqual("till-2", found?.AssetKey);
            Assert.AreEqual("pos-kiosk", adapter.LastTemplateKey);
            Assert.AreEqual("2", adapter.LastArguments?["clientAppId"]);
        }

        private static SharedAssetAdminHandler Handler(FakeAdapter adapter)
            => new(adapter, NullLogger<SharedAssetAdminHandler>.Instance);

        private static ClaimsPrincipal User()
            => new(new ClaimsIdentity(new[] { new Claim(System.Security.Claims.ClaimTypes.Name, "cashier") }, "Test"));

        private static ShareRequestViewModel Anonymous()
            => new() { RequestPath = "/checkout/start", TemplateKey = "pos-start", Title = "Entrance", Anonymous = true };

        private static FullAssetInfo Stored(string key, string title)
            => new() { AssetKey = key, AssetTitle = title, AnonymousAccessTokenRaw = "{secret}" };

        /// <summary>
        /// Beantwortet nur, was der Handler beim Teilen fragt. Der Rest wirft - fragt der Handler danach,
        /// hat sich sein Weg geaendert, und dieser Test soll das merken.
        /// </summary>
        private sealed class FakeAdapter : ISharedAssetAdapter
        {
            public FullAssetInfo? Existing { get; set; }
            public bool AnonymousLinks { get; set; } = true;
            public int Lookups { get; private set; }
            public int Created { get; private set; }
            public string? LastTemplateKey { get; private set; }
            public IDictionary<string, string>? LastArguments { get; private set; }

            public FullAssetInfo? FindReusableSharedAsset(string requestPath, AssetTemplateInfo template,
                IDictionary<string, string> argumentValues, ClaimsPrincipal requestor)
            {
                Lookups++;
                LastTemplateKey = template.TemplateKey;
                LastArguments = argumentValues;
                return Existing;
            }

            public AssetInfo? CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title,
                IDictionary<string, string> argumentValues, string recipientLabel, bool anonymous,
                IEnumerable<string> userFilters, IEnumerable<string> tenantFilters, out string? error)
            {
                Created++;
                error = null;
                return new FullAssetInfo { AssetKey = "new", AssetTitle = title };
            }

            public string? CreateAnonymousLink(AssetInfo info, string origin)
                => AnonymousLinks ? $"anon:{info.AssetKey}" : null;

            public string CreateLink(AssetInfo info, string origin) => $"link:{info.AssetKey}";

            public string? CreateAdHocTicket(string requestPath, AssetTemplateInfo template,
                IDictionary<string, string> argumentValues, string recipientLabel, TimeSpan? lifetime, string origin,
                out string? error)
            {
                error = null;
                return "ticket";
            }

            public AssetInfo GetAssetInfo(string assetKey, ClaimsPrincipal requestor, bool asOwner = false) => throw Unexpected();
            public bool VerifyRequestLocation(string requestPath, string assetKey, string userScope, ClaimsPrincipal requestor) => throw Unexpected();
            public AssetTemplateInfo[] GetEligibleShares(string requestPath) => throw Unexpected();
            public AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title) => throw Unexpected();
            public bool UpdateSharedAsset(FullAssetInfo updateInfo) => throw Unexpected();
            public bool DeleteSharedAsset(FullAssetInfo assetInfo) => throw Unexpected();
            public string CreateAnonymousLink(AssetInfo info, HttpContext context) => throw Unexpected();
            public string CreateLink(AssetInfo info, HttpContext context) => throw Unexpected();
            public SharedAssetListItem[] ListSharedAssets(string search, int skip, int take, out int total) => throw Unexpected();
            public bool RotateAnonymousToken(string assetKey) => throw Unexpected();
            public AssetInfo GetTicketInfo(string tenantName, string payload, ClaimsPrincipal requestor, bool forAuthentication = false) => throw Unexpected();
            public bool VerifyAssetValidity(AssetInfo info) => throw Unexpected();
            public bool RevokeTicket(string nonce, DateTime expiresUtc) => throw Unexpected();
            public FullAssetInfo FindAnonymousAsset(string assetKey) => throw Unexpected();
            void ISharedAssetAdapter.SetImpersonationOff() => throw Unexpected();
            void ISharedAssetAdapter.SetImpersonationOn() => throw Unexpected();

            private static NotSupportedException Unexpected() => new("The handler is not expected to ask this while sharing.");
        }
    }
}
