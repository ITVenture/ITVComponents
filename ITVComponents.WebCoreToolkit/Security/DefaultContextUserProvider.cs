using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ITVComponents.WebCoreToolkit.Security
{
    public class DefaultContextUserProvider:IContextUserProvider
    {
        private readonly IHttpContextAccessor httpContext;

        private ClaimsPrincipal user;
        private IDictionary<string, object> customRouteData;
        private string requestPath;

        public DefaultContextUserProvider(IHttpContextAccessor httpContext ,IServiceProvider services)
        {
            Services = services;
            this.httpContext = httpContext;
        }

        public ClaimsPrincipal User => user ?? httpContext?.HttpContext?.User;
        public HttpContext HttpContext => httpContext?.HttpContext;

        public IDictionary<string, object> RouteData =>
            customRouteData ?? httpContext?.HttpContext?.GetRouteData()?.Values;

        public string RequestPath => requestPath ?? httpContext?.HttpContext?.Request.Path;

        public IServiceProvider Services { get; }

        internal void SetDefaults(ClaimsPrincipal user = null, IDictionary<string, object> customRouteData = null, string requestPath = null)
        {
            this.user = user;
            this.customRouteData = customRouteData;
            this.requestPath = requestPath;
        }
    }
}
