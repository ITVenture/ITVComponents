using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.PostgreSql.SyntaxHelper;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.PostgreSql
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
                config.RefResolve(retVal);
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
            [WebPartConfig("ActivationSettings")]ActivationOptions partActivation,
            [SharedObjectHeap]ISharedObjHeap sharedObjects)
        {
            Type t = null;
            if (!string.IsNullOrEmpty(contextOptions.ContextType))
            {
                var dic = new Dictionary<string, object>();
                t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
                services.ConfigureMethods(t, bld => PostgreSqlColumnsSyntaxHelper.ConfigureMethods(bld));
            }

            if (partActivation.ActivateDbContext)
            {
                if (t != null)
                {
                    services.UseDbIdentities(t, (services,options) => options.UseNpgsql(partActivation.ConnectionStringName));
                }
                else
                {
                    services.UseDbIdentities((services, options) => options.UseNpgsql(partActivation.ConnectionStringName));
                }
            }
        }
    }
}
