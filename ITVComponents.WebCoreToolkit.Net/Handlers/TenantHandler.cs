using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.Comparers;
using ITVComponents.WebCoreToolkit.Net.Handlers.Model;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.Handlers
{
    internal static class TenantHandler
    {
        /// <summary>
        /// Switches to the specified tenant
        /// </summary>
        /// <param name="context">the http-context in which the query is being executed</param>
        /// <param name="formData">the form-data containing a NewTenant-attribute</param>
        /// <response code="200">when the switch to the provided tenant was successful</response>
        /// <response code="401">when the provided tenant is not accessible for the current user</response>
        /// <response code="404">when the form-data could not be processed</response>
        public static async Task<IResult> SwitchTenant(HttpContext context, TenantSwitchForm formData)
        {
            if (formData != null)
            {
                var scopeProvider =
                    context.RequestServices.GetRequiredService<IPermissionScope>();
                var securityRepo =
                    context.RequestServices.GetRequiredService<ISecurityRepository>();
                var userProvider = context.RequestServices.GetRequiredService<IUserNameMapper>();
                var eligibleTenants =
                    (from t in context.User.Identities
                        where t.IsAuthenticated
                        select securityRepo.GetEligibleScopes(userProvider.GetUserLabels(t), t.AuthenticationType))
                    .SelectMany(n => n).Distinct(new ScopeInfoComparer()).ToArray();
                if (eligibleTenants.Any(n => n.ScopeName.ToLower() == formData.NewTenant.ToLower()))
                {
                    scopeProvider.ChangeScope(formData.NewTenant);
                    return Results.Ok();
                }

                return Results.Unauthorized();
            }

            return Results.NotFound();
        }

        /// <summary>
        /// Returns all permission of the current user
        /// </summary>
        /// <param name="context">the http-context in which the Widget is being executed</param>
        /// <response code="200">a list of permissions that are assigned to the current user</response>
        public static async Task<IResult> ReadTenantFeatures(HttpContext context, ISecurityRepository repo)
        {
            var retVal =  repo.GetFeatures(null).Distinct(new FeatureComparer()).ToArray();
            return Results.Json(retVal, new JsonSerializerOptions());
        }
    }
}
