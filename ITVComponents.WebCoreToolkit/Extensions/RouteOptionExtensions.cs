using ITVComponents.WebCoreToolkit.Routing;
using Microsoft.AspNetCore.Routing;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class RouteOptionExtensions
    {
        public static RouteOptions UsePermissionScopeInRoute(this RouteOptions options)
        {
            options.ConstraintMap.Add("permissionScope", typeof(CheckPermissionScopeExists));
            return options;
        }
    }
}
