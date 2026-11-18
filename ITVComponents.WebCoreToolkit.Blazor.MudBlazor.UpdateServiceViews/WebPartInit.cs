using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.UpdateServiceViews
{
    /// <summary>
    /// Adds the tab "Updates" to the detail row of the client apps. The page of the releases is routable
    /// (<c>/Connectivity/Updates</c>) and needs no registration.
    /// </summary>
    /// <remarks>
    /// The handler behind both (<c>IUpdateAdminHandler</c>) is registered by the host with
    /// <c>AddUpdateServiceAdministration</c>. Without it the tab fails with a message when opened - not the whole page.
    /// </remarks>
    [WebPart]
    public static class WebPartInit
    {
        /// <summary>Registers the tab.</summary>
        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services)
        {
            services.AddMudBlazorUpdateServiceViews();
        }

        /// <summary>
        /// Registers the tab "Updates" in the detail row of the client apps - for hosts without web part loader.
        /// </summary>
        public static IServiceCollection AddMudBlazorUpdateServiceViews(this IServiceCollection services)
        {
            if (services.All(d => d.ImplementationInstance is not AdminViews.TenantSecurityViews.Handlers.ClientAppDetailTabDescriptor t
                                  || t.ComponentType != typeof(ClientAppUpdates)))
            {
                services.AddClientAppDetailTab<ClientAppUpdates>("Tab_Updates", typeof(UpdateServiceMessages));
            }

            return services;
        }
    }
}
