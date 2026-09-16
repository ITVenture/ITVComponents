using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Nagelt fest, dass eine <b>gueltige</b> Anmeldung nicht daran scheitert, dass der Aufruf
    /// <b>nebenbei</b> nicht zu einer Freigabe passt.
    /// </summary>
    /// <remarks>
    /// Die Stelle ist <c>GetUserPermissions</c>:
    /// <code>
    /// isAuthenticated = (IsLegitSharedAssetPath(…, out var denied) || IsUserAuthenticated(…)) &amp;&amp; !denied;
    /// </code>
    /// <c>IsLegitSharedAssetPath</c> setzt <c>denied</c> und gibt <b>false</b> zurueck, wenn der angefragte
    /// Pfad nicht zur Freigabe gehoert. Das <c>&amp;&amp; !denied</c> kippt dann auch das Ja der regulaeren
    /// Anmeldung - obwohl mit ihr alles in Ordnung ist.
    /// <para>
    /// <b>Wen das trifft:</b> jede Identitaet, die einen <c>FixedUserScope</c>-Anspruch traegt, waehrend im
    /// Kontext eine Freigabe steht. Genau diesen Anspruch setzt der Anmelder fuer <b>jeden Maschinenzugang</b>
    /// (er ist dort die einzige Quelle des Mandanten - ein gRPC-Endpunkt hat kein Mandantensegment in der
    /// Route). Die Absage lautet dann "kein angemeldeter Benutzer", und die Suche beginnt bei der
    /// Rechteaufloesung, wo alles stimmt.
    /// </para>
    /// </remarks>
    [TestClass]
    public class MachineIdentityAssetDenialTests
    {
        private const string MachineLabel = "Kasse1-5684110770ae4622871d502f402abc58";
        private const string Tenant = "45e6968d5a7044c38ee17787efe0ee66";

        [TestMethod]
        public void AMachineIdentity_StaysAuthenticated_WhenTheRequestIsNoAssetPath()
        {
            var services = Provider(hasAsset: true, assetPathMatches: false);

            var perms = services.GetUserPermissions(out _, out var isAuthenticated);

            Assert.IsTrue(isAuthenticated,
                "der Zugang ist gueltig und die Rechteaufloesung sagt ja. Dass der Aufruf nicht zu einer " +
                "Freigabe gehoert, ist KEINE Aussage ueber die Anmeldung - eine Maschine ruft nun einmal " +
                "keinen Freigabe-Link auf.");
            CollectionAssert.Contains(perms, "ActAsService");
        }

        [TestMethod]
        public void WithoutAnyAssetInContext_TheMachineIdentityIsAuthenticated()
        {
            var services = Provider(hasAsset: false, assetPathMatches: false);

            services.GetUserPermissions(out _, out var isAuthenticated);

            Assert.IsTrue(isAuthenticated,
                "die Gegenprobe: ohne Freigabe im Kontext wird der Zweig gar nicht erst betreten.");
        }

        [TestMethod]
        public void AMatchingAssetPath_IsStillHonoured()
        {
            var services = Provider(hasAsset: true, assetPathMatches: true);

            services.GetUserPermissions(out _, out var isAuthenticated);

            Assert.IsTrue(isAuthenticated,
                "passt der Pfad zur Freigabe, gilt der Aufruf ohnehin - dieser Weg darf beim Reparieren " +
                "nicht verloren gehen.");
        }

        // --- Testgeruest ----------------------------------------------------------------------

        private static IServiceProvider Provider(bool hasAsset, bool assetPathMatches)
        {
            // Die Identitaet einer Maschine: ein Name, das App-User-Label und - entscheidend - der
            // FixedUserScope-Anspruch, aus dem ihr Mandant kommt.
            var identity = new ClaimsIdentity(
            [
                new Claim(System.Security.Claims.ClaimTypes.Name, MachineLabel),
                new Claim(ITVComponents.WebCoreToolkit.ClaimTypes.FixedUserScope, Tenant),
                // Die Anspruechse, an denen ein Anwendungs-Zugang als solcher erkennbar ist. Ohne sie
                // waere das hier irgendeine Identitaet mit festem Mandanten - und der Test pruefte
                // etwas anderes, als er behauptet.
                new Claim(ITVComponents.WebCoreToolkit.ClaimTypes.ClientAppId, "NywHKJr3"),
                new Claim(ITVComponents.WebCoreToolkit.ClaimTypes.ClientAppAccess, MachineLabel)
            ], "API Key");

            var user = new ClaimsPrincipal(identity);
            var repo = new FakeSecurityRepository
            {
                Authenticated = true,
                PermissionsForLabels = [new Permission { PermissionName = "ActAsService" }]
            };
            var ctx = new MachineContextUserProvider { User = user, RequestPath = "/ServiceHub" };
            var provider = new AssetAwareServiceProvider(ctx,
                hasAsset ? new AssetContextStub() : null,
                new AssetAdapterStub(assetPathMatches));
            // Der Freigabe-Weg verlangt ausdruecklich das gestapelte SecurityRepository - passt der Pfad
            // zur Freigabe, legt er eine Asset-Sicht darauf. Mit einem blanken ISecurityRepository wirft
            // er, und zwar mit genau dieser Ansage.
            provider.Repository = provider.GetAssetSecurityRepository(repo);
            ctx.Services = provider;
            return provider;
        }

        private sealed class MachineContextUserProvider : IContextUserProvider
        {
            public ClaimsPrincipal User { get; set; }
            public IServiceProvider Services { get; set; }
            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();
            public string RequestPath { get; set; }
        }

        private sealed class AssetAwareServiceProvider : IServiceProvider
        {
            private readonly IContextUserProvider ctx;
            private readonly ISharedAssetContext assetContext;
            private readonly ISharedAssetAdapter assetAdapter;

            public AssetAwareServiceProvider(IContextUserProvider ctx,
                ISharedAssetContext assetContext, ISharedAssetAdapter assetAdapter)
            {
                this.ctx = ctx;
                this.assetContext = assetContext;
                this.assetAdapter = assetAdapter;
            }

            /// <summary>Wird erst nach dem Bauen gesetzt - der Stapel braucht den Anbieter selbst.</summary>
            public ISecurityRepository Repository { get; set; }

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(ISecurityRepository)) return Repository;
                if (serviceType == typeof(IContextUserProvider)) return ctx;
                if (serviceType == typeof(IUserNameMapper)) return new MachineUserNameMapper();
                if (serviceType == typeof(ISharedAssetContext)) return assetContext;
                if (serviceType == typeof(ISharedAssetAdapter)) return assetAdapter;
                return null;
            }
        }

        /// <summary>Die Bezeichner einer Anwendung: der blanke Name UND die App-User-Wicklung.</summary>
        private sealed class MachineUserNameMapper : IUserNameMapper
        {
            public string[] GetUserLabels(IIdentity user)
                => [MachineLabel, string.Format(Global.AppUserKeyIndicatorFormat, MachineLabel)];

            public string UniqueName { get; set; }
            public void Dispose() { }
            public event EventHandler Disposed;
        }

        /// <summary>Eine Freigabe steht im Kontext - mehr braucht der Einstieg in den Zweig nicht.</summary>
        private sealed class AssetContextStub : ISharedAssetContext
        {
            public bool HasAsset => true;
            public string AssetKey => "some-asset";
            public string AccessToken => null;
            public string Segment => "s";
            public AssetSegmentKind SegmentKind => AssetSegmentKind.StoredAsset;
            public string TicketTenant => null;
            public string TicketPayload => null;
            /// <summary>
            /// Die Freigabe selbst - ohne sie wirft der Weg, der sie aufsetzt. Rechte und Features duerfen
            /// leer sein: eine Vorlage, die nichts gewaehrt (offene Seite), ist der Normalfall.
            /// </summary>
            public AssetInfo CurrentAsset { get; } = new AssetInfo
            {
                AssetKey = "some-asset",
                UserScopeName = Tenant,
                Permissions = [],
                Features = []
            };
            public AssetInfo AuthenticationAsset => null;
            public AssetContext AssetContext => null;
            public AssetArgumentEnforcement Enforcement => AssetArgumentEnforcement.None;
            public bool Confirmed => false;
            public bool Denied => false;
            public bool MustHoldBack => false;
            public bool Require(string name, object value) => true;
            public bool Require(IDictionary<string, object> values) => true;
            public void ResetConfirmation() { }
        }

        /// <summary>
        /// Sagt nur, ob der angefragte Pfad zur Freigabe gehoert - und genau dieses Nein wurde bisher als
        /// "nicht angemeldet" weitergereicht.
        /// </summary>
        private sealed class AssetAdapterStub : ISharedAssetAdapter
        {
            private readonly bool matches;

            public AssetAdapterStub(bool matches) => this.matches = matches;

            public bool VerifyRequestLocation(string requestPath, string assetKey, string userScope,
                ClaimsPrincipal requestor) => matches;

            // Der Rest des Vertrags wird hier nicht befragt - er steht nur da, damit die Attrappe
            // vollstaendig ist. Wer etwas davon doch braucht, ersetzt das Nein durch eine Antwort.
            public AssetInfo GetAssetInfo(string assetKey, ClaimsPrincipal requestor, bool asOwner = false) => null;
            public AssetInfo GetTicketInfo(string tenantName, string payload, ClaimsPrincipal requestor,
                bool forAuthentication = false) => null;
            public AssetTemplateInfo[] GetEligibleShares(string requestPath) => [];
            public AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title) => null;
            public AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title,
                IDictionary<string, string> argumentValues, string recipientLabel, bool anonymous,
                IEnumerable<string> tenantFilters, IEnumerable<string> userFilters, out string error)
            {
                error = null;
                return null;
            }

            public string CreateAdHocTicket(string requestPath, AssetTemplateInfo template,
                IDictionary<string, string> argumentValues, string recipientLabel, TimeSpan? lifetime,
                string origin, out string error)
            {
                error = null;
                return null;
            }

            public bool UpdateSharedAsset(FullAssetInfo updateInfo) => false;
            public bool DeleteSharedAsset(FullAssetInfo assetInfo) => false;
            public SharedAssetListItem[] ListSharedAssets(string search, int skip, int take, out int total)
            {
                total = 0;
                return [];
            }

            public string CreateAnonymousLink(AssetInfo info, Microsoft.AspNetCore.Http.HttpContext context) => null;
            public string CreateAnonymousLink(AssetInfo info, string origin) => null;
            public string CreateLink(AssetInfo info, Microsoft.AspNetCore.Http.HttpContext context) => null;
            public string CreateLink(AssetInfo info, string origin) => null;
            public bool RotateAnonymousToken(string assetKey) => false;
            public bool RevokeTicket(string nonce, DateTime expiresUtc) => false;
            public bool VerifyAssetValidity(AssetInfo info) => true;
            public FullAssetInfo FindAnonymousAsset(string assetKey) => null;
            void ISharedAssetAdapter.SetImpersonationOff() { }
            void ISharedAssetAdapter.SetImpersonationOn() { }
        }

    }
}
