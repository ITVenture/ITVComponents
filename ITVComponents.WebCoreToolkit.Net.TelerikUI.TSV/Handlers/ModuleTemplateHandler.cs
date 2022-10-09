using ITVComponents.WebCoreToolkit.Net.TelerikUi.Handlers.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Handlers
{
    public static class ModuleTemplateHandler
    {
        public static async Task<IResult> SaveModuleTemplateConfig(HttpContext context, string area, string moduleName,
            [FromBody] Dictionary<string,string> configurationData, [FromServices] IBaseTenantContext db)
        {
            return Results.Ok("Ok");
        }

        public static async Task<IResult> ReadModuleTemplateConfig(HttpContext context, string area, string moduleName, [FromServices] IBaseTenantContext db)
        {
            var retVal = new Dictionary<string, object>();
            var template = db.TemplateModules.FirstOrDefault(n => n.TemplateModuleName == moduleName);
            if (template != null)
            {
                var configurators = template.Configurators.OrderBy(n => n.Name);
                return Results.Json(retVal, new JsonSerializerOptions());
            }

            return Results.NoContent();
        }
    }
}
