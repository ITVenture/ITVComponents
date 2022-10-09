using ITVComponents.WebCoreToolkit.Net.TelerikUi.Handlers.Model;
using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Handlers;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Extensions
{
    public static class RouteExtensions
    {
        public static void UseFeatureModules(this WebApplication builder, string explicitTenantParam, bool forAreas, out IEndpointConventionBuilder getBuilder, out IEndpointConventionBuilder postBuilder)
        {
            bool forExplicitTenants = !string.IsNullOrEmpty(explicitTenantParam);
            getBuilder = builder.MapGet(
                $"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/Util/FeatureModule/{{moduleName:required}}",
                ModuleTemplateHandler.ReadModuleTemplateConfig).RequireAuthorization();
            postBuilder = builder.MapPost(
                    $"{(forExplicitTenants ? $"/{{{explicitTenantParam}:permissionScope}}" : "")}{(forAreas ? "/{area:exists}" : "")}/Util/FeatureModule/{{moduleName:required}}",
                    ModuleTemplateHandler.SaveModuleTemplateConfig).RequireAuthorization().Accepts<Dictionary<string,string>>("application/json");
        }
    }
}
