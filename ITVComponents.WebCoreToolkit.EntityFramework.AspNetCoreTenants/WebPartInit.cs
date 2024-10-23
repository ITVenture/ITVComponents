using System;
using System.Collections.Generic;
using ITVComponents.DuckTyping.Extensions;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Helpers.Initialization;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Interceptors;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DependencyExtensions = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Extensions.DependencyExtensions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants
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

            var t = contextType ?? typeof(AspNetSecurityContext);
            (string name, Type type)[] fxparam = [(name: "TContext", type: t), (name: "TImpl", type: t)];
            init = typeof(DependencyExtensions).WrapType<IDependencyInitializer>(t, fixParameters: fxparam);
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
            if (!ContextTypeInitialized)
            {
                Type t = null;
                if (!string.IsNullOrEmpty(contextOptions.ContextType))
                {
                    var dic = new Dictionary<string, object>();
                    t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
                    SetContextType(t);
                }
                else
                {
                    SetContextType(null);
                }
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
            }

            if (partActivation.UseSharedAssets)
            {
                DependencyInit.UseDbSharedAssets(services);
            }

            if (partActivation.UsePlugins)
            {
                DependencyInit.UseDbPlugins(services, partActivation.PluginBufferDuration);
            }

            if (partActivation.UseGlobalSettings)
            {
                services.UseDbGlobalSettings();
            }

            if (partActivation.UseTenantSettings)
            {
                DependencyInit.UseTenantSettings(services);
            }

            if (partActivation.UseLogAdapter)
            {
                services.UseDbLogAdapter();
            }

            if (partActivation.UseApplicationTokens)
            {
                DependencyInit.UseApplicationTokenService(services);
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
                    new SecurityModificationInterceptor<Tenant, string, User, Role, Permission, UserRole, RolePermission,
                        TenantUser, RoleRole>(services));
            }
        }
    }
}
