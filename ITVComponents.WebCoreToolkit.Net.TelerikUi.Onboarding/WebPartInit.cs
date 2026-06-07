using ITVComponents.Scripting.CScript.Core;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Options;
using Microsoft.Extensions.Configuration;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Onboarding.Extensions;
using Microsoft.AspNetCore.Identity.UI.Services;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.Extensions;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.Services.Impl;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Onboarding.Helpers;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Onboarding
{
    [WebPart]
    public static class WebPartInit
    {
        private static bool resolverRegistered = false;

        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string key, string path)
        {
            try
            {
                if (key == "ContextSettings")
                {
                    return config.GetSection<SecurityContextOptions>(path);
                }

            }
            finally
            {
                if (!resolverRegistered)
                {
                    ManagementNavDefaults.RegisterNavTagResolverCallback(CobNavDefaults.ResolveCobViews);
                    resolverRegistered = true;
                }
            }

            return null;
        }

        [ServiceRegistrationMethod]
        public static void Register(IServiceCollection services, [WebPartConfig("ContextSettings")] SecurityContextOptions contextOptions)
        {
            if (contextOptions.ConfigureContext)
            {
                services.AddTransient<UserGuard<User>, UserGuard>();
                services.ConfigurePageModelHandlerFactory(ha =>
                {
                    ha.ConfigureGenericArgument("TUser", typeof(User));
                });

                services.ConfigureIdentityPages(ip => ip.AddNavPage(ManagementNavDefaults.GetDefaultPage("myTenants")));
            }
        }

        [MvcRegistrationMethod]
        public static void RegisterTenantViewAssemblyPart(ApplicationPartManager manager, [WebPartConfig("ContextSettings")] SecurityContextOptions options, [WebPartConfig(WebCoreToolkit.Global.PartTypeLoadBehaviorOption)] AssemblyPartTypeLoadBehaviorOptions loadingOptions)
        {
            if (options.ConfigureContext)
            {
                if (!string.IsNullOrEmpty(options?.ContextType))
                {
                    var dic = new Dictionary<string, object>();
                    var t = (Type)ExpressionParser.Parse(options.ContextType, dic);
                    manager.EnableItvIdentityViews(t, loadingOptions);
                }
                else
                {
                    throw new InvalidOperationException("Unable to register Views without a Context-Type");
                }
            }
        }
    }
}
