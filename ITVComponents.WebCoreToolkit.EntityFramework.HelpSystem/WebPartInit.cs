using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem
{
    // Opt-in WebPart that contributes the help system to the downloadable system configuration. Deliberately kept
    // in the storage library rather than the Blazor help views, so the export section is available even on a host
    // that renders its help elsewhere. Flag-gated: a host that does not want its documentation travelling with the
    // system config just leaves ActivateHelpConfigExport off.
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string path)
            => config.GetSection<HelpConfigExportPartOptions>(path) ?? new HelpConfigExportPartOptions();

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, [WebPartConfig] HelpConfigExportPartOptions? options)
        {
            if (options is { ActivateHelpConfigExport: true })
            {
                services.AddHelpConfigExtension(options.Contents);
            }
        }
    }
}
