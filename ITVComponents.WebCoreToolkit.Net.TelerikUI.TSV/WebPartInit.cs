using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string key, string path)
        {
            if (key == "ContextSettings")
            {
                return config.GetSection<SecurityContextOptions>(path);
            }
            else if (key == "ContextActivationSettings")
            {
                return config.GetSection<ActivationOptions>(path);
            }
            else if (key == "ViewConfig")
            {
                return config.GetSection<SecurityViewsOptions>(path);
            }

            return null;
        }

        [MvcRegistrationMethod]
        public static void RegisterTenantViewAssemblyPart(ApplicationPartManager manager, [WebPartConfig("ContextSettings")] SecurityContextOptions options)
        {
            if (!string.IsNullOrEmpty(options?.ContextType))
            {
                var dic = new Dictionary<string, object>();
                var t = (Type)ExpressionParser.Parse(options.ContextType, dic);
                manager.EnableItvTenantViews(t);
            }
            else
            {
                throw new InvalidOperationException("Unable to register Views without a Context-Type");
            }
        }
        
        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services,
            [WebPartConfig("ViewConfig")] SecurityViewsOptions viewOptions,
            [WebPartConfig("ContextActivationSettings")] ActivationOptions contextActivation)
        {
            bool inheritGroups = contextActivation?.UseRoleInheritance ?? false;
            if (viewOptions != null)
            {
                services.ConfigureTenantViews(o =>
                {
                    o.TenantLinkMode = viewOptions.TenantLinkMode;
                    o.UseExplicitTenantPasswords = viewOptions.UseExplicitTenantPasswords;
                    o.UseRoleInheritance = inheritGroups;
                });
            }
        }

        [EndpointRegistrationMethod]
        public static void RegisterTenantViewAssemblyPart(WebApplication builder,
            [WebPartConfig("ViewConfig")] SecurityViewsOptions options, [SharedObjectHeap] ISharedObjHeap sharedObjects)
        {
            if (options.UseModuleTemplates)
            {
                var endPointRegistry = sharedObjects.Property<EndPointTrunk>(nameof(EndPointTrunk), true).Value;
                if (!string.IsNullOrEmpty(options.TenantParam) && options.WithTenants)
                {
                    if (options.WithAreas)
                    {
                        Register(builder, options.TenantParam, true, endPointRegistry);
                    }

                    if (options.WithoutAreas)
                    {
                        Register(builder, options.TenantParam, false, endPointRegistry);
                    }
                }

                if (options.WithoutTenants)
                {
                    if (options.WithAreas)
                    {
                        Register(builder, null, true, endPointRegistry);
                    }

                    if (options.WithoutAreas)
                    {
                        Register(builder, null, false, endPointRegistry);
                    }
                }
            }
        }

        private static void Register(WebApplication builder, string tenantParam, bool useAreas, EndPointTrunk endPointRegistry)
        {
            builder.UseFeatureModules(tenantParam, useAreas, out var getter, out var setter);
            endPointRegistry.Register(new OpenApiDescriptor(getter, "FeatureModuleTemplates",
                "Reads the current Tenant configuration for a specific FeatureModule",
                produces: r => r.Output<Dictionary<string,object>>(200, "application/json",
                    "An object containing all component specific configurations depending on the given module name")));
            endPointRegistry.Register(new OpenApiDescriptor(setter, "FeatureModuleTemplates",
                "Stores the current Tenant configuration for a specific FeatureModule",
                produces: r => r.Output<DummyDataSourceResult>(200, "plain/text",
                    "A simple string indicating whether the configuration was saved successfully")));
        }
    }
}
