using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using Onboarding = ITVComponents.WebCoreToolkit.EntityFramework.Onboarding;
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

        /// <summary>
        /// Registers the billing-profile-backed <see cref="ITenantIdentityProvider"/> for a FLAT tenant model,
        /// so the payout onboarding hands the provider what the tenant already entered - company name, address,
        /// telephone number, and the owner as the responsible person.
        /// <para>
        /// Optional, and nothing breaks without it: an unregistered provider simply means the account is created
        /// from the payout tab alone and the provider's own form asks for the rest. What it saves is the tenant
        /// typing their address a second time, into a form in a different language.
        /// </para>
        /// <para>
        /// <typeparamref name="TContext"/> needs an <see cref="IDbContextFactory{TContext}"/> registered: the
        /// provider opens its own context per call, because it is asked from a Blazor circuit that already has
        /// one in flight.
        /// </para>
        /// </summary>
        public static IServiceCollection AddBillingProfileIdentity<TContext>(this IServiceCollection services)
            where TContext : DbContext, Onboarding.Flat.ISecurityContextWithOnboarding
        {
            services.AddScoped<ITenantIdentityProvider, BillingProfileIdentityProvider<TContext>>();
            return services;
        }

        /// <summary>
        /// Dasselbe fuer das hierarchische Mandantenmodell - siehe
        /// <see cref="AddBillingProfileIdentity{TContext}"/>. Zwei Methoden statt einer Erkennung zur Laufzeit:
        /// welche Auspraegung ein Host faehrt, weiss er selbst, und ein falsch geratener Kontext faellt sonst
        /// erst beim ersten Kontoanlegen auf.
        /// </summary>
        public static IServiceCollection AddHierarchyBillingProfileIdentity<TContext>(this IServiceCollection services)
            where TContext : DbContext, Onboarding.Tree.IHierarchySecurityContextWithOnboarding
        {
            services.AddScoped<ITenantIdentityProvider, HierarchyBillingProfileIdentityProvider<TContext>>();
            return services;
        }
    }
}
