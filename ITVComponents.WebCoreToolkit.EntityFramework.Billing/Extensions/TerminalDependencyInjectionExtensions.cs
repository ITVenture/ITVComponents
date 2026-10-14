using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Extensions
{
    /// <summary>
    /// Verdrahtet das Kassieren am Zahlungsterminal (Achse C).
    /// </summary>
    public static class TerminalDependencyInjectionExtensions
    {
        /// <summary>
        /// Registriert die Weiche, die je Gerät an den zuständigen Weg leitet.
        /// </summary>
        /// <remarks>
        /// Jedes Terminal-Paket ruft das mit auf; <c>TryAdd</c> sorgt dafür, dass mehrere Aufrufe nichts
        /// doppelt eintragen. Anders als bei den Online-Verkäufen gibt es hier keine Vorgabe und keinen
        /// „bei genau einem nimm den": welcher Weg gilt, steht am Gerät, und ein Gerät ohne Weg ist kein
        /// Fall, den man raten kann.
        /// </remarks>
        public static IServiceCollection AddTenantTerminalRouting<TContext>(this IServiceCollection services)
            where TContext : DbContext, IPaymentsContext
        {
            services.AddTenantSaleWebhookSink<TContext>();
            services.RemoveAll<ITerminalPaymentService>();
            services.RemoveAll<ITerminalProviderCatalog>();
            services.AddScoped<RoutingTerminalPaymentService<TContext>>();
            services.AddScoped<ITerminalPaymentService>(sp => sp.GetRequiredService<RoutingTerminalPaymentService<TContext>>());
            // Dieselbe Instanz unter beiden Vertraegen: die Weiche hat die Wege ohnehin beisammen, und
            // zwei Exemplare hiessen zwei Listen, die auseinanderlaufen koennen.
            services.AddScoped<ITerminalProviderCatalog>(sp => sp.GetRequiredService<RoutingTerminalPaymentService<TContext>>());
            services.TryAddScoped<ITerminalAdministration, TerminalAdministration<TContext>>();
            // Holt Belege nach, wenn ein Webhook schneller war als das Terminal (AddHostedService traegt
            // denselben Dienst nur einmal ein, auch wenn mehrere Terminal-Pakete hier vorbeikommen).
            services.AddHostedService<TerminalCompletionSweeper<TContext>>();
            return services;
        }

        /// <summary>
        /// Registriert den Weg über einen Kassen-Agenten — für Terminals, die nur lokal ansprechbar sind.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Braucht einen <c>IAgentConnector</c></b> — den bringt das AgentServices-Webpaket mit
        /// (<c>AddAgentServices</c>). Über ihn erreicht der Weg den Katalog des Agenten (Einrichten) und das
        /// Gerät selbst (Kassieren). Ein Host mit eigener Verdrahtung darf zusätzlich einen eigenen
        /// <see cref="ITerminalAgentLocator"/> registrieren; der mitgelieferte tritt dann zurück.
        /// </para>
        /// <para>
        /// Die geräteseitige Umsetzung (<c>ITerminalDevice</c>) läuft auf dem Kassen-PC und wird hier
        /// nicht registriert — sie lebt in einem anderen Prozess.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddAgentTerminals<TContext>(this IServiceCollection services)
            where TContext : DbContext, IPaymentsContext
        {
            services.TryAddScoped<ITerminalAgentLocator, AgentConnectorTerminalLocator>();
            services.AddScoped<AgentTerminalPaymentService<TContext>>();
            services.AddScoped<ITerminalProviderAdapter>(sp => new TerminalProviderAdapter(
                AgentTerminalPaymentService<TContext>.Key,
                sp.GetRequiredService<AgentTerminalPaymentService<TContext>>()));
            return services.AddTenantTerminalRouting<TContext>();
        }
    }
}
