using System;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.Security.UserScopes
{
    public class CookieScopeOptions
    {
        public string ScopeCookie { get;set; }
        public bool EncryptCookie { get; set; }

        public string RouteOverrideParam { get; set; }

        /// <summary>
        /// Picks a default scope when none is stored or the stored one is no longer eligible. Receives the
        /// host-neutral <see cref="IContextUserProvider"/> (user, services, route) instead of an HttpContext,
        /// so the same expression works under MVC and Blazor.
        /// </summary>
        public Func<IContextUserProvider, ScopeInfo[], string> DefaultScopeExpression { get;set; }

        public int CoookieRenewalMinutes { get; set; } = 30;
    }
}
