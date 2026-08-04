using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazorLib.Config;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents;
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
            services.AddToolkitForeignKeyCache();

            // Bewusst NICHT an UseViews gebunden: die Regeln darin betreffen Mud-Dialoge ueberhaupt,
            // also auch die, die ein Konsument selbst baut. Ohne sie scrollt kein Dialog-Inhalt, und
            // bei einem langen Formular liegt die Aktionsleiste ausserhalb des Bildes.
            services.AddToolkitClientStyleSheet(
                "_content/ITVComponents.WebCoreToolkit.Blazor.MudBlazor/itv-mudblazor.css");

            if (config.UseViews)
            {
                services.AddMudBlazorDiagnostics();
            }
        }
    }
}
