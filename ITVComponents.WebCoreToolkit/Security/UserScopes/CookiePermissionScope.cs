using System;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Numerics;
using Antlr4.Runtime;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Cookies;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.Comparers;
using ITVComponents.WebCoreToolkit.Security.AssetLevelImpersonation;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels;
using ITVComponents.WebCoreToolkit.Security.UserScopes.Helpers;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Permission = ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels.Permission;
using ScopeInfo = ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels.ScopeInfo;

namespace ITVComponents.WebCoreToolkit.Security.UserScopes
{
    /// <summary>
    /// 
    /// </summary>
    public class CookiePermissionScope:PermissionScopeBase
    {
        private readonly IHttpContextAccessor httpContext;
        private readonly ICookieService cookieHandler;
        private readonly IOptions<CookieScopeOptions> options;
        private readonly ILogger<CookiePermissionScope> logger;
        private string currentScope;
        private bool preventEndlessLoop = false;

        public CookiePermissionScope(IHttpContextAccessor httpContext, ICookieService cookieHandler, IOptions<CookieScopeOptions> options, ILogger<CookiePermissionScope> logger)
        {
            this.httpContext = httpContext;
            this.cookieHandler = cookieHandler;
            this.options = options;
            this.logger = logger;
        }

        /// <summary>
        /// Sets the permissionScope to a new value
        /// </summary>
        /// <param name="newScope">the new scope to apply for the current user</param>
        protected override void SetPermissionScopePrefix(string newScope)
        {
            var token = ReadScopeToken(out var opt, out var createdNew, out var secc);
            UpdateToken(newScope, token, secc, createdNew, true, false);
        }

        /// <summary>
        /// Enables a derived class to provide a permissionScopePrefix for the case that it is not being bypassed internally
        /// </summary>
        /// <returns>a string representing the current permission scope</returns>
        protected override string GetPermissionScopePrefix()
        {
            if (ImpersonationExplicitDeactivated)
            {
                return GetCurrentScope();
            }
            return currentScope ??= GetCurrentScope();
        }

        /// <summary>
        /// GEts the current Scope from the configured claims of the signed-in user
        /// </summary>
        /// <returns>teh current scope for the logged-in user</returns>
        private string GetCurrentScope()
        {
            if (preventEndlessLoop)
            {
                return currentScope;
            }

            preventEndlessLoop = true;
            try
            {
                var identities = httpContext.HttpContext?.User?.Identities?.ToArray();
                if (identities != null && identities.Any(n => n.IsAuthenticated))
                {
                    if (httpContext.HttpContext.User.HasClaim(c => c.Type == ClaimTypes.FixedUserScope))
                    {
                        var fixedUserScope = httpContext.HttpContext.User.Claims
                            .First(n => n.Type == ClaimTypes.FixedUserScope).Value;
                        IsScopeExplicit = true;
                        return fixedUserScope;
                    }
                }

                var scopeToken = ReadScopeToken(out var opt, out var isNew, out var secc);
                string retVal = scopeToken.ScopeName;
                if (scopeToken.UserLabels.Length == 0)
                {
                    return null;
                }

                if (!string.IsNullOrEmpty(opt.RouteOverrideParam) &&
                    httpContext.HttpContext.Request.RouteValues.ContainsKey(opt.RouteOverrideParam) &&
                    !string.IsNullOrEmpty((string)httpContext.HttpContext.Request.RouteValues[opt.RouteOverrideParam]))
                {


                    var tmpRet = (string)httpContext.HttpContext.Request.RouteValues[opt.RouteOverrideParam];
                    if (scopeToken.EligibleScopes.Any(n =>
                            n.ScopeName.Equals(tmpRet, StringComparison.OrdinalIgnoreCase)))
                    {
                        UpdateToken(tmpRet, scopeToken, secc, isNew, false, true);
                        IsScopeExplicit = true;
                        return tmpRet;
                    }
                }

                IsScopeExplicit = false;
                var invalidTenant = scopeToken.EligibleScopes.All(n => n.ScopeName != retVal);
                if (string.IsNullOrEmpty(retVal) ||
                    invalidTenant)
                {
                    retVal = opt.DefaultScopeExpression(httpContext.HttpContext, scopeToken.EligibleScopes);
                    logger.LogDebug($"Default-Value of {opt.ScopeCookie}: {retVal}");
                }

                UpdateToken(retVal, scopeToken, secc, isNew, true, true);
                return retVal;
            }
            finally
            {
                preventEndlessLoop = false;
            }
        }

