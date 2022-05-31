using System;
using System.Linq;
using System.Numerics;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Security.UserScopes
{
    /// <summary>
    /// 
    /// </summary>
    public class CookiePermissionScope:PermissionScopeBase
    {
        private readonly IHttpContextAccessor httpContext;
        private readonly IOptions<CookieScopeOptions> options;
        private readonly ILogger<CookiePermissionScope> logger;
        private string currentScope;

        public CookiePermissionScope(IHttpContextAccessor httpContext, IOptions<CookieScopeOptions> options, ILogger<CookiePermissionScope> logger)
        {
            this.httpContext = httpContext;
            this.options = options;
            this.logger = logger;
        }

        /// <summary>
        /// Sets the permissionScope to a new value
        /// </summary>
        /// <param name="newScope">the new scope to apply for the current user</param>
        protected override void SetPermissionScopePrefix(string newScope)
        {
            var opt = options.Value;
            httpContext.HttpContext.Response.Cookies.Append(opt.ScopeCookie, CreateScopeToken(newScope));
        }

        /// <summary>
        /// Enables a derived class to provide a permissionScopePrefix for the case that it is not being bypassed internally
        /// </summary>
        /// <returns>a string representing the current permission scope</returns>
        protected override string GetPermissionScopePrefix()
        {
            return currentScope ??= GetCurrentScope();
        }

        /// <summary>
        /// GEts the current Scope from the configured claims of the signed-in user
        /// </summary>
        /// <returns>teh current scope for the logged-in user</returns>
        private string GetCurrentScope()
        {
            var opt = options.Value;
            string retVal = null;
            var identities = httpContext.HttpContext?.User?.Identities?.ToArray();
            if (identities != null && identities.Any(n => n.IsAuthenticated))
            {
                if (httpContext.HttpContext.Request.Query.ContainsKey(Global.FixedAssetRequestQueryParameter))
                {
                    if (httpContext.HttpContext.User.HasClaim(c => c.Type == Global.FixedAssetUserScope))
                    {
                        var asset = httpContext.HttpContext.Request.Query[Global.FixedAssetRequestQueryParameter];
                        var fixedUserScope = httpContext.HttpContext.User.Claims
                            .First(n => n.Type == Global.FixedAssetUserScope).Value;
                        var assetProvider =
                            httpContext.HttpContext.RequestServices.GetService<ISharedAssetAdapter>();
                        if (assetProvider != null && assetProvider.VerifyRequestLocation(httpContext.HttpContext.Request.Path, asset, fixedUserScope, httpContext.HttpContext.User))
                        {
                            IsScopeExplicit = true;
                            return fixedUserScope;
                        }
                    }
                }

                var secc = httpContext.HttpContext.RequestServices.GetService<ISecurityRepository>();
                var userProvider = httpContext.HttpContext.RequestServices.GetRequiredService<IUserNameMapper>();
                var eligibles = secc.GetEligibleScopes(userProvider.GetUserLabels(httpContext.HttpContext.User),
                    httpContext.HttpContext.User.Identity.AuthenticationType).ToArray();
                logger.LogDebug($"Found: {identities.Length} identities");
                logger.LogDebug(string.Join(Environment.NewLine,
                    identities.Select(n => $"{n.Name}-> authenticated:{n.IsAuthenticated}({n.AuthenticationType})")));
                if (!string.IsNullOrEmpty(opt.RouteOverrideParam) &&
                    httpContext.HttpContext.Request.RouteValues.ContainsKey(opt.RouteOverrideParam) &&
                    !string.IsNullOrEmpty((string)httpContext.HttpContext.Request.RouteValues[opt.RouteOverrideParam]))
                {
                    var tmpRet = (string)httpContext.HttpContext.Request.RouteValues[opt.RouteOverrideParam];
                    if (eligibles.Any(n => n.ScopeName == tmpRet))
                    {
                        IsScopeExplicit = true;
                        return tmpRet;
                    }
                }

                logger.LogDebug($"Authenticated User(s): {string.Join(", ", identities.Select(n => n.Name))}");
                bool decryptFailed = false;
                bool invalidTenant = false;
                if (!httpContext.HttpContext.Request.Cookies.TryGetValue(opt.ScopeCookie, out retVal) ||
                    (decryptFailed = !TryReadScopeToken(ref retVal, out var renew)) || (invalidTenant = eligibles.All(n => n.ScopeName != retVal)) || renew)
                {
                    if (string.IsNullOrEmpty(retVal) || decryptFailed ||
                        invalidTenant)
                    {
                        retVal = opt.DefaultScopeExpression(httpContext.HttpContext, eligibles);
                        logger.LogDebug($"Default-Value of {opt.ScopeCookie}: {retVal}");
                    }

                    if (!string.IsNullOrEmpty(retVal))
                    {
                        ChangeScope(retVal);
                    }
                }

                logger.LogDebug($"Returning current permission scope: {retVal}");
            }

            return retVal;
        }

        private string CreateScopeToken(string currentScope)
        {
            var token = new UserScope {ScopeName = currentScope};
            return token.CompressToken(true, options.Value.EncryptCookie);
        }
        
        private bool TryReadScopeToken(ref string tokenString, out bool renewRequired)
        {
            var opt = options.Value;
            try
            {
                var token = tokenString.DecompressToken<UserScope>();
                renewRequired = DateTime.Now.Subtract(token.Created).TotalDays > opt.CookieRenewalDays;
                tokenString = token.ScopeName;
                return true;
            }
            catch(Exception ex)
            {
            }

            renewRequired = true;
            return false;
        }
    }
}
