using ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Extensions
{
    /// <summary>
    /// Verdrahtet Payrexx.
    /// </summary>
    public static class PayrexxDependencyInjectionExtensions
    {
        /// <summary>Der Name, unter dem dieser Anbieter angesprochen wird.</summary>
        public const string ProviderKey = "payrexx";

        /// <summary>
        /// Registriert Payrexx für Achse B (Verkäufe und Konto-Anbindung) samt Weiche.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Die Weiche kommt mit, auch wenn Payrexx allein läuft: bei genau einem Anbieter nimmt sie ihn
        /// von selbst, und ein zweiter lässt sich später dazustellen, ohne dass hier etwas zu ändern wäre.
        /// </para>
        /// <para>
        /// <b>Zwei HttpClients, nicht einer</b> — Händler- und Service-API sind verschiedene APIs mit
        /// verschiedenen Adressen und verschiedener Anmeldung. Ein gemeinsamer Client müsste bei jedem
        /// Aufruf umkonfiguriert werden, und genau daraus entsteht der Fehler, bei dem eine Anfrage mit
        /// den Kopfzeilen der anderen losgeht.
        /// </para>
        /// <para>
        /// Der Host muss weiterhin selbst beisteuern: einen <c>IPaymentFeatureGate</c> — ohne ihn wird
        /// jeder Verkauf abgelehnt, was die sichere Richtung ist — und mindestens einen
        /// <c>ITenantSaleObserver</c>, sonst erfährt der Laden nie, dass bezahlt wurde.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddPayrexxPayments<TContext>(this IServiceCollection services)
            where TContext : DbContext, IPaymentsContext
        {
            services.AddHttpClient<PayrexxApiClient>();
            services.AddHttpClient<PayrexxServiceApiClient>();

            services.AddScoped<PayrexxSaleService<TContext>>();
            services.AddScoped<PayrexxAccountService<TContext>>();
            services.AddScoped<IPaymentProviderAdapter>(sp => new PaymentProviderAdapter(ProviderKey,
                sp.GetRequiredService<PayrexxSaleService<TContext>>(),
                sp.GetRequiredService<PayrexxAccountService<TContext>>()));

            return services.AddTenantPaymentRouting<TContext>();
        }

        /// <summary>
        /// Registriert Payrexx für Achse A (Abo-Kasse und Plan-Abgleich).
        /// </summary>
        /// <remarks>
        /// Getrennt von Achse B, weil die Achsen unabhängig sind: ein Betrieb kann Abos über Stripe
        /// abrechnen und die Ladenverkäufe über Payrexx laufen lassen — genau das ist der Fall, für den
        /// es diesen Anbieter überhaupt gibt.
        /// <para>
        /// <see cref="PayrexxPlanSynchronizer{TContext}"/> kommt mit, obwohl er nichts tut: ohne
        /// Registrierung fiele jeder Aufrufer auf einen fehlenden Dienst, statt eine Erklärung im
        /// Protokoll zu finden.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddPayrexxBilling<TContext>(this IServiceCollection services)
            where TContext : DbContext, IBillingContext
        {
            services.AddHttpClient<PayrexxApiClient>();
            services.AddScoped<ISubscriptionCheckoutFactory, PayrexxSubscriptionCheckoutFactory<TContext>>();
            services.AddScoped<IPlanSynchronizer, PayrexxPlanSynchronizer<TContext>>();
            return services;
        }
    }
}