        private void UpdateToken(string scope, UserScope scopeToken, ISecurityRepository secc, bool forceRefresh, bool setAsDefault, bool pushRepo)
        {
            bool setRefreshed = false;
            var perms = scopeToken.GetPermissionsOf(scope);
            if (perms == null || forceRefresh)
            {
                perms = (from t in scopeToken.UserLabels
                        select secc.GetPermissions(t.UserLabels, scope, t.AuthenticationType))
                    .SelectMany(n => n.Select(n => n.PermissionName)).Distinct().ToArray();
                scopeToken.UpdateScopePermissions(scope, secc.GetKnownPermissions(scope), perms);
                setRefreshed = true;
            }

            var features = scopeToken.GetFeaturesOf(scope);
            if (features == null || forceRefresh)
            {
                var tmp = secc.GetFeatures(scope).ToArray();
                features = scopeToken.UpdateScopeFeatures(scope, tmp);
                setRefreshed = true;
            }

            if (setRefreshed)
            {
                scopeToken.SetScopeRefreshed(scope);
            }

            if (pushRepo && secc is SecurityRepository { Current: not CookiePermissionRepo } wrap)
            {
                wrap.PushRepo(new CookiePermissionRepo(scopeToken.UserLabels, scopeToken.EligibleScopes, scope, perms, scopeToken.KnownPermissions, features, wrap.Current));
            }

            if (setAsDefault)
            {
                setRefreshed |= scopeToken.ScopeName != scope;
                scopeToken.ScopeName = scope;
            }

            if (setRefreshed)
            {
                var opt = options.Value;
                cookieHandler.SetCookie(opt.ScopeCookie,
                    scopeToken.CompressToken(true, options.Value.EncryptCookie, useHighCompression: true),
                    new CookieOptions
                    {
                        Expires = new DateTimeOffset(DateTime.Now.AddMinutes(opt.CoookieRenewalMinutes * 3))
                    });
            }
        }

        private UserScope ReadScopeToken(out CookieScopeOptions opt, out bool createdNew, out ISecurityRepository secc)
        {
            createdNew = false;
            opt = options.Value;
            bool noCookieFound = !cookieHandler.TryGetCookie(opt.ScopeCookie, out var defaultScope);
            UserScope scopeToken = null;
            if (!noCookieFound && !string.IsNullOrEmpty(defaultScope))
            {
                try
                {
                    scopeToken = defaultScope.DecompressToken<UserScope>();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Failed to unpack UserScope: {ex.Message}", LogSeverity.Error);
                }
            }

            if (scopeToken == null || DateTime.Now.Subtract(scopeToken.Created).TotalMinutes > opt.CoookieRenewalMinutes)
            {
                scopeToken = new UserScope{ScopeName = scopeToken?.ScopeName};
                createdNew = true;

            }

            ScopeInfo[] eligibles = null;
            secc = null;
            var lbl = GetUserLabels();
            if (!createdNew && scopeToken.UserLabels != null && UserValidateHelper.IsUserOk(scopeToken.UserLabels, lbl))
            {
                eligibles = scopeToken.EligibleScopes;
                secc = GetSecurityRepo();
            }

            if (eligibles == null)
            {
                scopeToken.UserLabels = lbl;
                eligibles = GetEligibleScopes(out secc, lbl);
                scopeToken.EligibleScopes = eligibles;
            }
            return scopeToken;
        }

        private ScopeInfo[] GetEligibleScopes(out ISecurityRepository securityRepo, AuthTypeUserLabels[] userLabels)
        {

            var scc = securityRepo = GetSecurityRepo();
            return (from t in userLabels
                    select
                        scc.GetEligibleScopes(t.UserLabels, t.AuthenticationType))
                .SelectMany(n => n).Distinct(new ScopeInfoComparer())
                .Select(n => new ScopeInfo
                {
                    AccessMode = n.AccessMode,
                    Created = DateTime.Now,
                    ScopeDisplayName = n.ScopeDisplayName,
                    ScopeName = n.ScopeName
                })
                .ToArray();
        }

        private ISecurityRepository GetSecurityRepo()
        {
            return httpContext.HttpContext.RequestServices.GetService<ISecurityRepository>();
            ;
        }

        private AuthTypeUserLabels[] GetUserLabels()
        {
            var userProvider = httpContext.HttpContext.RequestServices.GetRequiredService<IUserNameMapper>();
            return (from t in httpContext.HttpContext.User.Identities
                    where t.IsAuthenticated
                    select new { UserLabels = userProvider.GetUserLabels(t), AuthType = t.AuthenticationType })
                .GroupBy(m => m.AuthType)
                .Select(n => new AuthTypeUserLabels { UserLabels = n.SelectMany(n => n.UserLabels).Distinct().ToArray(), AuthenticationType = n.Key }).ToArray();
        }
    }
}
