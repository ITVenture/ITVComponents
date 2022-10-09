using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ApplicationToken;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.JWT.Impl
{
    internal class JwtTokenService: IJwtService
    {
        private readonly IContextUserProvider userProvider;
        private readonly ISecurityRepository securityRepo;
        private readonly IApplicationTokenService tokenService;
        private readonly IPermissionScope scopeProvider;
        private readonly IOptions<JwtGeneratorOptions> options;

        public JwtTokenService(IContextUserProvider userProvider, ISecurityRepository securityRepo, IApplicationTokenService tokenService, IPermissionScope scopeProvider, IOptions<JwtGeneratorOptions> options)
        {
            this.userProvider = userProvider;
            this.securityRepo = securityRepo;
            this.tokenService = tokenService;
            this.scopeProvider = scopeProvider;
            this.options = options;
        }

        public string GetJwtToken(string applicationKey)
        {
            return GetJwtTokenFor(userProvider.User, applicationKey);
        }

        public string GetRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token, out string applicationKey)
        {
            var opt = options.Value;
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = opt.ValidateAudience,
                ValidateIssuer = opt.ValidateIssuer,
                ValidateIssuerSigningKey = opt.ValidateIssuerSigningKey,
                ValidIssuer = opt.ValidIssuer,
                ValidAudience = opt.ValidAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.IssuerKey)),
                ValidateLifetime = false //here we are saying that we don't care about the token's expiration date
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            SecurityToken securityToken;
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
            var jwtSecurityToken = securityToken as JwtSecurityToken;
            if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");
            applicationKey = principal.Claims.First(n => n.Type == opt.ApplicationKeyClaim).Value;
            return principal;
        }

        public string GetJwtTokenFor(ClaimsPrincipal principal, string applicationKey)
        {
            var opt = options.Value;
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.IssuerKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            string userScope = null;
            if (principal.HasClaim(c => c.Type == ClaimTypes.FixedUserScope))
            {
                userScope = principal.Claims.First(n => n.Type == ClaimTypes.FixedUserScope).Value;
            }
            else
            {
                userScope = scopeProvider.PermissionPrefix;
            }

            var applicationUserLabel = tokenService.GetApplicationUserLabel(principal, applicationKey);
            using var ctx = ExpressionParser.BeginRepl(new Dictionary<string, object>
                {
                    {"GetClaim", (string name) => principal.Claims.FirstOrDefault(n => n.Type == name)},
                    {"GetClaims", (string name) => principal.Claims.Where(n => n.Type == name).ToList()},
                    {"GlobalClaimTypes", typeof(System.Security.Claims.ClaimTypes)},
                    {"ClaimTypes", typeof(ClaimTypes)},
                    {"ClaimValueTypes",typeof(ClaimValueTypes)}
                },
                (i) => DefaultCallbacks.PrepareDefaultCallbacks(i.Scope, i.ReplSession));
            var claims = (from t in opt.IncludedClaims
                select CreateClaim(t, ctx)).ToList();
            claims.Add(new Claim(ClaimTypes.FixedUserScope, userScope));
            claims.Add(new Claim(ClaimTypes.ClientAppId, applicationKey));
            claims.Add(new Claim(ClaimTypes.ClientAppUser, applicationUserLabel));
            var tok = new JwtSecurityToken(opt.Issuer, opt.Audience, claims, expires:
                DateTime.Now.AddMinutes(opt.TokenDuration),
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(tok);
        }

        /// <summary>
        /// Creates a new claim based on a configuration-claimdata model
        /// </summary>
        /// <param name="claimData">the configured claimdata model</param>
        /// <param name="ctx">the expression-parser repl context</param>
        /// <returns>the created claim object</returns>
        private Claim CreateClaim(ClaimData claimData, IDisposable ctx)
        {
            var type = WithValue(claimData.Type, ctx);
            var value = WithValue(claimData.Value, ctx);
            var valueType = WithValue(claimData.ValueType, ctx);
            var issuer = WithValue(claimData.Issuer, ctx);
            var originalIssuer = WithValue(claimData.OriginalIssuer, ctx);
            return new Claim(type, value, valueType, issuer, originalIssuer);
        }

        /// <summary>
        /// Parses the configured expression or returns the literal value configured on the claim
        /// </summary>
        /// <param name="value">the raw-value</param>
        /// <param name="ctx">the current scripting context</param>
        /// <returns>the parsed value or the literal, when its a raw-value</returns>
        private string WithValue(string value, IDisposable ctx)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("##"))
            {
                return value;
            }

            var exp = value.Substring(2);
            return (string)ExpressionParser.Parse(exp, ctx);
        }
    }
}
