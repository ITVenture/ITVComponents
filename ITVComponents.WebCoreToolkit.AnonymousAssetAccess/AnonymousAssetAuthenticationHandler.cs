using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using ITVComponents.WebCoreToolkit.AnonymousAssetAccess.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.AnonymousAssetAccess
{
    public class AnonymousAssetAuthenticationHandler : AuthenticationHandler<AnonymousAssetAuthenticationOptions>
    {
        private const string ProblemDetailsContentType = "application/problem+json";
        private readonly IGetAnonymousAssetQuery getAnonymousAssetQuery;
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
            IGetAnonymousAssetQuery getApiKeyQuery) : base(options, logger, encoder, clock)
        {
            this.getAnonymousAssetQuery = getApiKeyQuery ?? throw new ArgumentNullException(nameof(getApiKeyQuery));
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var existingAsset = getAnonymousAssetQuery.Execute(Request.Query, out bool denied);
            if (existingAsset == null && !denied)
            {
                var qd = Request.GetRefererQuery();
                if (qd != null)
                {
                    existingAsset = getAnonymousAssetQuery.Execute(qd, out denied);
                }


                if (existingAsset == null && !denied)
                {
                    return AuthenticateResult.NoResult();
                }
            }

            if (existingAsset != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, existingAsset.Key)
                };

                var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
                var identities = new List<ClaimsIdentity> {identity};
                var principal = new ClaimsPrincipal(identities);
                var ticket = new AuthenticationTicket(principal, Options.Scheme);
                ticket.Properties.SetString("##ANONYMOUS_ASSET", "true");
                return AuthenticateResult.Success(ticket);
            }

            return AuthenticateResult.Fail("Invalid Asset Access-Token provided.");
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
