﻿using System;
using System.Linq;
using System.Numerics;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Http;
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
            if (identities != null)
            {
                logger.LogDebug($"Found: {httpContext.HttpContext.User.Identity.Name} identities");
                if (!string.IsNullOrEmpty(opt.RouteOverrideParam) && httpContext.HttpContext.Request.RouteValues.ContainsKey(opt.RouteOverrideParam) && !string.IsNullOrEmpty((string) httpContext.HttpContext.Request.RouteValues[opt.RouteOverrideParam]))
                {
                    IsScopeExplicit = true;
                    return (string) httpContext.HttpContext.Request.RouteValues[opt.RouteOverrideParam];
                }

                if (httpContext.HttpContext.User.Identity?.IsAuthenticated ?? false)
                {
                    logger.LogDebug($"Authenticated User: {httpContext.HttpContext.User.Identity.Name}");
                    bool decryptFailed = false;
                    if (!httpContext.HttpContext.Request.Cookies.TryGetValue(opt.ScopeCookie, out retVal) || (decryptFailed = !TryReadScopeToken(ref retVal, out var renew)) || renew)
                    {
                        if (string.IsNullOrEmpty(retVal) || decryptFailed)
                        {
                            retVal = opt.DefaultScopeExpression(httpContext.HttpContext);
                            logger.LogDebug($"Default-Value of {opt.ScopeCookie}: {retVal}");
                        }

                        if (!string.IsNullOrEmpty(retVal))
                        {
                            ChangeScope(retVal);
                        }
                    }


                    logger.LogDebug($"retVal: {retVal}");
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
