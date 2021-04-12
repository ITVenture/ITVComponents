using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Routing
{
    public class CheckPermissionScopeExists:IRouteConstraint
    {
        private readonly IServiceProvider services;
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="secrepo"></param>
        public CheckPermissionScopeExists(IServiceProvider services)
        {
            this.services = services;
        }

        /// <summary>
        /// Determines whether the URL parameter contains a valid value for this constraint.
        /// </summary>
        /// <param name="httpContext">An object that encapsulates information about the HTTP request.</param>
        /// <param name="route">The router that this constraint belongs to.</param>
        /// <param name="routeKey">The name of the parameter that is being checked.</param>
        /// <param name="values">A dictionary that contains the parameters for the URL.</param>
        /// <param name="routeDirection">
        /// An object that indicates whether the constraint check is being performed
        /// when an incoming request is being handled or when a URL is being generated.
        /// </param>
        /// <returns><c>true</c> if the URL parameter contains a valid value; otherwise, <c>false</c>.</returns>
        public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
        {
            using (var scopedSvc = services.CreateScope())
            {
                var secrepo = scopedSvc.ServiceProvider.GetService<ISecurityRepository>();
                var scopeProvider = scopedSvc.ServiceProvider.GetService<IPermissionScope>();
                if (values.TryGetValue(routeKey, out object value))
                {
                    var parameterValueString = Convert.ToString(value,
                        CultureInfo.InvariantCulture);
                    if (parameterValueString == null)
                    {
                        return false;
                    }

                    var retVal = secrepo.PermissionScopeExists(parameterValueString);
                    return retVal;
                }

                return false;
            }
        }
    }
}
