using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Middleware
{
    internal class ThreadCultureMiddleware
    {
        RequestDelegate _next;

        public ThreadCultureMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IOptions<CultureOptions> options)
        {
            var cultureProvider = context.Features.Get<IRequestCultureFeature>();
            var opt = options.Value;
            CultureInfo.CurrentCulture = opt.MapCulture(cultureProvider.RequestCulture.Culture);
            CultureInfo.CurrentUICulture = opt.MapUiCulture(cultureProvider.RequestCulture.UICulture);
            await _next(context);
        }
    }
}
