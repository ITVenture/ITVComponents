using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.Comparers;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace ITVComponents.WebCoreToolkit.Net.Handlers
{
    internal static class UserPermissionsHandler
    {
        /// <summary>
        /// Returns all permission of the current user
        /// </summary>
        /// <param name="context">the http-context in which the Widget is being executed</param>
        /// <response code="200">a list of permissions that are assigned to the current user</response>
        public static async Task<IResult> ReadUserPermissions(HttpContext context, IServiceProvider services)
        {
            var retVal = Array.Empty<Models.Permission>();
            ISecurityRepository repo;
            IdentityInfo[] identities;
            if ((services.IsLegitSharedAssetPath(out repo, out identities, out var denied) ||
                 services.IsUserAuthenticated(out repo, out identities)) && !denied)
            {
                //var repo = context.RequestServices.GetService<ISecurityRepository>();
                //var mapper = context.RequestServices.GetService<IUserNameMapper>();
                //var userLabels = mapper.GetUserLabels(context.User);
                //var authType = context.User.Identity.AuthenticationType;
                retVal = identities.SelectMany(i => repo.GetPermissions(i.Labels, i.AuthenticationType))
                    .Distinct(new PermissionComparer()).ToArray();
            }

            return Results.Json(retVal, new JsonSerializerOptions());
        }
    }
}
