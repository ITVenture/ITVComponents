using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings.Native;
using Microsoft.Extensions.Configuration;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding
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
            Type t = null;
            if (!string.IsNullOrEmpty(contextOptions.ContextType))
            {
                var dic = new Dictionary<string, object>();
                t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
            }

            if (t != null && partOptions.ActivateFilters)
            {
                services.ActivateGlobalCobFilters(t);
            }
        }
    }
}
