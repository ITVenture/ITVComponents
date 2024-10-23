using System;
using System.Collections.Generic;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DuckTyping.Extensions;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Helpers.Initialization;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Interceptors;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext
{
    [WebPart]
    public static class WebPartInit
    {
        private static IDependencyInitializer init;

        public static IDependencyInitializer DependencyInit => init;

        public static bool ContextTypeInitialized { get; private set; }

        public static void SetContextType(Type contextType)
        {
            if (ContextTypeInitialized)
            {
                throw new InvalidOperationException("ContextType already initialized!");
            }

            var t = contextType ?? typeof(SecurityContext);
            (string name, Type type)[] fxparam = [(name:"TContext", type:t), (name: "TImpl", type: t)];
            init = typeof(Extensions.DependencyExtensions).WrapType<IDependencyInitializer>(t, fixParameters: fxparam);
            init.ExtendWithStatic(typeof(EntityFramework.TenantSecurityShared.Extensions.DependencyExtensions), t,
                fixParameters: fxparam);
            ContextTypeInitialized = true;
        }

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
            }

            if (!ContextTypeInitialized)
            {
                SetContextType(t);
            }

            if (partActivation.ActivateDbContext)
            {
                if (partActivation.UseApplicationIdentitySchema)
                {
                    var l = sharedObjects.Property<List<string>>("SignInSchemes", true);
                    l.Value.AddIfMissing(IdentityConstants.ApplicationScheme, true);
                }

            }

            if (partActivation.UseNavigation)
            {
                DependencyInit.UseDbNavigation(services);
                /*if (t != null)
                {
                    services.UseDbNavigation(t);
                }
                else
                {
                    services.UseDbNavigation();
                }*/
            }

            if (partActivation.UseSharedAssets)
            {
                DependencyInit.UseDbSharedAssets(services);
                /*if (t != null)
                {
                    services.UseDbSharedAssets(t);
                }
                else
                {
                    services.UseDbSharedAssets();
                }*/
            }

            if (partActivation.UsePlugins)
            {
                DependencyInit.UseDbPlugins(services, partActivation.PluginBufferDuration);
                //services.UseDbPlugins(partActivation.PluginBufferDuration);
            }

            if (partActivation.UseGlobalSettings)
            {
                services.UseDbGlobalSettings();
            }

            if (partActivation.UseTenantSettings)
            {
                DependencyInit.UseTenantSettings(services);
                //services.UseTenantSettings();
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

        [HealthCheckRegistration]
        public static void RegisterHealthChecks(IHealthChecksBuilder builder,
            [WebPartConfig("ActivationSettings")] ActivationOptions partActivation)
        {
            if (partActivation.UseHealthChecks)
            {
                foreach (var item in partActivation.HealthChecks)
                {
                    bool apply = true;
                    if (!string.IsNullOrEmpty(item.UseExpression) && item.ConditionVariables != null)
                    {
                        apply = (bool)ExpressionParser.Parse(item.UseExpression, item.ConditionVariables);
                    }

                    if (apply)
                    {
                        builder.AddScriptedCheck(item.Label);
                    }
                }
            }
        }

        [CustomConfigurator(typeof(DbContextOptionsBuilder))]
        public static void ConfigureDbInterceptors(DbContextOptionsBuilder optionsBuilder, IServiceProvider services,
            [WebPartConfig("ActivationSettings")] ActivationOptions partOptions)
        {

            if (partOptions.UseDefaultInterceptors)
            {
                optionsBuilder.AddInterceptors(
                    new SecurityModificationInterceptor<Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole>(services));
            }
        }
    }
}
