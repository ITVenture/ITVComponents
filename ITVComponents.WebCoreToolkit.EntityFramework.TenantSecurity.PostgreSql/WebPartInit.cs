using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FlatSyntax = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.PostgreSql.SyntaxHelper.PostgreSqlColumnsSyntaxHelper;
using TreeSyntax = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.PostgreSql.SyntaxHelper.PostgreSqlColumnsSyntaxHelper;
using BasicSyntax = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.PostgreSql.SyntaxHelper.PostgreSqlColumnsSyntaxHelper;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.PostgreSql
{
    /// <summary>
    /// Consolidated PostgreSql provider WebPart for the tenant-security package. Replaces the two per-strategy
    /// provider WebPartInit classes (AspNetCoreTenants.PostgreSql, TenantSecurityContext.PostgreSql). One method
    /// per registration aspect; the active combination is selected via <see cref="ActivationOptions.Identity"/>
    /// (CoreIdentity vs. BasicTenantSecurity) and <see cref="ActivationOptions.Strategy"/> (Flat and - seit der
    /// Baum-Umsetzung fuer PostgreSql - auch Tree; die einfache Mandanten-Sicherheit gibt es weiterhin nur flach).
    /// </summary>
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

            if (contextOptions.ConfigureContext)
            {
                Type t = null;
                if (!string.IsNullOrEmpty(contextOptions.ContextType))
                {
                    var dic = new Dictionary<string, object>();
                    t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
                    services.ConfigureMethods(t, bld =>
                    {
                        switch (identity, strategy)
                        {
                            case (IdentityStrategy.CoreIdentity, TenantStrategy.Flat):
                                FlatSyntax.ConfigureMethods(bld);
                                break;
                            case (IdentityStrategy.CoreIdentity, TenantStrategy.Tree):
                                TreeSyntax.ConfigureMethods(bld);
                                TreeSyntax.ConfigureVirtualTables(bld);
                                break;
                            case (IdentityStrategy.BasicTenantSecurity, TenantStrategy.Flat):
                                BasicSyntax.ConfigureMethods(bld);
                                break;
                        }
                    });
                }

                if (!TenantSecurityInitializer.ContextTypeInitialized)
                {
                    TenantSecurityInitializer.SetContextType(t, identity, strategy);
                }
            }

            if (partActivation.ActivateDbContext)
            {
                var manager = sharedObjects.Property<WebPartManager>("WebPartManager").Value;
                TenantSecurityInitializer.DependencyInit.UseDbIdentities(services, (services, options) =>
                {
                    options.UseNpgsql(partActivation.ConnectionStringName);
                    manager.CustomObjectConfig(options, services);
                });
            }
        }
    }
}
