using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public DefaultCookieService(IHttpContextAccessor httpContext, IOptions<DefaultCookieOptions> options)
        {
            this.httpContext = httpContext;
            this.options = options;
        }

        public bool TryGetCookie(string cookieKey, out string cookieValue)
        {
            var tmp = httpContext.HttpContext.Request.Cookies.TryGetValue(cookieKey, out cookieValue);
            if (tmp && CookieBufferHelper.IsBufferCookie(cookieValue, "DCMB", out var guid, out var validity))
            {
                var objectProvider = httpContext.HttpContext.RequestServices.GetObjectProvider("ITVWCT:DCS:Buffer", null);
                if (tmp=(objectProvider != null))
                {
                    cookieValue = objectProvider.GetBufferedValue<string>($"{guid:N}", out var tmpVal);
                    tmp = cookieValue != null && tmpVal == validity;
                }
            }

            return tmp;
        }

        public void SetCookie(string cookieKey, string cookieValue, CookieOptions cookieOptions = null)
        {
            var opt = options.Value;
            var objectProvider = httpContext.HttpContext.RequestServices.GetObjectProvider("ITVWCT:DCS:Buffer", null);
            if (objectProvider != null && cookieValue.Length > opt.LengthToBufferThreashold)
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
