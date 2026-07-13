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
        /// host billing context <typeparamref name="TContext"/>. Reads secrets from the configuration section
        /// named by <paramref name="configSection"/> (defaults to <c>Billing:Stripe</c>). NOTE: the feature
        /// provisioner is registered separately — wire the tenant-security adapter via
        /// <c>AddBillingFeatureProvisioner&lt;...&gt;()</c> so the webhook can activate features.
        /// </summary>
        public static IServiceCollection AddStripeBilling<TContext>(this IServiceCollection services, IConfiguration configuration, string configSection = "Billing:Stripe")
            where TContext : DbContext, IBillingContext
        {
            services.Configure<StripeOptions>(configuration.GetSection(configSection));
            return services.AddStripeBillingCore<TContext>();
        }

        /// <summary>
        /// Registers the Stripe billing service layer bound to the host billing context
        /// <typeparamref name="TContext"/> from already-resolved <paramref name="options"/>. Used when the
        /// secrets were bound outside the DI container (e.g. by the WebPart, which reads them from a
        /// configurable section) rather than from a live <see cref="IConfiguration"/> section.
        /// </summary>
        public static IServiceCollection AddStripeBilling<TContext>(this IServiceCollection services, StripeOptions options)
            where TContext : DbContext, IBillingContext
        {
            services.Configure<StripeOptions>(o =>
            {
                o.ApiKey = options.ApiKey;
                o.WebhookSecret = options.WebhookSecret;
            });
            return services.AddStripeBillingCore<TContext>();
        }

        private static IServiceCollection AddStripeBillingCore<TContext>(this IServiceCollection services)
            where TContext : DbContext, IBillingContext
        {
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
