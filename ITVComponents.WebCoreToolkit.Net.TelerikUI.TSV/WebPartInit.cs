using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Options;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string key, string path)
        {
            if (key == "ContextSettings")
            {
                return config.GetSection<SecurityContextOptions>(path);
            }
            else if (key == "ViewConfig")
            {
                return config.GetSection<SecurityViewsOptions>(path);
            }

            return null;
        }

        [MvcRegistrationMethod]
        public static void RegisterTenantViewAssemblyPart(ApplicationPartManager manager, [WebPartConfig("ContextSettings")] SecurityContextOptions options)
        {
            if (!string.IsNullOrEmpty(options?.ContextType))
            {
                var dic = new Dictionary<string, object>();
                var t = (Type)ExpressionParser.Parse(options.ContextType, dic);
                manager.EnableItvTenantViews(t);
            }
            else
            {
                throw new InvalidOperationException("Unable to register Views without a Context-Type");
            }
        }
        
        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services,
            [WebPartConfig("ViewConfig")] SecurityViewsOptions viewOptions)
        {
            if (viewOptions != null)
            {
                services.ConfigureTenantViews(o =>
                {
                    o.TenantLinkMode = viewOptions.TenantLinkMode;
                    o.UseExplicitTenantPasswords = viewOptions.UseExplicitTenantPasswords;
                });
            }
        }
    }
}
