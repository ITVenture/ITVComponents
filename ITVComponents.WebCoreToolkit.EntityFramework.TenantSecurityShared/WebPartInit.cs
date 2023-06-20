using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.TemplateHandling;
using Microsoft.Extensions.DependencyInjection;
using ITVComponents.Scripting.CScript.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.Interceptors;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string settingsKey, string path)
        {
            if (settingsKey == "ContextSettings")
            {
                return config.GetSection<SecurityContextOptions>(path);
            }

            if (settingsKey == "ActivationSettings")
            {
                return config.GetSection<ActivationOptions>(path);
            }

            return null;
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, [WebPartConfig("ContextSettings")] SecurityContextOptions contextOptions,
            [WebPartConfig("ActivationSettings")] ActivationOptions partOptions)
        {
            Type t = null;
            if (!string.IsNullOrEmpty(contextOptions.ContextType))
            {
                var dic = new Dictionary<string, object>();
                t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
            }

            if (partOptions.ActivateTemplateFactory)
            {
                services.AddScoped<ITemplateHandlerFactory, TemplateHandlerFactory>();
            }

            if (partOptions.ActivateFilters && t != null)
            {
                services.ConfigureGlobalFilters(t);
            }

            if (partOptions.ActivateDefaultContextUserProvider && t != null)
            {
                services.ConfigureDefaultContextUserProvider(t);
            }
        }

        [CustomConfigurator(typeof(DbContextOptionsBuilder))]
        public static void ConfigureDbInterceptors(DbContextOptionsBuilder optionsBuilder, IServiceProvider services,
            [WebPartConfig("ActivationSettings")] ActivationOptions partOptions)
        {
            if (partOptions.ActivateCreateModifyAttributes)
            {
                optionsBuilder.AddInterceptors(new ModCreateInterceptor(services, partOptions.UseUTCForCreateModifyAttributes));
            }
        }
    }
}
