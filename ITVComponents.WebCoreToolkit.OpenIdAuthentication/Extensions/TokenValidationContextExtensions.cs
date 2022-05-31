using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Extensions
{
    public static class TokenValidationContextExtensions
    {
        public static void TokenToClaims(this TokenValidatedContext context)
        {
            var tok = new JwtSecurityToken(context.TokenEndpointResponse.AccessToken);
            // store both access and refresh token in the claims - hence in the cookie
            var identity = (ClaimsIdentity)context.Principal.Identity;
            foreach (var t in tok.Payload)
            {
                identity.AddClaim(new Claim(t.Key, t.Value?.ToString()));
            }

            /*identity.AddClaims(new[]
            {
                new Claim("access_token", context.TokenEndpointResponse.AccessToken),
                new Claim("refresh_token", context.TokenEndpointResponse.RefreshToken)
            });*/

            // so that we don't issue a session cookie but one with a fixed expiration
            context.Properties.IsPersistent = true;

            // align expiration of the cookie with expiration of the
            // access token
            var accessToken = new JwtSecurityToken(context.TokenEndpointResponse.AccessToken);
            var l = context.Properties.GetTokens().ToList();
            l.Add(new AuthenticationToken
            {
                Name = "access_token",
                Value = context.TokenEndpointResponse.AccessToken
            });
            l.Add(new AuthenticationToken
            {
                Name = "refresh_token",
                Value = context.TokenEndpointResponse.RefreshToken
            });
            l.Add(new AuthenticationToken { Name = "ExpiresAt", Value = accessToken.ValidTo.ToString("O") });
            context.Options.SaveTokens = true;
            //context.Properties.ExpiresUtc = accessToken.ValidTo;
            context.Properties.ExpiresUtc = DateTime.Now.AddHours(2);
        }
    }
}
