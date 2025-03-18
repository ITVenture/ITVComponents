using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTreeTenantSecurityUserView.Extensions;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTreeTenantSecurityUserView
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static SecurityContextOptions LoadOptions(IConfiguration config, string path)
        {
            return config.GetSection<SecurityContextOptions>(path);
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services)
        {
            services.UseSecurityContextUserExtensions();
        }

        [MvcRegistrationMethod]
        public static void RegisterTenantViewAssemblyPart(ApplicationPartManager manager, [WebPartConfig] SecurityContextOptions options, [WebPartConfig(WebCoreToolkit.Global.PartTypeLoadBehaviorOption)] AssemblyPartTypeLoadBehaviorOptions loadingOptions)
        {
            if (options.ConfigureContext)
            {
                if (!string.IsNullOrEmpty(options?.ContextType))
                {
                    var dic = new Dictionary<string, object>();
                    var t = (Type)ExpressionParser.Parse(options.ContextType, dic);
                    manager.EnableItvUserView(t, loadingOptions);
                }
                else
                {
                    manager.EnableItvUserView(loadingOptions);
                }
            }
        }
    }
}
