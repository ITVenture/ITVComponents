using System;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Cookies;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Security.UserScopes
{
    /// <summary>
    /// MVC/HTTP <see cref="IPermissionScope"/> strategy: persists the resolved <see cref="UserScope"/> token in
    /// an (optionally encrypted) cookie. All eligibility/permission resolution is inherited from
    /// <see cref="ResolvingPermissionScope"/>; this type only owns cookie load/store and the cookie-specific
    /// renewal window.
    /// </summary>
    public class CookiePermissionScope : ResolvingPermissionScope
    {
        private readonly ICookieService cookieHandler;
        private readonly IOptions<CookieScopeOptions> options;

        /// <summary>
        /// Initializes a new instance of the <see cref="CookiePermissionScope"/> class.
        /// </summary>
        /// <param name="contextUser">the ambient context (user, services, route) — works under MVC and Blazor</param>
        /// <param name="cookieHandler">the cookie service used to persist the scope token</param>
        /// <param name="options">cookie-scope options (cookie name, encryption, renewal, route override, default expression)</param>
        /// <param name="logger">a logger for diagnostic output</param>
        public CookiePermissionScope(IContextUserProvider contextUser, ICookieService cookieHandler, IOptions<CookieScopeOptions> options, ILogger<CookiePermissionScope> logger)
            : base(contextUser, logger)
        {
            this.cookieHandler = cookieHandler;
            this.options = options;
        }

        /// <inheritdoc/>
        protected override string RouteOverrideParam => options.Value.RouteOverrideParam;

        /// <inheritdoc/>
        protected override Func<IContextUserProvider, Models.ScopeInfo[], string> DefaultScopeExpression => options.Value.DefaultScopeExpression;

        /// <inheritdoc/>
        protected override UserScope LoadStoredToken()
        {
            var opt = options.Value;
            if (!cookieHandler.TryGetCookie(opt.ScopeCookie, out var defaultScope) || string.IsNullOrEmpty(defaultScope))
            {
                return null;
            }

            try
            {
                return defaultScope.DecompressToken<UserScope>();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Failed to unpack UserScope: {ex.Message}", LogSeverity.Error);
                return null;
            }
        }

        /// <inheritdoc/>
        protected override bool IsTokenStale(UserScope token)
        {
            return DateTime.Now.Subtract(token.Created).TotalMinutes > options.Value.CoookieRenewalMinutes;
        }

        /// <inheritdoc/>
        protected override void PersistToken(UserScope token)
        {
            var opt = options.Value;
            cookieHandler.SetCookie(opt.ScopeCookie,
                token.CompressToken(true, opt.EncryptCookie, useHighCompression: true),
                new CookieOptions
                {
                    Expires = new DateTimeOffset(DateTime.Now.AddMinutes(opt.CoookieRenewalMinutes * 3))
                });
        }
    }
}
