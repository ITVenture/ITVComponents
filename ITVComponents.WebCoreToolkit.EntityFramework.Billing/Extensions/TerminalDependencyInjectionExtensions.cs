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
            services.AddScoped<ITerminalPaymentService, RoutingTerminalPaymentService<TContext>>();
            return services;
        }

        /// <summary>
        /// Registriert den Weg über einen Kassen-Agenten — für Terminals, die nur lokal ansprechbar sind.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Der Host muss einen <see cref="ITerminalAgentLocator"/> beisteuern.</b> Ohne ihn scheitert
        /// die Auflösung, und zwar ausdrücklich: wie aus dem Namen in der Route eine Verbindung wird,
        /// weiss nur die Anwendung, die ihre Proxies ohnehin schon verdrahtet hat. Eine mitgelieferte
        /// Vorgabe wäre eine Annahme, die bei zwei Agenten falsch ist.
        /// </para>
        /// <para>
        /// Die geräteseitige Umsetzung (<c>ITerminalDevice</c>) läuft auf dem Kassen-PC und wird hier
        /// nicht registriert — sie lebt in einem anderen Prozess.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddAgentTerminals<TContext>(this IServiceCollection services)
            where TContext : DbContext, IPaymentsContext
        {
            services.AddScoped<AgentTerminalPaymentService<TContext>>();
            services.AddScoped<ITerminalProviderAdapter>(sp => new TerminalProviderAdapter(
                AgentTerminalPaymentService<TContext>.Key,
                sp.GetRequiredService<AgentTerminalPaymentService<TContext>>()));
            return services.AddTenantTerminalRouting<TContext>();
        }
    }
}
