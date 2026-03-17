using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;

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
    }
}
