using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess;
using ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ClaimsTransformation;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
// Nur fuer IOptionsMonitor: der Typ Options kollidiert hier mit dem Namespace
// ITVComponents.WebCoreToolkit.Options, deshalb ist Options.Create unten ausgeschrieben.
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Der Anmeldeweg eines Ad-hoc-Tickets.
    /// </summary>
    /// <remarks>
    /// Ein Ticket steht NIRGENDS - es hat weder AssetKey noch Zugangs-Token, die sich abfragen liessen.
    /// Genau daran scheiterte es (BUG-PRE230): der Handler fragte
    /// <c>IGetAnonymousAssetQuery.Execute(null, null)</c>, bekam nichts und kein "denied" zurueck, und
    /// meldete <c>NoResult</c> - lautlos. Jeder anonyme Ticket-Link endete im 404, waehrend
    /// <c>GetTicketInfo</c> am Ergebnis <c>IsAnonymous = true</c> zusagte.
    /// <para>
    /// Diese Tests halten den Weg fest, nicht bloss das Ergebnis: sie pruefen mit, dass die Abfrage nach
    /// gespeicherten Freigaben fuer ein Ticket gar nicht erst gestellt wird.
    /// </para>
    /// </remarks>
    [TestClass]
    public class AdHocTicketAuthenticationTests
    {
        private const string Payload = "encrypted-payload";

        [TestMethod]
        public async Task A_Valid_Ticket_Establishes_A_Principal()
        {
            var query = new StubQuery();
            var result = await AuthenticateTicket(TicketInfo(), query);

            Assert.IsTrue(result.Succeeded, "an anonymous ad-hoc ticket must let its holder in");
            Assert.IsFalse(query.WasAsked,
                "a ticket is nowhere on file - asking the stored-asset query for it is the bug itself");
        }

        /// <summary>
        /// Der Name entscheidet an drei Stellen darueber, ob ein Besucher als angemeldet gilt
        /// (<c>KnownVisitor</c>, <c>SharedAssetContext</c>, <c>AssetAccessRecorder</c>). Die Nonce des
        /// Tickets an dieser Stelle liesse den zweiten Durchlauf den eigenen Besucher fuer einen
        /// Angemeldeten halten - und sie stuende als Benutzername im Zugriffsprotokoll.
        /// </summary>
        [TestMethod]
        public async Task The_Visitor_Is_The_Anonymous_One_And_Not_The_Ticket_Nonce()
        {
            var result = await AuthenticateTicket(TicketInfo(), new StubQuery());

            Assert.AreEqual(Global.AnonymousAssetUserName, result.Principal?.Identity?.Name);
        }

        [TestMethod]
        public async Task A_Ticket_That_Does_Not_Resolve_Is_Refused_Not_Ignored()
        {
            // null heisst: GetTicketInfo hat abgelehnt - abgelaufen, widerrufen, fremder Mandant, Vorlage
            // weg. NoResult waere hier falsch: es hiesse "dieses Schema ist nicht zustaendig", und der
            // Aufrufer suchte den Fehler woanders. Genau das hat den Bug so teuer gemacht.
            var result = await AuthenticateTicket(null, new StubQuery());

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.None, "an invalid ticket is a decision, not a shrug");
        }

        /// <summary>
        /// Der Gegenfall: eine gespeicherte Freigabe ohne Zugangs-Token ist ein Link fuer angemeldete
        /// Empfaenger - dort ist <c>NoResult</c> richtig, und das darf der Fix nicht mitnehmen.
        /// </summary>
        [TestMethod]
        public async Task A_Stored_Link_Without_A_Token_Is_Still_Not_This_Schemes_Business()
        {
            var handler = await Handler(StoredContext(), new StubQuery());
            var result = await handler.AuthenticateAsync();

            Assert.IsTrue(result.None);
        }

        /// <summary>
        /// Die zweite Haelfte desselben Fehlers, eine Schicht spaeter: die Claims-Transformation holte die
        /// Rechte ueber <c>GetAssetInfo(AssetKey)</c> - und ein Ticket hat keinen AssetKey. Der Besucher
        /// kaeme damit zwar herein, aber ohne jedes Recht, und der Link scheiterte am Berechtigungsriegel
        /// statt am 404.
        /// </summary>
        [TestMethod]
        public async Task A_Ticket_Grants_The_Rights_Of_Its_Template()
        {
            var info = TicketInfo();
            info.Permissions = new[] { "Checkout.Use" };
            info.Features = new[] { "MiniStore.Core" };

            var adapter = new StubAdapter(info);
            var (context, _) = (NewContext(TicketHttpContext(), adapter), (HttpContext)null);
            var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
            var transformation = new AssetDrivenClaimsTransformation(context,
                new StubScopeFactory(new StubServices(adapter)),
                NullLogger<AssetDrivenClaimsTransformation>.Instance);

            var transformed = await transformation.TransformAsync(principal);

            // Auch die Claims-Transformation laeuft je Anfrage - sie muss dieselbe Frage stellen wie die
            // Anmeldung, sonst faellt jede Unterressource durch die volle Pruefung.
            Assert.AreEqual(true, adapter.AskedForAuthentication);

            Assert.IsTrue(transformed.HasClaim(WebCoreToolkit.ClaimTypes.FixedAssetPermission, "Checkout.Use"));
            Assert.IsTrue(transformed.HasClaim(WebCoreToolkit.ClaimTypes.FixedAssetFeature, "MiniStore.Core"));
            Assert.IsTrue(transformed.HasClaim(WebCoreToolkit.ClaimTypes.FixedUserScope, "TenantA"),
                "without the scope claim the visitor is in no tenant at all");
        }

        /// <summary>
        /// BUG-PRE231: die Anmeldung lief ueber CurrentAsset und damit durch die VOLLE Pruefung - Ort
        /// gegen den laufenden Pfad und Gueltigkeitsregel. Beides kann dort nicht stehen: der Pfad ist
        /// noch nicht kanonisch, ein Geltungsbereich existiert nicht, und die Anmeldung laeuft je
        /// Anfrage statt je Vorgang.
        /// </summary>
        [TestMethod]
        public async Task Authentication_Asks_The_Authentication_Question_Not_The_Guard_One()
        {
            var adapter = new StubAdapter(TicketInfo());
            var http = TicketHttpContext();
            var handler = await Handler((NewContext(http, adapter), http), new StubQuery());

            var result = await handler.AuthenticateAsync();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(true, adapter.AskedForAuthentication,
                "the login path must not run the checks that presuppose a running operation");
        }

        /// <summary>
        /// Der Gegenpol: der Riegel fragt weiterhin vollstaendig. Sonst waere die Ortspruefung gegen den
        /// laufenden Pfad ersatzlos weg.
        /// </summary>
        [TestMethod]
        public void The_Guard_Still_Asks_The_Full_Question()
        {
            var adapter = new StubAdapter(TicketInfo());
            var context = NewContext(TicketHttpContext(), adapter);

            Assert.IsNotNull(context.CurrentAsset);
            Assert.AreEqual(false, adapter.AskedForAuthentication);
        }

        /// <summary>
        /// Eine Unterressource der Seite - blazor.web.js, CSS, ein Bild - traegt denselben Abschnitt und
        /// muss sich genauso anmelden. Genau daran starb die Seite: gegen das Pfadmuster DER SEITE
        /// geprueft fielen alle durch, und uebrig blieb eine weisse Seite ohne Fehlermeldung.
        /// </summary>
        [TestMethod]
        public async Task A_Sub_Resource_Of_The_Page_Authenticates_Too()
        {
            var adapter = new StubAdapter(TicketInfo());
            var http = TicketHttpContext();
            http.Request.Path = "/ADM/_framework/blazor.web.js";
            var handler = await Handler((NewContext(http, adapter), http), new StubQuery());

            var result = await handler.AuthenticateAsync();

            Assert.IsTrue(result.Succeeded, "without its scripts the page stays white");
        }

        private static AssetInfo TicketInfo() => new()
        {
            AssetKey = null,
            TicketNonce = "0123456789abcdef",
            UserScopeName = "TenantA",
            TemplateSystemKey = "pos-checkout",
            IsAnonymous = true
        };

        private static async Task<AuthenticateResult> AuthenticateTicket(AssetInfo ticket, StubQuery query)
        {
            var handler = await Handler(TicketContext(ticket), query);
            return await handler.AuthenticateAsync();
        }

        private static async Task<AnonymousAssetAuthenticationHandler> Handler(
            (ISharedAssetContext Context, HttpContext Http) setup, StubQuery query)
        {
            var options = new AnonymousAssetAuthenticationOptions();
            var handler = new AnonymousAssetAuthenticationHandler(
                new StubOptionsMonitor(options), NullLoggerFactory.Instance, UrlEncoder.Default,
                new StubClock(), query, setup.Context);
            await handler.InitializeAsync(
                new AuthenticationScheme(AnonymousAssetAuthenticationOptions.DefaultScheme, null,
                    typeof(AnonymousAssetAuthenticationHandler)),
                setup.Http);
            return handler;
        }

        private static (ISharedAssetContext, HttpContext) TicketContext(AssetInfo ticket)
        {
            var http = TicketHttpContext();
            return (NewContext(http, new StubAdapter(ticket)), http);
        }

        private static DefaultHttpContext TicketHttpContext()
        {
            var http = new DefaultHttpContext();
            http.Items[Global.SharedAssetTicketPayloadItemKey] = Payload;
            http.Items[Global.SharedAssetTicketTenantItemKey] = "TenantA";
            http.Items[Global.SharedAssetSegmentItemKey] =
                SharedAssetPath.BuildTicketSegment("TenantA", Payload);
            http.Request.Path = "/ADM/checkout/12";
            return http;
        }

        private static (ISharedAssetContext, HttpContext) StoredContext()
        {
            var http = new DefaultHttpContext();
            http.Items[Global.SharedAssetKeyItemKey] = "abc";
            http.Items[Global.SharedAssetSegmentItemKey] = SharedAssetPath.BuildSegment("abc");
            return (NewContext(http, new StubAdapter(null)), http);
        }

        private static ISharedAssetContext NewContext(HttpContext http, StubAdapter adapter)
        {
            var services = new StubServices(adapter);
            return new SharedAssetContext(new HttpContextAccessor { HttpContext = http },
                new StubContextUser(services),
                Microsoft.Extensions.Options.Options.Create(new SharedAssetPathOptions()), services,
                NullLogger<SharedAssetContext>.Instance);
        }

        private sealed class StubScopeFactory : IServiceScopeFactory, IServiceScope
        {
            public StubScopeFactory(IServiceProvider services) => ServiceProvider = services;

            public IServiceProvider ServiceProvider { get; }

            public IServiceScope CreateScope() => this;

            public void Dispose() { }
        }

        private sealed class StubQuery : IGetAnonymousAssetQuery
        {
            public bool WasAsked { get; private set; }

            public AnonymousAsset Execute(string assetKey, string accessToken, out bool denied)
            {
                WasAsked = true;
                denied = false;
                return null;
            }
        }

        private sealed class StubOptionsMonitor : IOptionsMonitor<AnonymousAssetAuthenticationOptions>
        {
            private readonly AnonymousAssetAuthenticationOptions options;

            public StubOptionsMonitor(AnonymousAssetAuthenticationOptions options) => this.options = options;

            public AnonymousAssetAuthenticationOptions CurrentValue => options;

            public AnonymousAssetAuthenticationOptions Get(string name) => options;

            public IDisposable OnChange(Action<AnonymousAssetAuthenticationOptions, string> listener) => null;
        }

        private sealed class StubClock : ISystemClock
        {
            public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        }

        private sealed class StubContextUser : IContextUserProvider
        {
            public StubContextUser(IServiceProvider services) => Services = services;

            public ClaimsPrincipal User { get; } = new(new ClaimsIdentity());
            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();
            public string RequestPath => "/checkout/12";
            public IServiceProvider Services { get; }
        }

        private sealed class StubServices : IServiceProvider
        {
            private readonly ISharedAssetAdapter adapter;

            public StubServices(ISharedAssetAdapter adapter) => this.adapter = adapter;

            public object GetService(Type serviceType)
                => serviceType == typeof(ISharedAssetAdapter) ? adapter : null;
        }

        /// <summary>
        /// Beantwortet genau die eine Frage, um die es hier geht, und stellt sicher, dass sie mit den
        /// Werten AUS DEM ABSCHNITT gestellt wird.
        /// </summary>
        private sealed class StubAdapter : ISharedAssetAdapter
        {
            private readonly AssetInfo ticket;

            public StubAdapter(AssetInfo ticket) => this.ticket = ticket;

            /// <summary>Wie zuletzt gefragt wurde - Anmeldung oder Riegel.</summary>
            public bool? AskedForAuthentication { get; private set; }

            public AssetInfo GetTicketInfo(string tenantName, string payload, ClaimsPrincipal requestor,
                bool forAuthentication = false)
            {
                Assert.AreEqual("TenantA", tenantName);
                Assert.AreEqual(Payload, payload);
                AskedForAuthentication = forAuthentication;
                return ticket;
            }

            public AssetInfo GetAssetInfo(string assetKey, ClaimsPrincipal requestor, bool asOwner = false) => null;
            public bool VerifyRequestLocation(string requestPath, string assetKey, string userScope, ClaimsPrincipal requestor) => true;
            public AssetTemplateInfo[] GetEligibleShares(string requestPath) => Array.Empty<AssetTemplateInfo>();
            public AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title) => null;

            public AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title,
                IDictionary<string, string> argumentValues, string recipientLabel, bool anonymous,
                IEnumerable<string> userFilters, IEnumerable<string> tenantFilters, out string error)
            {
                error = null;
                return null;
            }

            public bool UpdateSharedAsset(FullAssetInfo updateInfo) => false;
            public bool DeleteSharedAsset(FullAssetInfo assetInfo) => false;
            public string CreateAnonymousLink(AssetInfo info, HttpContext context) => string.Empty;
            public string CreateLink(AssetInfo info, HttpContext context) => string.Empty;
            public string CreateLink(AssetInfo info, string origin) => string.Empty;
            public string CreateAnonymousLink(AssetInfo info, string origin) => string.Empty;

            public SharedAssetListItem[] ListSharedAssets(string search, int skip, int take, out int total)
            {
                total = 0;
                return Array.Empty<SharedAssetListItem>();
            }

            public bool RotateAnonymousToken(string assetKey) => false;

            public string CreateAdHocTicket(string requestPath, AssetTemplateInfo template,
                IDictionary<string, string> argumentValues, string recipientLabel, TimeSpan? lifetime,
                string origin, out string error)
            {
                error = null;
                return null;
            }

            public bool RevokeTicket(string nonce, DateTime expiresUtc) => false;
            public FullAssetInfo FindAnonymousAsset(string assetKey) => null;
            void ISharedAssetAdapter.SetImpersonationOff() { }
            void ISharedAssetAdapter.SetImpersonationOn() { }
        }
    }
}
