using ITVComponents.WebCoreToolkit.Billing.Stripe.Abstractions;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Impl;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Extensions
{
    public static class DependencyInjectionExtensions
    {
        /// <summary>
        /// Registers the Stripe billing service layer (checkout / portal / webhook / plan-sync) bound to the
        /// host billing context <typeparamref name="TContext"/>. Reads secrets from configuration section
        /// <c>Billing:Stripe</c>. NOTE: the feature provisioner is registered separately — wire the
        /// tenant-security adapter via <c>AddBillingFeatureProvisioner&lt;...&gt;()</c> so the webhook can
        /// activate features.
        /// </summary>
        public static IServiceCollection AddStripeBilling<TContext>(this IServiceCollection services, IConfiguration configuration)
            where TContext : DbContext, IBillingContext
        {
            services.Configure<StripeOptions>(configuration.GetSection("Billing:Stripe"));
            services.AddSingleton<IStripeClient>(sp =>
                new StripeClient(sp.GetRequiredService<IOptions<StripeOptions>>().Value.ApiKey));

            services.AddScoped<IStripeCheckoutSessionFactory, StripeCheckoutSessionFactory<TContext>>();
            services.AddScoped<IStripeBillingPortalFactory, StripeBillingPortalFactory<TContext>>();
            services.AddScoped<IStripeWebhookHandler, StripeWebhookHandler<TContext>>();
            services.AddScoped<IPlanSynchronizer, PlanSynchronizer<TContext>>();

            return services;
        }
    }
}
