using ITVComponents.WebCoreToolkit.Billing.Wallee.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Billing.Wallee.Extensions
{
    /// <summary>
    /// Verdrahtet wallee.
    /// </summary>
    public static class WalleeDependencyInjectionExtensions
    {
        /// <summary>Der Name, unter dem dieser Anbieter angesprochen wird.</summary>
        public const string ProviderKey = "wallee";

        /// <summary>
        /// Registriert wallee für Achse B (Verkäufe) samt Weiche.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Ohne Konto-Dienst.</b> wallee hat kein Marktplatz-Modell: es gibt nichts, wofür sich ein
        /// Mandant selbst anmelden könnte, und keine Provision, die einbehalten würde. Der Raum wird im
        /// Portal von wallee eingerichtet, und die Provision muss dem Mandanten getrennt verrechnet
        /// werden.
        /// </para>
        /// <para>
        /// Deshalb bekommt die Weiche hier einen Konto-Dienst, der <b>sagt, dass es keinen gibt</b>,
        /// statt einer Registrierung, die fehlt. Der Unterschied zählt: eine fehlende Registrierung
        /// endet in „Dienst nicht gefunden" irgendwo tief im Aufruf, diese hier in einem Satz, der die
        /// Lage erklärt.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddWalleePayments<TContext>(this IServiceCollection services)
            where TContext : DbContext, IPaymentsContext
        {
            services.AddScoped<WalleeSaleService<TContext>>();
            // Der Empfaenger der Zustandsmeldungen. Der Host muss den Endpunkt noch einhaengen
            // (MapWalleeWebhook) - ohne ihn erfaehrt die Anwendung nie, dass bezahlt wurde.
            services.AddScoped<WalleeWebhookHandler<TContext>>();
            services.AddScoped<IPaymentProviderAdapter>(sp => new PaymentProviderAdapter(ProviderKey,
                sp.GetRequiredService<WalleeSaleService<TContext>>(),
                new UnsupportedTenantPaymentAccountService(ProviderKey,
                    "wallee has no marketplace model: a tenant cannot onboard itself, and no commission is withheld. Set the space up in the wallee portal and record it as the tenant's ProviderAccountId.")));

            return services.AddTenantPaymentRouting<TContext>();
        }

        /// <summary>
        /// Registriert wallee für Achse C (Kassieren am Zahlungsterminal) über die Cloud.
        /// </summary>
        /// <remarks>
        /// Das ist die Cloud-Variante (CTI): der Server beauftragt das Gerät über wallee, es funktioniert
        /// von überall. Die schnellere lokale Variante (LTI, Socket im selben Netz) läuft über den
        /// Agenten-Weg und nicht über diese Registrierung.
        /// </remarks>
        public static IServiceCollection AddWalleeTerminals<TContext>(this IServiceCollection services)
            where TContext : DbContext, IPaymentsContext
        {
            services.AddScoped<WalleeTerminalPaymentService<TContext>>();
            services.AddScoped<ITerminalProviderAdapter>(sp => new TerminalProviderAdapter(
                WalleeTerminalPaymentService<TContext>.Key,
                sp.GetRequiredService<WalleeTerminalPaymentService<TContext>>()));
            return services.AddTenantTerminalRouting<TContext>();
        }

        /// <summary>
        /// Registriert wallee für Achse A (Abo-Kasse, Plan-Abgleich, Portal-Absage).
        /// </summary>
        /// <remarks>
        /// Das ist die Seite, auf der wallee stark ist: ein echtes Produktmodell mit Versionen,
        /// Komponenten und gestaffelten Gebühren. Der Preis dafür sind sechs Aufrufe je Plan statt zwei.
        /// <para>
        /// <see cref="WalleeBillingPortalFactory"/> kommt mit und meldet, dass es kein Kundenportal gibt
        /// — aus demselben Grund wie oben.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddWalleeBilling<TContext>(this IServiceCollection services)
            where TContext : DbContext, IBillingContext
        {
            services.AddScoped<ISubscriptionCheckoutFactory, WalleeSubscriptionCheckoutFactory<TContext>>();
            services.AddScoped<IPlanSynchronizer, WalleePlanSynchronizer<TContext>>();
            services.AddScoped<IBillingPortalFactory, WalleeBillingPortalFactory>();
            return services;
        }
    }
}
