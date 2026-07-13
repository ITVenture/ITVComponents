using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing
{
    // Opt-in WebPart that contributes the billing catalog to the downloadable system configuration. Deliberately
    // kept in the provider-neutral billing library (not the Stripe part) so the config-export section is available
    // regardless of which billing provider is wired, and independent of the Blazor billing views. Flag-gated:
    // a host that does not want billing in its system-config export just leaves ActivateBillingConfigExport off.
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string path)
            => config.GetSection<BillingConfigExportPartOptions>(path) ?? new BillingConfigExportPartOptions();

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, [WebPartConfig] BillingConfigExportPartOptions? options)
        {
            if (options is { ActivateBillingConfigExport: true })
            {
                services.AddBillingConfigExtension();
            }
        }
    }
}
