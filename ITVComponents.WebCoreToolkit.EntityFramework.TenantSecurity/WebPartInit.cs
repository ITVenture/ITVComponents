using System;
using System.Collections.Generic;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.Cookies;
using ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Health;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Localization;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.TemplateHandling;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.ConfigMarkupModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedExt = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions.DependencyExtensions;
using TreeExt = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Extensions.DependencyExtensions;
using HealthCheckExtensions = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions.HealthCheckExtensions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity
{
    /// <summary>
    /// Consolidated WebPart for the tenant-security package. Replaces the five per-strategy WebPartInit classes
    /// (TenantSecurityShared, TenantTreeShared, AspNetCoreTenants, AspNetCoreTreeTenants, TenantSecurityContext).
    /// One method per registration aspect; the active combination is selected via
    /// <see cref="ActivationOptions.Identity"/> (CoreIdentity vs. BasicTenantSecurity) and
    /// <see cref="ActivationOptions.Strategy"/> (Flat vs. Tree).
    /// </summary>
    [WebPart]
    public static class WebPartInit
    {
        static WebPartInit()
        {
            // Tree-capable protocol extensions (formerly registered by TreeShared.WebPartInit's static ctor).
            // Harmless when no tree config is present; the "treeCapable" markers only activate on tree configs.
            JsonHelper.ExtendNativeProtocolType<PlugInTemplateMarkup, HierarchyPlugInTemplateMarkup>("treeCapable");
            JsonHelper.ExtendNativeProtocolType<ConstTemplateMarkup, HierarchyWebPluginConstantTemplateMarkup>("treeCapable");
            JsonHelper.ExtendNativeProtocolType<SettingTemplateMarkup, HierarchyTenantSettingTemplateMarkup>("treeCapable");
            JsonHelper.ExtendNativeProtocolType<ExternalOAuthServiceTemplateMarkup, HierarchyExternalOAuthServiceTemplateMarkup>("treeCapable");
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

            return null;
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services,
            [WebPartConfig("ContextSettings")] SecurityContextOptions contextOptions,
            [WebPartConfig("ActivationSettings")] ActivationOptions partActivation,
            [SharedObjectHeap] ISharedObjHeap sharedObjects)
        {
            var identity = partActivation.Identity;
            var strategy = partActivation.Strategy;
            bool tree = strategy == TenantStrategy.Tree;

            Type t = null;
            if (contextOptions.ConfigureContext && !string.IsNullOrEmpty(contextOptions.ContextType))
            {
                var dic = new Dictionary<string, object>();
                t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
            }

            // Wire the context type for the active combination (provider package may already have done this).
            if (contextOptions.ConfigureContext && !TenantSecurityInitializer.ContextTypeInitialized)
            {
                TenantSecurityInitializer.SetContextType(t, identity, strategy);
            }

            // --- Strategy-independent shared services ---
            if (partActivation.ActivateTemplateFactory)
            {
                services.AddScoped<ITemplateHandlerFactory, TemplateHandlerFactory>();
            }

            if (contextOptions.ConfigureContext)
            {
                if (partActivation.ActivateFilters && t != null)
                {
                    if (tree) TreeExt.ConfigureGlobalFilters(services, t); else SharedExt.ConfigureGlobalFilters(services, t);
                }

                if (partActivation.ActivateDefaultContextUserProvider && t != null)
                {
                    if (tree) TreeExt.ConfigureDefaultContextUserProvider(services, t); else SharedExt.ConfigureDefaultContextUserProvider(services, t);
                }
            }

            if (partActivation.UseContextLocalizationServices)
            {
                services.AddSingleton<IStringLocalizerFactory>(sp =>
                    new ContextLocalizerFactory(sp, sp.GetService<IOptions<LocalizationOptions>>(),
                        sp.GetService<ILoggerFactory>()));
            }

            if (partActivation.UseServerCookies)
            {
                Action<ServerCookieOptions> cookieConfig = o =>
                {
                    o.DefaultCookieValidDays = partActivation.DefaultServerCookieValidity;
                    o.CookieLengthThreshold = partActivation.CookieLengthThreshold;
                };
                if (tree) TreeExt.UseServerCookies(services, cookieConfig); else SharedExt.UseServerCookies(services, cookieConfig);
            }

            if (partActivation.UseDefaultSecurityAccessProvider)
            {
                if (tree) TreeExt.UseDefaultSecurityAccessProvider(services); else SharedExt.UseDefaultSecurityAccessProvider(services);
            }

            // --- DbContext wiring ---
            if (partActivation.ActivateDbContext && partActivation.UseApplicationIdentitySchema)
            {
                var l = sharedObjects.Property<List<string>>("SignInSchemes", true);
                l.Value.AddIfMissing(IdentityConstants.ApplicationScheme, true);
            }

            var init = TenantSecurityInitializer.DependencyInit;

            if (partActivation.UseNavigation)
            {
                init.UseDbNavigation(services);
            }

            if (partActivation.UseSharedAssets)
            {
                init.UseDbSharedAssets(services);
            }

            if (partActivation.UsePlugins)
            {
                init.UseDbPlugins(services, partActivation.PluginBufferDuration);
            }

            if (partActivation.UseGlobalSettings)
            {
                if (tree) TreeExt.UseDbGlobalSettings(services); else SharedExt.UseDbGlobalSettings(services);
            }

            if (partActivation.UseTenantSettings)
            {
                init.UseTenantSettings(services);
            }

            if (partActivation.UseLogAdapter)
            {
                if (tree) TreeExt.UseDbLogAdapter(services); else SharedExt.UseDbLogAdapter(services);
            }

            if (partActivation.UseApplicationTokens)
            {
                init.UseApplicationTokenService(services);
            }

            if (partActivation.UseEntityTracker)
            {
                services.AddSingleton(typeof(IEntityWriteTracker<>), typeof(EntityWriteTracker<>));
                init?.UseEntityChangeSignal(services);
            }
        }

        [HealthCheckRegistration]
        public static void RegisterHealthChecks(IHealthChecksBuilder builder,
            [WebPartConfig("ActivationSettings")] ActivationOptions partActivation)
        {
            if (!partActivation.UseHealthChecks)
            {
                return;
            }

            bool tree = partActivation.Strategy == TenantStrategy.Tree;
            foreach (var item in partActivation.HealthChecks)
            {
                bool apply = true;
                if (!string.IsNullOrEmpty(item.UseExpression) && item.ConditionVariables != null)
                {
                    apply = (bool)ExpressionParser.Parse(item.UseExpression, item.ConditionVariables);
                }

                if (!apply)
                {
                    continue;
                }

                if (tree)
                {
                    HealthCheckExtensions.AddScriptedCheck<ScriptedHealthCheck<TreeShared.Helpers.Models.HierarchyTenantContextSecurityTrustConfig>,
                        TreeShared.Helpers.Models.HierarchyTenantContextSecurityTrustConfig>(builder, item.Label);
                }
                else
                {
                    HealthCheckExtensions.AddScriptedCheck<ScriptedHealthCheck<BaseTenantContextSecurityTrustConfig>,
                        BaseTenantContextSecurityTrustConfig>(builder, item.Label);
                }
            }
        }

        [CustomConfigurator(typeof(DbContextOptionsBuilder))]
        public static void ConfigureDbInterceptors(DbContextOptionsBuilder optionsBuilder, IServiceProvider services,
            [WebPartConfig("ActivationSettings")] ActivationOptions partOptions)
        {
            if (partOptions.UseEntityTracker)
            {
                optionsBuilder.AddEntityWriteTrackerInterceptor(services);
            }

            if (!partOptions.UseDefaultInterceptors)
            {
                return;
            }

            switch (partOptions.Identity, partOptions.Strategy)
            {
                case (IdentityStrategy.CoreIdentity, TenantStrategy.Flat):
                    optionsBuilder.AddInterceptors(
                        new Shared.Interceptors.SecurityModificationInterceptor<
                            Shared.Models.Tenant, string, CoreIdentity.Models.User, CoreIdentity.Models.Role,
                            CoreIdentity.Models.Permission, CoreIdentity.Models.UserRole, CoreIdentity.Models.RolePermission,
                            CoreIdentity.Models.TenantUser, CoreIdentity.Models.RoleRole, CoreIdentity.Models.GlobalRole,
                            CoreIdentity.Models.GlobalRolePermission, CoreIdentity.Models.GRoleLRole>(services));
                    break;
                case (IdentityStrategy.CoreIdentity, TenantStrategy.Tree):
                    optionsBuilder.AddInterceptors(
                        new TreeShared.Interceptors.SecurityModificationInterceptor<
                            TreeShared.Models.HierarchyTenant, string, CoreIdentityTree.Model.User, CoreIdentityTree.Model.Role,
                            CoreIdentityTree.Model.Permission, CoreIdentityTree.Model.UserRole, CoreIdentityTree.Model.RolePermission,
                            CoreIdentityTree.Model.HierarchyTenantUser, CoreIdentityTree.Model.RoleRole, CoreIdentityTree.Model.GlobalRole,
                            CoreIdentityTree.Model.GlobalRolePermission, CoreIdentityTree.Model.GRoleLRole>(services));
                    break;
                case (IdentityStrategy.BasicTenantSecurity, TenantStrategy.Flat):
                    optionsBuilder.AddInterceptors(
                        new Shared.Interceptors.SecurityModificationInterceptor<
                            Shared.Models.Tenant, int, Basic.Models.User, Basic.Models.Role,
                            Basic.Models.Permission, Basic.Models.UserRole, Basic.Models.RolePermission,
                            Basic.Models.TenantUser, Basic.Models.RoleRole, Basic.Models.GlobalRole,
                            Basic.Models.GlobalRolePermission, Basic.Models.GRoleLRole>(services));
                    break;
            }
        }
    }
}
