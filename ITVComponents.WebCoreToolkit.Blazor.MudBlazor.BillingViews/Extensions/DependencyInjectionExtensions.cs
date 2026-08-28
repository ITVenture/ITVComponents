using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Extensions
{
    public static class DependencyInjectionExtensions
    {
        /// <summary>
        /// Registers the MudBlazor billing views (routing assembly + <see cref="IBillingHandler"/>). The host
        /// must also wire the provider service layer (<c>AddStripeBilling&lt;TContext&gt;(config)</c>) and the
        /// feature provisioner (<c>AddBillingFeatureProvisioner&lt;...&gt;()</c>); the handler depends on both.
        /// </summary>
        public static IServiceCollection AddMudBlazorBillingViews<TContext>(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior = null)
            where TContext : DbContext, IBillingContext, ITenantScopeContext
        {
            partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions { DefaultBehavior = TypeRegisterBehavior.Use };
            services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);
            services.AddScoped<IBillingHandler, BillingHandler<TContext>>();
            return services;
        }

        /// <summary>
        /// Registers the MudBlazor payment views (axis B: the tenant's own end customers). A call of its OWN next
        /// to <see cref="AddMudBlazorBillingViews{TContext}"/>, so a host can use the payments services without
        /// these pages — precisely the "shop with its own front end" case.
        /// <para>
        /// The host must also wire the connect service layer (<c>AddStripePayments&lt;TContext&gt;()</c>) and the
        /// payment feature gate (<c>AddPaymentFeatureGate&lt;...&gt;()</c>); the handler depends on both.
        /// </para>
        /// </summary>
        /// <typeparam name="TContext">The host context carrying the payments tables and the active tenant.</typeparam>
        /// <typeparam name="TTenant">
        /// The tenant model AS MAPPED by the host — <c>Tenant</c> on flat hosts, <c>HierarchyTenant</c> on
        /// hierarchical ones. The platform overview reads tenant names through it; asking for the base type on a
        /// hierarchical host would throw, because only the derived type is in that model. The WebPart fills this
        /// automatically: <c>MethodHelper</c> binds type parameters BY NAME out of the security context, and
        /// <c>TTenant</c> is one of its own names.
        /// </typeparam>
        public static IServiceCollection AddMudBlazorPaymentViews<TContext, TTenant>(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior = null)
            where TContext : DbContext, IPaymentsContext, ITenantScopeContext
            where TTenant : Tenant
        {
            partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions { DefaultBehavior = TypeRegisterBehavior.Use };
            // Same assembly as the billing views; the routing registration is idempotent for a host that wires
            // both.
            services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);
            services.AddScoped<IPaymentsHandler, PaymentsHandler<TContext, TTenant>>();
            return services;
        }
    }
}
