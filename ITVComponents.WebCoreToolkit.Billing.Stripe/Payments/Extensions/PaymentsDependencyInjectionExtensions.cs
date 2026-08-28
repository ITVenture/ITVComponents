using ITVComponents.WebCoreToolkit.Billing.Stripe.Options;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Abstractions;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Extensions
{
    public static class PaymentsDependencyInjectionExtensions
    {
        /// <summary>
        /// Registers the connect service layer AND the provider client from already-resolved
        /// <paramref name="stripeOptions"/>. Use this when axis B is wired without axis A — the two are
        /// independent, so a host may well run shops without ever selling a subscription.
        /// <para>
        /// The client is added only if none is registered yet: with both axes active they share one platform
        /// account and therefore one client, and registering a second one with the same key would just be noise.
        /// </para>
        /// </summary>
        public static IServiceCollection AddStripePayments<TContext>(this IServiceCollection services, StripeOptions stripeOptions)
            where TContext : DbContext, IPaymentsContext
        {
            services.Configure<StripeOptions>(o =>
            {
                o.ApiKey = stripeOptions.ApiKey;
                o.WebhookSecret = stripeOptions.WebhookSecret;
            });
            services.TryAddSingleton<IStripeClient>(sp =>
                new StripeClient(sp.GetRequiredService<IOptions<StripeOptions>>().Value.ApiKey));
            return services.AddStripePayments<TContext>();
        }

        /// <summary>
        /// Registers the connect service layer (payout onboarding, sales, refunds, connect webhook) bound to the
        /// host context <typeparamref name="TContext"/>.
        /// <para>
        /// The provider client and its API key are NOT registered here — both axes share one platform account, so
        /// <c>AddStripeBilling&lt;...&gt;()</c> stays the single place the key is read. Call this in ADDITION to it.
        /// </para>
        /// <para>
        /// The host still has to supply two things itself: an <c>IPaymentFeatureGate</c> (the adapter in
        /// <c>EntityFramework.Billing.TenantSecurity</c>) — without it every sale is refused, which is the safe
        /// direction — and at least one <c>ITenantSaleObserver</c>, or nothing in the shop will ever learn that a
        /// payment succeeded.
        /// </para>
        /// <para>
        /// <typeparamref name="TContext"/> must have an <see cref="IDbContextFactory{TContext}"/> registered: the
        /// services open a fresh context per operation, because they are also called from a Blazor circuit where
        /// a shared context plus an awaited provider round-trip is a concurrent-use exception waiting to happen.
        /// </para>
        /// </summary>
        public static IServiceCollection AddStripePayments<TContext>(this IServiceCollection services)
            where TContext : DbContext, IPaymentsContext
        {
            services.AddScoped<IApplicationFeeCalculator, ApplicationFeeCalculator>();
            services.AddScoped<ITenantPaymentAccountService, TenantPaymentAccountService<TContext>>();
            services.AddScoped<ITenantSaleService, TenantSaleService<TContext>>();
            services.AddScoped<IStripeConnectWebhookHandler, StripeConnectWebhookHandler<TContext>>();
            return services;
        }

        /// <summary>
        /// Registers the volume-based waiver of the subscription base fee. Separate from
        /// <c>AddStripePayments</c> because it is the one feature that needs BOTH axes in one
        /// context: it reads the tenant's sales and writes onto the tenant's own subscription invoice.
        /// <para>
        /// Hangs on the PLATFORM webhook (<c>invoice.created</c>), which resolves the processors as a collection
        /// — so leaving this out is a supported configuration, not a missing registration.
        /// </para>
        /// </summary>
        public static IServiceCollection AddStripePaymentsWaiver<TContext>(this IServiceCollection services)
            where TContext : DbContext, IPaymentsContext, IBillingContext
        {
            services.AddScoped<IVolumeWaiverProcessor, VolumeWaiverProcessor<TContext>>();
            return services;
        }
    }
}
