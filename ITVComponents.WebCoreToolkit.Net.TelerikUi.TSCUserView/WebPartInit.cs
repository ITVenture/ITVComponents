using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityContextUserView.Extensions;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityContextUserView
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static SecurityContextOptions LoadOptions(IConfiguration config, string path)
        {
            return config.GetSection<SecurityContextOptions>(path);
        }

        [MvcRegistrationMethod]
        public static void RegisterTenantViewAssemblyPart(ApplicationPartManager manager, SecurityContextOptions options)
        {
            if (!string.IsNullOrEmpty(options?.ContextType))
            {
                var dic = new Dictionary<string, object>();
                var t = (Type)ExpressionParser.Parse(options.ContextType, dic);
                manager.EnableItvUserView(t);
            }
            else
            {
                manager.EnableItvUserView();
            }
        }
    }
}
