using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.DuckTyping;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Cookies;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Cookies
{
    public class DbCookieService:ICookieService
    {
        private readonly IHttpContextAccessor httpContext;
        private readonly IServiceProvider services;
        private ICoreSystemContext coreDb;
        private readonly IOptions<ServerCookieOptions> options;
        private ICookieService decorated;

        public DbCookieService(IHttpContextAccessor httpContext ,IServiceProvider services, IOptions<ServerCookieOptions> options)
        {
            this.httpContext = httpContext;
            this.services = services;
            this.options = options;
            decorated = ActivatorUtilities.CreateInstance<DefaultCookieService>(services);
        }

        private ICoreSystemContext SystemContext => coreDb ??= services.GetService<ICoreSystemContext>();

        public bool TryGetCookie(string cookieKey, out string cookieValue)
        {
            cookieValue = null;
            var  isSvCookie = IsServerCookie(cookieKey, out var svCookie, out var found, out var cookieVal, out var validUntil);
            if (found)
            {
                cookieValue = cookieVal;
                if (isSvCookie && validUntil > DateTime.Now)
                {
                    cookieValue = svCookie.Content;
                }
                else if (isSvCookie)
                {
                    cookieValue = null;
                }
            }
            
            return found;
        }

        public void SetCookie(string cookieKey, string cookieValue, CookieOptions cookieOptions = null)
        {
            TryDropOldCookie(cookieKey);
            var opt = options.Value;

            if (cookieValue.Length > opt.CookieLengthThreshold)
            {

                var replacementVal = CookieBufferHelper.CreateBufferCookieTag("SC", cookieOptions, opt.DefaultCookieValidDays, out var validity, out var gd);
                var rec = new ServerCookie { Key = gd, ValidThrough = validity };
                rec.Content = cookieValue;
                SystemContext.ServerCookies.Add(rec);
                SystemContext.SaveChanges();
                cookieValue = replacementVal;
            }

            decorated.SetCookie(cookieKey, cookieValue, cookieOptions);
        }

        private bool TryDropOldCookie(string cookieKey)
        {
            var retVal = IsServerCookie(cookieKey, out var serverCookie, out _, out _, out _);
            if (retVal)
            {

                SystemContext.ServerCookies.Remove(serverCookie);
                SystemContext.SaveChanges();
            }


            return retVal;
        }

        private bool IsServerCookie(string cookieKey, out ServerCookie cookie, out bool cookieFound, out string oriCookie, out DateTime validUntil)
        {

            cookieFound = decorated.TryGetCookie(cookieKey, out oriCookie);
            var cookieValue = oriCookie;
            if (cookieFound)
            {
                if (CookieBufferHelper.IsBufferCookie(cookieValue, "SC", out var guid, out validUntil))
                {
                    var vd = validUntil;
                    cookie = SystemContext.ServerCookies.FirstOrDefault(n => n.Key == guid && n.ValidThrough == vd);
                    return cookie != null;
                }
            }

            validUntil = DateTime.MaxValue;
            cookie = null;
            return false;
        }
    }
}
