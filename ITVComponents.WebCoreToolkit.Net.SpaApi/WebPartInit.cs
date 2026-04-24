using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData.Extensions;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Extensions;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Handlers.Model;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static SecurityContextOptions LoadOptions(IConfiguration config, string path)
        {
            return config.GetSection<SecurityContextOptions>(path);
        }

        private static void Register(WebApplication builder, string tenantParam, bool useAreas, bool useAuth, bool useFilteredForeignKeys, EndPointTrunk endPointRegistry)
        {
            if (useFilteredForeignKeys)
            {
                endPointRegistry.Register(new OpenApiDescriptor(builder.UseFilteredAutoForeignKeys(tenantParam, useAreas, useAuth),
                    "FilteredForeignKeys", "Kendo-UI compilant filterable ForeignKey-Api",
                    produces: r => r.Output<DataResult>(200, "application/json", "A Data-Source result containing the select FK-Data")));
            }
        }
    }
}
