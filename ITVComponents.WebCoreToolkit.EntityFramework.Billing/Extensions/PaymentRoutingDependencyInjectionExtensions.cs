using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Extensions
{
    /// <summary>
    /// Verdrahtet die Anbieter-Weiche für Achse B.
    /// </summary>
    public static class PaymentRoutingDependencyInjectionExtensions
    {
        /// <summary>
        /// Registriert die Weiche und die beiden Fassaden, die je Mandant an den zuständigen Anbieter
        /// weiterleiten.
        /// </summary>
        /// <param name="services">die Dienstsammlung</param>
        /// <param name="defaultProvider">
        /// Der Anbieter der Plattform für Mandanten, an deren Konto keiner steht. <b>Nur nötig, wenn
        /// mehrere registriert sind</b> — bei genau einem nimmt die Weiche ihn von selbst, und ein
        /// Vorgabewert wäre eine Angabe, die beim Umbenennen veraltet.
        /// </param>
        /// <remarks>
        /// <para>
        /// Jedes Anbieter-Paket ruft das mit auf; <c>TryAdd</c> sorgt dafür, dass mehrere Aufrufe nichts
        /// doppelt eintragen. Wer die Vorgabe setzen will, ruft es danach noch einmal ausdrücklich mit
        /// einem Namen auf.
        /// </para>
        /// <para>
        /// <b>Was bewusst NICHT passiert:</b> ein stiller Vorzug. Sind mehrere Anbieter registriert und
        /// weder am Mandanten noch hier ist einer benannt, wirft die Weiche — statt den erstbesten zu
        /// nehmen. Geld über einen Anbieter zu leiten, den niemand gewählt hat, ist der teurere Fehler.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddTenantPaymentRouting<TContext>(this IServiceCollection services,
            string? defaultProvider = null)
            where TContext : DbContext, IPaymentsContext
        {
            if (!string.IsNullOrWhiteSpace(defaultProvider))
            {
                // Ausdrücklich gesetzt: das ersetzt eine frühere Registrierung, damit die Reihenfolge der
                // Aufrufe nicht darüber entscheidet, welcher Anbieter die Vorgabe ist.
                services.RemoveAll<PaymentProviderRouter<TContext>>();
                services.AddScoped(sp => new PaymentProviderRouter<TContext>(
                    sp.GetRequiredService<IDbContextFactory<TContext>>(),
                    sp.GetServices<IPaymentProviderAdapter>(), defaultProvider));
            }
            else
            {
                services.TryAddScoped(sp => new PaymentProviderRouter<TContext>(
                    sp.GetRequiredService<IDbContextFactory<TContext>>(),
                    sp.GetServices<IPaymentProviderAdapter>(), null));
            }

            services.AddTenantSaleWebhookSink<TContext>();

            services.RemoveAll<ITenantSaleService>();
            services.RemoveAll<ITenantPaymentAccountService>();
            services.AddScoped<ITenantSaleService, RoutingTenantSaleService<TContext>>();
            services.AddScoped<ITenantPaymentAccountService, RoutingTenantPaymentAccountService<TContext>>();
            return services;
        }

        /// <summary>
        /// Registriert die Buchung eingehender Zahlungsmeldungen.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Getrennt von der Weiche, weil ein Anbieter sie auch OHNE sie braucht: der Webhook-Weg ist
        /// unabhängig davon, ob mehrere Anbieter nebeneinander laufen. Jedes Anbieter-Paket ruft das mit
        /// auf; <c>TryAdd</c> sorgt dafür, dass mehrere Aufrufe nichts doppelt eintragen.
        /// </para>
        /// <para>
        /// Sie liegt hier und nicht bei den Anbietern, weil sie für alle dieselbe ist — und weil ein
        /// Paket, das sie vergisst, einen Webhook hätte, der auf einen fehlenden Dienst läuft.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddTenantSaleWebhookSink<TContext>(this IServiceCollection services)
            where TContext : DbContext, IPaymentsContext
        {
            services.TryAddScoped(sp => new TenantSaleWebhookSink<TContext>(
                sp.GetRequiredService<IDbContextFactory<TContext>>(),
                new TenantSaleNotifier(sp.GetServices<ITenantSaleObserver>())));
            return services;
        }
    }
}
