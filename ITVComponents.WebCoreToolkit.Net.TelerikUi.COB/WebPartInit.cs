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
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Options;
using Microsoft.Extensions.Configuration;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Extensions;
using Microsoft.AspNetCore.Identity.UI.Services;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Services.Impl;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Helpers;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB
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
