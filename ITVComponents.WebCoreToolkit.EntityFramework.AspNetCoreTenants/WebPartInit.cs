using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants
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
                var retVal = config.GetSection<ActivationOptions>(path);
                if (retVal.ActivateDbContext)
                {
                    retVal.ConnectionStringName = config.GetConnectionString(retVal.ConnectionStringName);
                }

                return retVal;
            }

            return null;//config.GetSection<UserViewOptions>(path);
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, [WebPartConfig("ContextSettings")]SecurityContextOptions contextOptions,
            [WebPartConfig("ActivationSettings")]ActivationOptions partActivation)
        {
            Type t = null;
            if (!string.IsNullOrEmpty(contextOptions.ContextType))
            {
                var dic = new Dictionary<string, object>();
                t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
            }

            if (partActivation.ActivateDbContext)
            {
                if (t != null)
                {
                    services.UseDbIdentities(t, options => options.UseSqlServer(partActivation.ConnectionStringName));
                }
                else
                {
                    services.UseDbIdentities(options => options.UseSqlServer(partActivation.ConnectionStringName));
                }

            }

            if (partActivation.UseNavigation)
            {
                if (t != null)
                {
                    services.UseDbNavigation(t);
                }
                else
                {
                    services.UseDbNavigation();
                }
            }

            if (partActivation.UsePlugins)
            {
                services.UseDbPlugins();
            }

            if (partActivation.UseGlobalSettings)
            {
                services.UseDbGlobalSettings();
            }

            if (partActivation.UseTenantSettings)
            {
                services.UseTenantSettings();
            }

            if (partActivation.UseLogAdapter)
            {
                services.UseDbLogAdapter();
            }
            /*if (!string.IsNullOrEmpty(options?.ContextType))
            {
                var dic = new Dictionary<string, object>();
                var t = (Type)ExpressionParser.Parse(options.ContextType, dic);
                manager.EnableItvUserView(t);
            }
            else
            {
                manager.EnableItvUserView();
            }*/
        }
    }
}
