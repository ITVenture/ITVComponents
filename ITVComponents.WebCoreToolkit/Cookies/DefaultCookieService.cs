using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DIServices;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Cookies
{
    public class DefaultCookieService:ICookieService
    {
        private readonly IHttpContextAccessor httpContext;
        private readonly IOptions<DefaultCookieOptions> options;
        private IObjectProvider ObjectProvider => httpContext.HttpContext?.RequestServices.GetObjectProvider("ITVWCT:DCS:Buffer", null);
        public DefaultCookieService(IHttpContextAccessor httpContext, IOptions<DefaultCookieOptions> options)
        {
            this.httpContext = httpContext;
            this.options = options;
        }

        public bool ServerCookiesSupported => ObjectProvider != null;

        public bool Ready => httpContext.HttpContext != null;

        public bool TryGetCookie(string cookieKey, out string cookieValue)
        {
            var objectProvider = ObjectProvider;
            var tmp = httpContext.HttpContext.Request.Cookies.TryGetValue(cookieKey, out cookieValue);
            if (tmp && CookieBufferHelper.IsBufferCookie(cookieValue, "DCMB", out var guid, out var validity))
            {
                if (tmp=(objectProvider != null))
                {
                    cookieValue = objectProvider.GetBufferedValue<string>($"{guid:N}", out var tmpVal);
                    tmp = cookieValue != null && tmpVal == validity;
                }
            }

            return tmp;
        }

        public void SetCookie(string cookieKey, string cookieValue, CookieOptions cookieOptions = null, CookieStrategy preferredStrategy = CookieStrategy.Client)
        {
            var objectProvider = ObjectProvider;
            var opt = options.Value;
            if (objectProvider != null && (preferredStrategy == CookieStrategy.Server || cookieValue.Length > opt.LengthToBufferThreashold))
            {
                var id = CookieBufferHelper.CreateBufferCookieTag("DCMB", cookieOptions, opt.BufferValidityDays,
                    out var validity, out var guid);
                var gd = $"{guid:N}";
                objectProvider.UpdateBufferedValue(gd, cookieValue, validity);
                cookieValue  = id;
            }

            if (cookieOptions != null)
            {
                httpContext.HttpContext.Response.Cookies.Append(cookieKey, cookieValue, cookieOptions);
            }
            else
            {
                httpContext.HttpContext.Response.Cookies.Append(cookieKey, cookieValue);
            }
        }
    }
}
