using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Cookies
{
    public interface ICookieService
    {
        bool TryGetCookie(string cookieKey, out string cookieValue);
        void SetCookie(string cookieKey, string cookieValue, CookieOptions cookieOptions = null);
    }
}
