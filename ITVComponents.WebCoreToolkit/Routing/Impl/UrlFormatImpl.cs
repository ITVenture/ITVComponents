using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Routing.Impl
{
    public class UrlFormatImpl:IUrlFormat
    {
        private readonly IHttpContextAccessor httpContext;
        private readonly IPermissionScope permissionScope;

        public UrlFormatImpl(IHttpContextAccessor httpContext, IPermissionScope permissionScope)
        {
            this.httpContext = httpContext;
            this.permissionScope = permissionScope;
        }

        /// <summary>
        /// Formats a url using the given url-prototype. Use [paramSlash] to access a parameter suffixed with a slash or [Slashparam] to access the parameter prefixed with a slash
        /// </summary>
        /// <param name="url">the route prototype for a route that needs to be formatted</param>
        /// <returns>the formatted url.</returns>
        public string FormatUrl(string url)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            foreach (var arg in httpContext.HttpContext.Request.RouteValues)
            {
                if (arg.Value != null && (!(arg.Value is string) || !string.IsNullOrEmpty((string) arg.Value)))
                {
                    values.Add(arg.Key, arg.Value);
                    values.Add($"{arg.Key}Slash", $"{arg.Value}/");
                    values.Add($"Slash{arg.Key}", $"/{arg.Value}");
                }
            }

            if (permissionScope.IsScopeExplicit)
            {
                values.Add("permissionScope", permissionScope.PermissionPrefix);
                values.Add("permissionScopeSlash", $"{permissionScope.PermissionPrefix}/");
                values.Add("SlashPermissionScope", $"/{permissionScope.PermissionPrefix}");
            }

            return values.FormatText(url);
        }
    }
}
