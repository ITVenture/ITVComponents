using ITVComponents.WebCoreToolkit.Net.TelerikUi.Handlers.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.DuckTyping.Extensions;
using ITVComponents.Helpers;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.ModuleConfigHandling;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.TemplateHandling;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Handlers
{
    public static class ModuleTemplateHandler
    {
        public static async Task<IResult> SaveModuleTemplateConfig(HttpContext context, string area, string moduleName, [FromBody] Dictionary<string, string> configurationData, [FromServices] ICoreSystemContext db,
            [FromServices] ITemplateHandlerFactory handlerFactory)
        {
            var template =
                db.TemplateModules.FirstOrDefault(n => n.TemplateModuleName.ToLower() == moduleName.ToLower());
            if (template != null)
            {
                var configurators = template.Configurators.OrderBy(n => n.Name);
                foreach (var configurator in configurators)
                {
                    UpdateConfiguration(configurator, handlerFactory, configurationData[configurator.Name]);
                }
            }

            return Results.Ok("Ok");
        }

        public static async Task<IResult> ReadModuleTemplateConfig(HttpContext context, string area, string moduleName,
            [FromServices] ICoreSystemContext db,
            [FromServices] ITemplateHandlerFactory handlerFactory)
        {
            var retVal = new Dictionary<string, object>();
            var template =
                db.TemplateModules.FirstOrDefault(n => n.TemplateModuleName.ToLower() == moduleName.ToLower());
            if (template != null)
            {
                var configurators = template.Configurators.OrderBy(n => n.Name);
                foreach (var configurator in configurators)
                {
                    retVal.Add(configurator.Name, FetchConfiguration(configurator, handlerFactory));
                }

                return Results.Json(retVal, new JsonSerializerOptions());
            }

            return Results.NoContent();
        }

        private static void UpdateConfiguration(TemplateModuleConfigurator configurator, ITemplateHandlerFactory handlerFactory, string s)
        {
            var handler = handlerFactory.GetBackEndHandler(configurator, out var param);
            param["values"] = s;
            ModuleConfigHandlerHelper.InvokeHandler(handler, HandlerMethodName.SetConfig, param);
        }

        private static object FetchConfiguration(TemplateModuleConfigurator configurator, ITemplateHandlerFactory handlerFactory)
        {
            var handler = handlerFactory.GetBackEndHandler(configurator, out var param);
            return ModuleConfigHandlerHelper.InvokeHandler<object>(handler, HandlerMethodName.GetConfig, param);
        }
    }
}
