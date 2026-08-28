using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity.Extensions
{
    public static class DependencyInjectionExtensions
    {
        /// <summary>
        /// Registers the tenant-security-backed <see cref="IFeatureProvisioner"/> so billing subscription
        /// changes drive <c>TenantFeatureActivation</c> rows. Use the activation type matching the host's
        /// security strategy (e.g. <c>FlatTenantFeatureActivation</c> / <c>HierarchyTenantFeatureActivation</c>).
        /// Scoped, like the host DbContext.
        /// </summary>
        public static IServiceCollection AddBillingFeatureProvisioner<TContext, TTenant, TActivation>(this IServiceCollection services)
            where TContext : DbContext
            where TTenant : Tenant
            where TActivation : TenantFeatureActivation<TTenant>, new()
        {
            services.AddScoped<IFeatureProvisioner, BillingFeatureProvisioner<TContext, TTenant, TActivation>>();
            return services;
        }

        /// <summary>
        /// Registers the tenant-security-backed <see cref="IPaymentFeatureGate"/>, which decides per TENANT ID
        /// whether that tenant may receive payments from its own end customers.
        /// <para>
        /// Without this registration the payments service refuses every sale — fail-closed by design. Use the
        /// activation type matching the host's security strategy, the same one passed to
        /// <c>AddBillingFeatureProvisioner</c>.
        /// </para>
        /// </summary>
        public static IServiceCollection AddPaymentFeatureGate<TContext, TTenant, TActivation>(this IServiceCollection services)
            where TContext : DbContext
            where TTenant : Tenant
            where TActivation : TenantFeatureActivation<TTenant>, new()
        {
            services.AddScoped<IPaymentFeatureGate, PaymentFeatureGate<TContext, TTenant, TActivation>>();
            return services;
        }
    }
}
