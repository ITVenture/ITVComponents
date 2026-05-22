using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// Host-neutral access to the ambient user/scope/route of the current context. The MVC edge backs this with
    /// the HttpContext, a Blazor circuit with its AuthenticationState + NavigationManager. Consumers that genuinely
    /// need the raw <c>HttpContext</c> depend on <see cref="IHttpContextUserProvider"/> instead (MVC-only).
    /// </summary>
    public interface IContextUserProvider
    {
        /// <summary>
        /// Gets the user that is used within the current context
        /// </summary>
        ClaimsPrincipal User { get; }

        /// <summary>
        /// Gets the route-data of the current request-context
        /// </summary>
        IDictionary<string, object> RouteData { get; }

        /// <summary>
        /// Gets the path of the current Request
        /// </summary>
        string RequestPath { get; }

        /// <summary>
        /// Gets the Service-Scope for the current action or service call
        /// </summary>
        IServiceProvider Services { get; }
    }
}
