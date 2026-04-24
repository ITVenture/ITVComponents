using ITVComponents.WebCoreToolkit.Net.SpaApi.Handlers;
using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Extensions
{
    public static  class RouteExtensions
    {
        public static IEndpointConventionBuilder UseFilteredAutoForeignKeys(this WebApplication builder, string explicitTenantParam, bool forAreas, bool withAuthorization = true)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            ContextExtensions.Init();
            RouteHandlerBuilder tmp;
            if (withAuthorization)
            {
                tmp = builder.MapGet(
                    $"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/ForeignKey/{{connection:regex(^[\\w_]+$)}}/{{table:required}}",
                    ForeignKeyHandler.FkWithAuth).RequireAuthorization();

            }
            else
            {
                tmp = builder.MapGet(
                    $"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/ForeignKey/{{connection:regex(^[\\w_]+$)}}/{{table:required}}",
                    ForeignKeyHandler.FkNoAuth);
            }

            return tmp;
        }
    }
}
