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
    }
}
