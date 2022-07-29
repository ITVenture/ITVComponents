using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.Net.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.OpenApi;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static NetUiPartOptions LoadOptions(IConfiguration config, string path)
        {
            return config.GetSection<NetUiPartOptions>(path);
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, NetUiPartOptions options)
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
                });
            }
        }

        [MvcRegistrationMethod]
        public static void RegisterTenantViewAssemblyPart(ApplicationPartManager manager, NetUiPartOptions options)
        {
            if (options.UseViews)
            {
                manager.EnableItvExtensionViews();
            }
        }

        [EndpointRegistrationMethod]
        public static void RegisterTenantViewAssemblyPart(WebApplication builder, EndPointTrunk endPointRegistry, NetUiPartOptions options)
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
            }
        }

        private static void Register(WebApplication builder, string tenantParam, bool useAreas, bool useAuth, bool useFilteredForeignKeys, EndPointTrunk endPointRegistry)
        {
            if (useFilteredForeignKeys)
            {
                endPointRegistry.Register(new OpenApiDescriptor(builder.UseFilteredAutoForeignKeys(tenantParam, useAreas, useAuth),
                    "FilteredForeignKeys", "Kendo-UI compilant filterable ForeignKey-Api",
                    produces: r => r.Output<DummyDataSourceResult>(200,"application/json","A Data-Source result containing the select FK-Data"))) ;
            }
        }
    }
}
