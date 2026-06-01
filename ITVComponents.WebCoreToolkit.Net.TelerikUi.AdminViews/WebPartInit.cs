using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;
using ITVComponents.WebCoreToolkit.Net.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.Hubs;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.OpenApi;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews
{
    [WebPart]
    public static class WebPartInit
    {
        // Consolidated from former TelerikUi (core, "NetUi") + TenantSecurityViews (TSV) packages.
        // All configurations are named now; the former core single/DEFAULT config moves to the "NetUi" key.
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string optionType, string path)
        {
            switch (optionType)
            {
                case "NetUi":
                    return config.GetSection<NetUiPartOptions>(path);
                case "ContextSettings":
                    return config.GetSection<SecurityContextOptions>(path);
                case "ContextActivationSettings":
                    return config.GetSection<ActivationOptions>(path);
                case "ViewConfig":
                    return config.GetSection<SecurityViewsOptions>(path);
            }

            return null;
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services,
            [WebPartConfig("NetUi")] NetUiPartOptions options,
            [WebPartConfig("ViewConfig")] SecurityViewsOptions viewOptions,
            [WebPartConfig("ContextActivationSettings")] ActivationOptions contextActivation)
        {
            //-- core (NetUi) services
            if (options != null)
            {
                if (options.UseValidationAdapters)
                {
                    services.UseValidationAdapters();
                }

                if (options.UseScriptLocalization)
                {
                    services.UseScriptLocalization();
                }

                if (!string.IsNullOrEmpty(options.LayoutPage))
                {
                    services.Configure<ViewOptions>(o =>
                    {
                        o.LayoutPage = options.LayoutPage;
                        o.UseHealthView = options.UseHealthView;
                        o.UseViewsView = options.UseViewsView;
                        o.UseControllerView = options.UseControllerView;
                        o.UseTagHelperView = options.UseTagHelperView;
                        o.UseViewComponentView = options.UseViewComponentView;
                    });
                }
            }

            //-- tenant-security-views services
            if (viewOptions != null)
            {
                bool inheritGroups = contextActivation?.UseRoleInheritance ?? false;
                services.ConfigureTenantViews(o =>
                {
                    o.TenantLinkMode = viewOptions.TenantLinkMode;
                    o.UseExplicitTenantPasswords = viewOptions.UseExplicitTenantPasswords;
                    o.UseRoleInheritance = inheritGroups;
                });
            }
        }

        [MvcRegistrationMethod]
        public static void RegisterTenantViewAssemblyPart(ApplicationPartManager manager,
            [WebPartConfig("NetUi")] NetUiPartOptions options,
            [WebPartConfig("ContextSettings")] SecurityContextOptions contextOptions,
            [WebPartConfig("ViewConfig")] SecurityViewsOptions viewOptions,
            [WebPartConfig(WebCoreToolkit.Global.PartTypeLoadBehaviorOption)] AssemblyPartTypeLoadBehaviorOptions loadingOptions)
        {
            //-- core (NetUi) extension-views (non-generic CompiledRazorAssemblyPart)
            if (options is { UseViews: true })
            {
                manager.EnableItvExtensionViews();
            }

            //-- tenant-security-views (generic AssemblyPartWithGenerics bound to TContext)
            if (contextOptions is { ConfigureContext: true })
            {
                if (!string.IsNullOrEmpty(contextOptions.ContextType))
                {
                    var dic = new Dictionary<string, object>();
                    var t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
                    var customTypes = new Dictionary<string, Type>();
                    if (viewOptions?.CustomViewGenericArgs != null)
                    {
                        foreach (var tmp in viewOptions.CustomViewGenericArgs)
                        {
                            customTypes.Add(tmp.Key, (Type)ExpressionParser.Parse(tmp.Value, dic));
                        }
                    }

                    manager.EnableItvTenantViews(t, customTypes, loadingOptions);
                }
                else
                {
                    throw new InvalidOperationException("Unable to register Views without a Context-Type");
                }
            }
        }

        [EndpointRegistrationMethod]
        public static void RegisterTenantViewAssemblyPart(WebApplication builder,
            [WebPartConfig("NetUi")] NetUiPartOptions options,
            [WebPartConfig("ViewConfig")] SecurityViewsOptions viewOptions,
            [SharedObjectHeap] ISharedObjHeap sharedObjects)
        {
            var endPointRegistry = sharedObjects.Property<EndPointTrunk>(nameof(EndPointTrunk), true).Value;

            //-- core (NetUi) endpoints
            if (options != null)
            {
                if (!string.IsNullOrEmpty(options.TenantParam) && options.WithTenants)
                {
                    if (options.WithAreas && options.WithSecurity)
                    {
                        Register(builder, options.TenantParam, true, true, options.UseFilteredForeignKeys, endPointRegistry);
                    }

                    if (options.WithAreas && options.WithoutSecurity)
                    {
                        Register(builder, options.TenantParam, true, false, options.UseFilteredForeignKeys, endPointRegistry);
                    }

                    if (options.WithoutAreas && options.WithSecurity)
                    {
                        Register(builder, options.TenantParam, false, true, options.UseFilteredForeignKeys, endPointRegistry);
                    }

                    if (options.WithoutAreas && options.WithoutSecurity)
                    {
                        Register(builder, options.TenantParam, false, false, options.UseFilteredForeignKeys, endPointRegistry);
                    }

                    if (options.RegisterHub)
                    {
                        builder.MapHub<GlobalNotificationHub>(
                            $"/{{{options.TenantParam}:permissionScope}}/Util/GlobalNotificationHub");
                    }
                }

                if (options.WithoutTenants)
                {
                    if (options.WithAreas && options.WithSecurity)
                    {
                        Register(builder, null, true, true, options.UseFilteredForeignKeys, endPointRegistry);
                    }

                    if (options.WithAreas && options.WithoutSecurity)
                    {
                        Register(builder, null, true, false, options.UseFilteredForeignKeys, endPointRegistry);
                    }

                    if (options.WithoutAreas && options.WithSecurity)
                    {
                        Register(builder, null, false, true, options.UseFilteredForeignKeys, endPointRegistry);
                    }

                    if (options.WithoutAreas && options.WithoutSecurity)
                    {
                        Register(builder, null, false, false, options.UseFilteredForeignKeys, endPointRegistry);
                    }

                    if (options.RegisterHub)
                    {
                        builder.MapHub<GlobalNotificationHub>(
                            "/Util/GlobalNotificationHub");
                    }
                }
            }

            //-- tenant-security-views feature-module endpoints
            if (viewOptions is { UseModuleTemplates: true })
            {
                if (!string.IsNullOrEmpty(viewOptions.TenantParam) && viewOptions.WithTenants)
                {
                    if (viewOptions.WithAreas)
                    {
                        Register(builder, viewOptions.TenantParam, true, endPointRegistry);
                    }

                    if (viewOptions.WithoutAreas)
                    {
                        Register(builder, viewOptions.TenantParam, false, endPointRegistry);
                    }
                }

                if (viewOptions.WithoutTenants)
                {
                    if (viewOptions.WithAreas)
                    {
                        Register(builder, null, true, endPointRegistry);
                    }

                    if (viewOptions.WithoutAreas)
                    {
                        Register(builder, null, false, endPointRegistry);
                    }
                }
            }
        }

        //-- core (NetUi) filtered-foreign-keys endpoint
        private static void Register(WebApplication builder, string tenantParam, bool useAreas, bool useAuth, bool useFilteredForeignKeys, EndPointTrunk endPointRegistry)
        {
            if (useFilteredForeignKeys)
            {
                endPointRegistry.Register(new OpenApiDescriptor(builder.UseFilteredAutoForeignKeys(tenantParam, useAreas, useAuth),
                    "FilteredForeignKeys", "Kendo-UI compilant filterable ForeignKey-Api",
                    produces: r => r.Output<DummyDataSourceResult>(200, "application/json", "A Data-Source result containing the select FK-Data")));
            }
        }

        //-- tenant-security-views feature-module-templates endpoints
        private static void Register(WebApplication builder, string tenantParam, bool useAreas, EndPointTrunk endPointRegistry)
        {
            builder.UseFeatureModules(tenantParam, useAreas, out var getter, out var setter);
            endPointRegistry.Register(new OpenApiDescriptor(getter, "FeatureModuleTemplates",
                "Reads the current Tenant configuration for a specific FeatureModule",
                produces: r => r.Output<Dictionary<string, object>>(200, "application/json",
                    "An object containing all component specific configurations depending on the given module name")));
            endPointRegistry.Register(new OpenApiDescriptor(setter, "FeatureModuleTemplates",
                "Stores the current Tenant configuration for a specific FeatureModule",
                produces: r => r.Output<DummyDataSourceResult>(200, "plain/text",
                    "A simple string indicating whether the configuration was saved successfully")));
        }
    }
}
