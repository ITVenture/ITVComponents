using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.Net.Extensions;
using ITVComponents.WebCoreToolkit.Net.Options;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static NetPartOptions LoadOptions(IConfiguration config, string path)
        {
            var ret = config.GetSection<NetPartOptions>(path);
            config.RefResolve(ret);
            if (!string.IsNullOrEmpty(ret.UrlQueryExtVersion))
            {
                AppVersionHelper.SetVersion(ret.UrlQueryExtVersion);
            }

            return ret;
        }

        [ServiceRegistrationMethod]
        public static void RegisterNetServiceConfigurations(IServiceCollection services, NetPartOptions options)
        {
            services.Configure<NetFileLinkOptions>(n => n.FileTokenAsQuery = options.UseFileTokenFromQuery);
        }

        [EndpointRegistrationMethod]
        public static void RegisterNetDefaultEndPoints(WebApplication builder, [WebPartConfig]NetPartOptions options, [SharedObjectHeap]ISharedObjHeap sharedObjects)
        {
            var endPointRegistry = sharedObjects.Property<EndPointTrunk>(nameof(EndPointTrunk), true).Value;
            if (!string.IsNullOrEmpty(options.TenantParam) && options.WithTenants)
            {
                if (options.WithAreas && options.WithSecurity)
                {
                    Register(builder, options.TenantParam, true, true, options.UseAutoForeignKeys, options.UseDiagnostics,
                        options.UseWidgets, options.ExposeFileSystem, options.ExposeClientSettings,
                        options.UseFileServices,
                        options.UseFileTokenFromQuery,
                        options.UseTenantSwitch,
                        options.ExposeUserPermissions,
                        options.ExposeTenantFeatures,
                        endPointRegistry);
                }

                if (options.WithAreas && options.WithoutSecurity)
                {
                    Register(builder, options.TenantParam, true, false, options.UseAutoForeignKeys, options.UseDiagnostics,
                        options.UseWidgets, options.ExposeFileSystem, options.ExposeClientSettings,
                        options.UseFileServices,
                        options.UseFileTokenFromQuery,
                        options.UseTenantSwitch,
                        false,false,
                        endPointRegistry);
                }

                if (options.WithoutAreas && options.WithSecurity)
                {
                    Register(builder, options.TenantParam, false, true, options.UseAutoForeignKeys, options.UseDiagnostics,
                        options.UseWidgets, options.ExposeFileSystem, options.ExposeClientSettings,
                        options.UseFileServices,
                        options.UseFileTokenFromQuery,
                        options.UseTenantSwitch,
                        options.ExposeUserPermissions,
                        options.ExposeTenantFeatures,
                        endPointRegistry);
                }

                if (options.WithoutAreas && options.WithoutSecurity)
                {
                    Register(builder, options.TenantParam, false, false, options.UseAutoForeignKeys, options.UseDiagnostics,
                        options.UseWidgets, options.ExposeFileSystem, options.ExposeClientSettings,
                        options.UseFileServices,
                        options.UseFileTokenFromQuery,
                        options.UseTenantSwitch,
                        false,false,
                        endPointRegistry);
                }
            }

            if (options.WithoutTenants)
            {
                if (options.WithAreas && options.WithSecurity)
                {
                    Register(builder, null, true, true, options.UseAutoForeignKeys, options.UseDiagnostics,
                        options.UseWidgets, options.ExposeFileSystem, options.ExposeClientSettings,
                        options.UseFileServices,
                        options.UseFileTokenFromQuery,
                        options.UseTenantSwitch,
                        options.ExposeUserPermissions,
                        options.ExposeTenantFeatures,
                        endPointRegistry);
                }

                if (options.WithAreas && options.WithoutSecurity)
                {
                    Register(builder, null, true, false, options.UseAutoForeignKeys, options.UseDiagnostics,
                        options.UseWidgets, options.ExposeFileSystem, options.ExposeClientSettings,
                        options.UseFileServices,
                        options.UseFileTokenFromQuery,
                        options.UseTenantSwitch,
                        false,false,
                        endPointRegistry);
                }

                if (options.WithoutAreas && options.WithSecurity)
                {
                    Register(builder, null, false, true, options.UseAutoForeignKeys, options.UseDiagnostics,
                        options.UseWidgets, options.ExposeFileSystem, options.ExposeClientSettings,
                        options.UseFileServices,
                        options.UseFileTokenFromQuery,
                        options.UseTenantSwitch,
                        options.ExposeUserPermissions,
                        options.ExposeTenantFeatures,
                        endPointRegistry);
                }

                if (options.WithoutAreas && options.WithoutSecurity)
                {
                    Register(builder, null, false, false, options.UseAutoForeignKeys, options.UseDiagnostics,
                        options.UseWidgets, options.ExposeFileSystem, options.ExposeClientSettings,
                        options.UseFileServices,
                        options.UseFileTokenFromQuery,
                        options.UseTenantSwitch,
                        false,false,
                        endPointRegistry);
                }
            }
        }

        private static void Register(WebApplication builder, string tenantParam, bool useAreas, bool useAuth, bool useAutoForeignKeys, bool useDiagnostics, bool useWidgets, bool exposeFileSystem, bool exposeClientSettings, bool useFileServices, bool useFileTokenFromQuery, bool useTenantSwitch, bool exposeUserPermissions, bool exposeTenantFeatures, EndPointTrunk endPointRegistry)
        {
            if (useAutoForeignKeys)
            {
                builder.UseAutoForeignKeys(tenantParam, useAreas, useAuth);
            }

            if (useDiagnostics)
            {
                builder.UseDiagnostics(tenantParam, useAreas, useAuth);
            }

            if (useWidgets && !useAreas)
            {
                builder.UseWidgets(tenantParam, out var getAction, out var postAction, useAuth);
            }

            if (exposeFileSystem && string.IsNullOrEmpty(tenantParam) && !useAreas)
            {
                builder.ExposeFileSystem(useAuth);
            }

            if (exposeClientSettings && !useAreas)
            {
                builder.ExposeClientSettings(tenantParam, useAuth);
            }

            if (exposeUserPermissions && !useAreas && useAuth)
            {
                builder.ExposeUserPermissions(tenantParam);
            }

            if (exposeTenantFeatures && !useAreas && useAuth)
            {
                builder.ExposeTenantFeatures(tenantParam);
            }

            if (useFileServices && !useAreas)
            {
                builder.UseFileServices(tenantParam, useAuth, useFileTokenFromQuery);
            }

            if (useTenantSwitch && string.IsNullOrEmpty(tenantParam) && !useAreas && useAuth)
            {
                builder.UseTenantSwitch();
            }
        }
    }
}
