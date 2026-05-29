using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.SqlServer.SyntaxHelper;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.SqlServer
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
            /*Type t = null;
            if (!string.IsNullOrEmpty(contextOptions.ContextType))
            {
                var dic = new Dictionary<string, object>();
                t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
                services.ConfigureMethods(t, bld => SqlColumnsSyntaxHelper.ConfigureMethods(bld));
            }*/

            if (contextOptions.ConfigureContext)
            {
                Type t = null;
                if (!string.IsNullOrEmpty(contextOptions.ContextType))
                {
                    var dic = new Dictionary<string, object>();
                    t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
                    services.ConfigureMethods(t, bld =>
                    {
                        SqlColumnsSyntaxHelper.ConfigureMethods(bld);
                        SqlColumnsSyntaxHelper.ConfigureVirtualTables(bld);
                    });
                }

                if (!TenantSecurityInitializer.ContextTypeInitialized)
                {
                    TenantSecurityInitializer.SetContextType(t, IdentityStrategy.CoreIdentity, TenantStrategy.Tree);
                }
            }

            if (partActivation.ActivateDbContext)
            {
                var manager = sharedObjects.Property<WebPartManager>("WebPartManager").Value;
                TenantSecurityInitializer.DependencyInit.UseDbIdentities(services, (services, options) =>
                {
                    options.UseSqlServer(partActivation.ConnectionStringName);
                    manager.CustomObjectConfig(options, services);
                });
                /*if (t != null)
                {
                    //services.AddDbContext<>()
                    AspNetCoreTenants.WebPartInit.DependencyInit.UseDbIdentities(services, (services,options) =>
                    {
                        options.UseSqlServer(partActivation.ConnectionStringName);
                        manager.CustomObjectConfig(options, services);
                    });
                }
                else
                {
                    services.UseDbIdentities((services, options) =>
                    {
                        options.UseSqlServer(partActivation.ConnectionStringName);
                        manager.CustomObjectConfig(options, services);
                    });
                }*/
            }
        }
    }
}
