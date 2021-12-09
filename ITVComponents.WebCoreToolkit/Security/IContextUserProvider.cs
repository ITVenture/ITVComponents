using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Security
{
    public interface IContextUserProvider
    {
        /// <summary>
        /// Gets the user that is used within the current context
        /// </summary>
        ClaimsPrincipal User { get; }

        /// <summary>
        /// Gets the current HttpContext
        /// </summary>
        HttpContext HttpContext { get; }

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
