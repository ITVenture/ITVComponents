using ITVComponents.EFRepo.DataSync;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Configuration
{
    public static class BillingConfigExtensionRegistration
    {
        /// <summary>
        /// Registers the billing catalog as a system-config export section. Call at startup (host wiring) so the
        /// billing plans/add-ons round-trip in the downloadable system configuration. Requires a config-handler
        /// host (TenantSecurity) whose context also implements <c>IBillingContext</c>.
        /// </summary>
        public static IServiceCollection AddBillingConfigExtension(this IServiceCollection services)
            => services.AddSystemConfigExtension<BillingConfigMarkup>();
    }
}
