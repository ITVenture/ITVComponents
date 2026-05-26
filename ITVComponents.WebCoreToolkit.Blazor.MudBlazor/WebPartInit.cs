using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazorLib.Config;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazorLib
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object? LoadOptions(IConfiguration config, string path)
        {
            return config.GetSection<WebPartConfig>(path);
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, [WebPartConfig]WebPartConfig config)
        {
            if (config.UseViews)
            {
                services.AddMudBlazorDiagnostics();
            }
        }
    }
}
