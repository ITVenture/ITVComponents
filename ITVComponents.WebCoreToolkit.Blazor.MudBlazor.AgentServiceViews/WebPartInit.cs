using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AgentServiceViews
{
    /// <summary>
    /// Hängt den Reiter „Devices" an die Detailzeile der ClientApps.
    /// </summary>
    /// <remarks>
    /// Die Dienste dahinter (<c>IAgentServiceAdministration</c>) registriert das AgentServices-WebPart. Fehlen
    /// sie, scheitert der Reiter beim Öffnen mit einer Meldung — nicht die ganze Seite.
    /// </remarks>
    [WebPart]
    public static class WebPartInit
    {
        /// <summary>Registriert den Reiter.</summary>
        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services)
        {
            services.AddMudBlazorAgentServiceViews();
        }

        /// <summary>
        /// Registriert den Reiter „Devices" in der Detailzeile der ClientApps — für Hosts ohne WebPart-Lader.
        /// </summary>
        public static IServiceCollection AddMudBlazorAgentServiceViews(this IServiceCollection services)
        {
            if (services.All(d => d.ImplementationInstance is not AdminViews.TenantSecurityViews.Handlers.ClientAppDetailTabDescriptor t
                                  || t.ComponentType != typeof(ClientAppServiceSlots)))
            {
                services.AddClientAppDetailTab<ClientAppServiceSlots>("Tab_Devices", typeof(AgentServiceMessages));
            }

            return services;
        }
    }
}
