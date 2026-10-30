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
using ITVComponents.Logging;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.Cookies;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

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
                var tmp = config.GetSection<ActivationOptions>(path);
                var rawSchema = tmp.CustomSchema;
                config.RefResolve(tmp);
                // RefResolve handles :--> itself, but silently leaves an unresolvable reference empty. The ->-notation
                // (as used by the plugin-side CustomSchemaOptionsLoader) is unknown to RefResolve and goes through the shared helper.
                if (!string.IsNullOrEmpty(rawSchema) && rawSchema.StartsWith("->"))
                {
                    tmp.CustomSchema = SchemaConfiguration.Resolve(rawSchema, config);
                }
                else if (!string.IsNullOrEmpty(rawSchema) && rawSchema.StartsWith(":-->") && string.IsNullOrEmpty(tmp.CustomSchema))
                {
                    LogEnvironment.LogEvent($"The schema-reference '{rawSchema}' in the ActivationSettings did not resolve to a value. The default schema of the model is used.", LogSeverity.Warning);
                }
                return tmp;
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

                if (!string.IsNullOrEmpty(partOptions.CustomSchema) && t != null)
                {
                    services.ConfigureCustomTenantSecuritySchema(t, partOptions.CustomSchema);
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

            if (partOptions.UseDefaultSecurityAccessProvider)
            {
                services.UseDefaultSecurityAccessProvider();
            }
        }
    }
}
