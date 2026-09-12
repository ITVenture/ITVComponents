using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.Web;
using ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess
{
    public class AnonymousAssetAuthenticationHandler : AuthenticationHandler<AnonymousAssetAuthenticationOptions>
    {
        private const string ProblemDetailsContentType = "application/problem+json";
        private readonly IGetAnonymousAssetQuery getAnonymousAssetQuery;
        private readonly ISharedAssetContext assetContext;

        /// <summary>
        /// Damit die Warnung aus <see cref="WarnAboutPipelineOrder"/> nicht bei jeder Anfrage im Log steht:
        /// die Ursache ist eine Startup-Konfiguration, sie aendert sich zur Laufzeit nicht.
        /// </summary>
        private static int pipelineOrderWarned;
        private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IgnoreNullValues = true
        };

        public AnonymousAssetAuthenticationHandler(
            IOptionsMonitor<AnonymousAssetAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IGetAnonymousAssetQuery getApiKeyQuery,
            ISharedAssetContext assetContext) : base(options, logger, encoder, clock)
        {
            this.getAnonymousAssetQuery = getApiKeyQuery ?? throw new ArgumentNullException(nameof(getApiKeyQuery));
            this.assetContext = assetContext ?? throw new ArgumentNullException(nameof(assetContext));
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!assetContext.HasAsset)
            {
                WarnAboutPipelineOrder();
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (KnownVisitor() is { } visitor)
            {
                // Eine Freigabe verleiht Rechte - sie nimmt keine Identitaet weg. Dass ein Link ohne
                // Anmeldung benutzt werden DARF, heisst nicht, dass es egal ist, wer ihn benutzt: wer
                // angemeldet ist, bleibt er selbst, sonst steht im Protokoll "#ANONYMOUS#", obwohl der
                // Server genau weiss, wer da war.
                //
                // An den Rechten aendert das nichts. Sie kommen aus AssetDrivenClaimsTransformation, und
                // ein Benutzerfilter ##ANONYMOUS passt dort auf JEDEN angemeldeten Aufrufer - die
                // Freigabe bleibt also auch fuer ihn das Gesetz. Wuerden wir hier stattdessen eine
                // zweite Identitaet ausstellen, entschiede die Reihenfolge der Anmeldeschemata in der
                // Policy, welcher der beiden Namen vorne steht: die haengt an der WebPart-Registrierung
                // und ist damit nicht einmal verlaesslich dieselbe.
                Logger.LogDebug(
                    "An anonymous shared-asset link was opened by the signed-in user '{User}'; their identity is kept. The rights of the asset apply unchanged.",
                    visitor);
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (assetContext.SegmentKind == AssetSegmentKind.Ticket)
            {
                // Ein Ticket steht NIRGENDS - es gibt weder AssetKey noch Zugangs-Token, die man abfragen
                // koennte. Sein Geheimnis ist die verschluesselte Nutzlast selbst, und die loest
                // GetTicketInfo auf. Ohne diesen Zweig liefe die Abfrage unten mit zwei null-Argumenten,
                // faende nichts, meldete kein "denied" - und der Link endete lautlos im 404.
                return Task.FromResult(AuthenticateTicket());
            }

            var existingAsset = getAnonymousAssetQuery.Execute(assetContext.AssetKey, assetContext.AccessToken,
                out bool denied);
            if (existingAsset == null && !denied)
            {
                // Ein Asset ohne Zugangs-Token ist ein Link fuer angemeldete Empfaenger - dieses Schema ist
                // dafuer nicht zustaendig, die Claims-Transformation uebernimmt.
                Logger.LogDebug(
                    "The shared-asset segment of {Path} carries no access token; this is a link for signed-in recipients and the claims transformation takes over.",
                    Context.Request.Path.Value);
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (existingAsset != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(System.Security.Claims.ClaimTypes.Name, existingAsset.Key)
                };

                var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
                var identities = new List<ClaimsIdentity> {identity};
                var principal = new ClaimsPrincipal(identities);
                var ticket = new AuthenticationTicket(principal, Options.Scheme);
                ticket.Properties.SetString("##ANONYMOUS_ASSET", "true");
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            return Task.FromResult(AuthenticateResult.Fail("Invalid Asset Access-Token provided."));
        }

        /// <summary>
        /// Stellt den Prinzipal eines Ad-hoc-Tickets aus.
        /// </summary>
        /// <remarks>
        /// <see cref="ISharedAssetContext.AuthenticationAsset"/> laeuft fuer einen Ticket-Abschnitt ueber
        /// <c>GetTicketInfo</c>: dort haengen Mandantenbindung, Frist, Widerruf, Vorlage und die Bindung an
        /// den <c>RootPath</c> des Tickets, und jeder Fehlschlag ist dort bereits mit seinem Grund
        /// protokolliert. Hier bleibt nur die Entscheidung.
        /// <para>
        /// <b>Der Name ist <see cref="Global.AnonymousAssetUserName"/>, nicht die Nonce des Tickets.</b>
        /// Drei Stellen unterscheiden den anonymen Besucher genau an diesem Namen von einem echten
        /// Benutzer (<see cref="KnownVisitor"/>, <c>SharedAssetContext</c>, <c>AssetAccessRecorder</c>);
        /// eine Nonce an dieser Stelle liesse den zweiten Durchlauf den eigenen Besucher fuer einen
        /// Angemeldeten halten und schriebe sie als Benutzernamen ins Protokoll. Die Nonce steht ohnehin
        /// schon im Protokoll - <c>AssetAccessEntry.TicketNonce</c> traegt sie.
        /// </para>
        /// </remarks>
        /// <returns>der ausgestellte Prinzipal, oder eine Ablehnung</returns>
        private AuthenticateResult AuthenticateTicket()
        {
            // AuthenticationAsset und nicht CurrentAsset: das hier laeuft je ANFRAGE, also auch fuer
            // blazor.web.js, CSS und Bilder. CurrentAsset pruefte sie alle gegen das Pfadmuster DER SEITE,
            // und uebrig bliebe eine angemeldete Seite ohne ihre Bestandteile - eine weisse Seite.
            var info = assetContext.AuthenticationAsset;
            if (info == null)
            {
                // Kein zweiter Logeintrag: GetTicketInfo hat den Grund schon benannt, und zwar genauer,
                // als es hier moeglich waere.
                return AuthenticateResult.Fail("The ad-hoc ticket is not valid.");
            }

            var identity = new ClaimsIdentity(
                new[] { new Claim(System.Security.Claims.ClaimTypes.Name, Global.AnonymousAssetUserName) },
                Options.AuthenticationType);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Options.Scheme);
            ticket.Properties.SetString("##ANONYMOUS_ASSET", "true");
            Logger.LogDebug("An ad-hoc ticket was accepted for tenant '{Tenant}' on {Path}.",
                info.UserScopeName, Context.Request.Path.Value);
            return AuthenticateResult.Success(ticket);
        }

        /// <summary>
        /// Der Name des angemeldeten Besuchers, oder null, wenn wirklich niemand dahintersteht.
        /// <para>
        /// Gefragt wird ueber alle Identitaeten, nicht nur ueber die vorderste - und der anonyme
        /// Freigabe-Besucher zaehlt nicht als Benutzer, damit ein zweiter Durchlauf dieses Handlers
        /// nicht seine eigene Ausgabe fuer einen Angemeldeten haelt.
        /// </para>
        /// </summary>
        private string KnownVisitor()
            => Context.User?.Identities.FirstOrDefault(n => n.IsAuthenticated
                                                            && !string.IsNullOrEmpty(n.Name)
                                                            && !string.Equals(n.Name, Global.AnonymousAssetUserName,
                                                                StringComparison.OrdinalIgnoreCase))?.Name;

        /// <summary>
        /// Erkennt den einen Verdrahtungsfehler, der sich sonst als "der Link tut einfach nichts" aeussert:
        /// die Anfrage traegt noch einen unbearbeiteten Asset-Abschnitt im Pfad, also wurde
        /// <c>UseSharedAssetPath()</c> gar nicht oder zu spaet registriert. Einmal je Prozess, weil sich die
        /// Ursache zur Laufzeit nicht aendert.
        /// </summary>
        private void WarnAboutPipelineOrder()
        {
            if (!SharedAssetPathMiddleware.HasUnprocessedSegment(Context)
                || Interlocked.Exchange(ref pipelineOrderWarned, 1) != 0)
            {
                return;
            }

            Logger.LogError(
                "A request carries an unprocessed shared-asset segment ({Path}). UseSharedAssetPath() is either missing or registered too late: it must run BEFORE UseStaticFiles, UseAuthentication, UseRouting and UseTenantPathPrefix. Anonymous asset links will not work until that is fixed.",
                Context.Request.Path.Value);
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            if (properties.Items.ContainsKey("##ANONYMOUS_ASSET"))
            {
                Response.StatusCode = 401;
                Response.ContentType = ProblemDetailsContentType;
                var problemDetails = new UnauthorizedProblemDetails();
                await Response.WriteAsync(JsonSerializer.Serialize(problemDetails, jsonSerializerOptions));
            }
        }

        protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            if (properties.Items.ContainsKey("##ANONYMOUS_ASSET"))
            {
                Response.StatusCode = 403;
                Response.ContentType = ProblemDetailsContentType;
                var problemDetails = new ForbiddenProblemDetails();
                await Response.WriteAsync(JsonSerializer.Serialize(problemDetails, jsonSerializerOptions));
            }
        }
    }
}
