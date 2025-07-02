using System;
using System.Collections.Generic;
using ITVComponents.Json;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Localization;
//using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
//using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Localization;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.TemplateHandling;
//using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.TemplateHandling;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.ConfigMarkupModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared
{
    [WebPart]
    public static class WebPartInit
    {
        static WebPartInit()
        {
            JsonHelper.ExtendNativeProtocolType<PlugInTemplateMarkup,HierarchyPlugInTemplateMarkup>("treeCapable");
            JsonHelper.ExtendNativeProtocolType<ConstTemplateMarkup, HierarchyWebPluginConstantTemplateMarkup>("treeCapable");
            JsonHelper.ExtendNativeProtocolType<SettingTemplateMarkup, HierarchyTenantSettingTemplateMarkup>("treeCapable");
        }

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
            if (contextOptions.ConfigureContext)
            {
                if (!string.IsNullOrEmpty(contextOptions.ContextType))
                {
                    var dic = new Dictionary<string, object>();
                    t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
                }
            }

            if (partOptions.ActivateTemplateFactory)
            {
                services.AddScoped<ITemplateHandlerFactory, TemplateHandlerFactory>();
            }

            if (contextOptions.ConfigureContext)
            {
                if (partOptions.ActivateFilters && t != null)
                {
                    services.ConfigureGlobalFilters(t);
                }

                if (partOptions.ActivateDefaultContextUserProvider && t != null)
                {
                    services.ConfigureDefaultContextUserProvider(t);
                }
            }

            if (partOptions.UseContextLocalizationServices)
            {
                services.AddSingleton<IStringLocalizerFactory>(services =>
                {
                    return new ContextLocalizerFactory(services, services.GetService<IOptions<LocalizationOptions>>(),
                        services.GetService<ILoggerFactory>());
                });
                //services.AddSingleton(typeof(IStringLocalizerFactory), typeof(ContextStringLocalizer));
            }

            if (partOptions.UseServerCookies)
            {
                services.UseServerCookies(o =>
                {
                    o.DefaultCookieValidDays = partOptions.DefaultServerCookieValidity;
                    o.CookieLengthThreshold = partOptions.CookieLengthThreshold;
                });
            }
        }
    }
}
