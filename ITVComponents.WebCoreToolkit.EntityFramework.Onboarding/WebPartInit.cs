using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;
using System;
using System.Collections.Generic;
using ITVComponents.Settings.Native;
using Microsoft.Extensions.Configuration;
using ITVComponents.Scripting.CScript.Core;
using Microsoft.Extensions.DependencyInjection;
using FlatExtensions = ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Extensions.DependencyExtensions;
using TreeExtensions = ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Extensions.DependencyExtensions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding
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
        public static void RegisterServices(IServiceCollection services,
            [WebPartConfig("ContextSettings")] SecurityContextOptions contextOptions
            , [WebPartConfig("ActivationSettings")] ActivationOptions partOptions)
        {
            if (contextOptions.ConfigureContext)
            {
                Type t = null;
                if (!string.IsNullOrEmpty(contextOptions.ContextType))
                {
                    var dic = new Dictionary<string, object>();
                    t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
                }

                if (t != null && partOptions.ActivateFilters)
                {
                    if (partOptions.Strategy == TenantStrategy.Tree)
                    {
                        TreeExtensions.ActivateGlobalCobFilters(services, t);
                    }
                    else
                    {
                        FlatExtensions.ActivateGlobalCobFilters(services, t);
                    }
                }
            }
        }
    }
}
