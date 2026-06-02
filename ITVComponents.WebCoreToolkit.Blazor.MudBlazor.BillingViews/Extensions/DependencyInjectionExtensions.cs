using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
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
    }
}
