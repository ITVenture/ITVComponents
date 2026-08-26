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

            var existingAsset = getAnonymousAssetQuery.Execute(assetContext.AssetKey, assetContext.AccessToken,
                out bool denied);
            if (existingAsset == null && !denied)
            {
                // Ein Asset ohne Zugangs-Token ist ein Link fuer angemeldete Empfaenger - dieses Schema ist
                // dafuer nicht zustaendig, die Claims-Transformation uebernimmt.
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
