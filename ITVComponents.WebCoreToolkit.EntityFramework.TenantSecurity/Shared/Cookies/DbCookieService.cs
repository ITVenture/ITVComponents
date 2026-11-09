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
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Cookies
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

        public bool ServerCookiesSupported => true;

        public bool Ready => httpContext.HttpContext != null;

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
                    found = false;
                    cookieValue = null;
                }
            }
            
            return found;
        }

        public void SetCookie(string cookieKey, string cookieValue, CookieOptions cookieOptions = null, CookieStrategy preferredStrategy = CookieStrategy.Client)
        {
            TryDropOldCookie(cookieKey);
            var opt = options.Value;

            if ((preferredStrategy == CookieStrategy.Server && !decorated.ServerCookiesSupported) || cookieValue.Length > opt.CookieLengthThreshold)
            {

                var replacementVal = CookieBufferHelper.CreateBufferCookieTag("SC", cookieOptions, opt.DefaultCookieValidDays, out var validity, out var gd);
                var rec = new ServerCookie { Key = gd, ValidThrough = validity };
                rec.Content = cookieValue;
                SystemContext.ServerCookies.Add(rec);
                SystemContext.SaveChanges();
                cookieValue = replacementVal;
                if (preferredStrategy == CookieStrategy.Server)
                {
                    preferredStrategy = CookieStrategy.Client;
                }
            }

            decorated.SetCookie(cookieKey, cookieValue, cookieOptions, preferredStrategy);
        }

        private bool TryDropOldCookie(string cookieKey)
        {
            var retVal = IsServerCookie(cookieKey, out var serverCookie, out _, out _, out _);
            if (retVal)
            {
                var buffer = services.GetService<ServerCookieBuffer>();
                if (buffer == null)
                {
                    SystemContext.ServerCookies.Remove(serverCookie);
                    SystemContext.SaveChanges();
                }
                else
                {
                    // The cookie may come from the buffer and is then not tracked by the context.
                    var key = serverCookie.Key;
                    var vd = serverCookie.ValidThrough;
                    SystemContext.ServerCookies.Where(n => n.Key == key && n.ValidThrough == vd).ExecuteDelete();
                    buffer.Remove(key, vd);
                }
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
                    var buffer = services.GetService<ServerCookieBuffer>();
                    if (buffer == null)
                    {
                        cookie = SystemContext.ServerCookies.FirstOrDefault(n => n.Key == guid && n.ValidThrough == vd);
                        return cookie != null;
                    }

                    if (buffer.TryGetContent(guid, vd, () =>
                        {
                            var row = SystemContext.ServerCookies.AsNoTracking().FirstOrDefault(n => n.Key == guid && n.ValidThrough == vd);
                            return (row != null, row?.Content);
                        }, out var content))
                    {
                        cookie = new ServerCookie { Key = guid, ValidThrough = vd, Content = content };
                        return true;
                    }

                    cookie = null;
                    return false;
                }
            }

            validUntil = DateTime.MaxValue;
            cookie = null;
            return false;
        }
    }
}
