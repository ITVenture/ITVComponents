using System;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Security.UserScopes
{
    public class CookieScopeOptions
    {
        public string ScopeCookie { get;set; }
        public bool EncryptCookie { get; set; }
        
        public string RouteOverrideParam { get; set; }

        public Func<HttpContext, ScopeInfo[], string> DefaultScopeExpression { get;set; }
        
        public int CookieRenewalDays { get; set; } = 5;
    }
}
