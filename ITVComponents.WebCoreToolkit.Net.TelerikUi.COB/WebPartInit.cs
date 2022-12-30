using ITVComponents.Scripting.CScript.Core;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Options;
using Microsoft.Extensions.Configuration;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Services;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB
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
            
            if (key == "ServiceOptions")
            {
                return config.GetSection<CobServiceOptions>(path);
            }

            return null;
        }

        [ServiceRegistrationMethod]
        public static void Register(IServiceCollection services, [WebPartConfig("ServiceOptions")] CobServiceOptions servicesOptions)
        {
            if (servicesOptions.UseDefaultMailSender)
            {
                services.AddSingleton<IEmailSender, DefaultMailSender>();
            }
        }

        [MvcRegistrationMethod]
        public static void RegisterTenantViewAssemblyPart(ApplicationPartManager manager, [WebPartConfig("ContextSettings")] SecurityContextOptions options)
        {
            if (!string.IsNullOrEmpty(options?.ContextType))
            {
                var dic = new Dictionary<string, object>();
                var t = (Type)ExpressionParser.Parse(options.ContextType, dic);
                manager.EnableItvIdentityViews(t);
            }
            else
            {
                throw new InvalidOperationException("Unable to register Views without a Context-Type");
            }
        }
    }
}
