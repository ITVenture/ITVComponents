using ITVComponents.AgentServices.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.InterProcessExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AgentServices.Extensions
{
    /// <summary>
    /// Registriert die Agent-Dienste.
    /// </summary>
    public static class DependencyInjectionExtensions
    {
        /// <summary>
        /// Registriert den Weg zu den Agenten, die Rollen-Auflösung, die Pflege und den Beobachter für
        /// <c>LastSeenUtc</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Der Host stellt dazu:</b> einen <see cref="IAgentClientFactory"/> (wie die Anwendung mit dem
        /// Dienst-Verteiler verbunden ist), die Tabelle (<c>modelBuilder.ConfigureAgentServices&lt;TTenant, TClientApp&gt;()</c>
        /// im <c>OnModelCreating</c>) und seine Rollen (<see cref="AddAgentServiceSlot{TContract}"/>).
        /// </para>
        /// <para>
        /// Mitgeliefert wird damit auch ein <see cref="IRemoteObjectCatalog"/> — die Auswahl „Geräte-Objekt auf
        /// dieser Kasse" im Terminal-Assistenten wird dadurch zur echten Liste.
        /// </para>
        /// <para>
        /// Die Typparameter heissen wie die des Sicherheitskontexts; das WebPart bindet sie nach Namen.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddAgentServices<TContext, TClientApp, TClientAppTemplate>(this IServiceCollection services)
            where TContext : DbContext, IAgentServiceBindingContext, IClientAppContext<TClientApp, TClientAppTemplate>, ITenantScopeContext
            where TClientApp : class, IClientAppCore
            where TClientAppTemplate : class, IClientAppTemplateCore
        {
            services.TryAddScoped<IAgentConnector, AgentConnector<TContext, TClientApp, TClientAppTemplate>>();
            services.TryAddScoped<IRemoteObjectCatalog, AgentRemoteObjectCatalog<TContext>>();
            services.TryAddScoped<IAgentServiceResolver, AgentServiceResolver<TContext, TClientApp, TClientAppTemplate>>();
            services.TryAddScoped<IAgentServiceAdministration, AgentServiceAdministration<TContext, TClientApp, TClientAppTemplate>>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService,
                ClientAppLastSeenObserver<TContext, TClientApp, TClientAppTemplate>>());
            return services;
        }

        /// <summary>
        /// Deklariert eine Rolle, die eine Kasse mit einem Gerät besetzen kann.
        /// </summary>
        /// <typeparam name="TContract">der Vertrag, den ein Gerät für diese Rolle erfüllen muss</typeparam>
        /// <param name="services">die Dienste</param>
        /// <param name="name">der Schlüssel, unter dem die Besetzung gespeichert wird — nie umbenennen</param>
        /// <param name="displayName">wie die Rolle in der Maske heisst</param>
        /// <exception cref="InvalidOperationException">wenn der Name schon vergeben ist</exception>
        /// <remarks>
        /// Je Rolle höchstens ein Gerät. Wer zwei gleiche braucht (zwei Drucker an einer Kasse), deklariert zwei
        /// Rollen. Ein doppelter Name fällt hier auf, beim Start — nicht beim ersten Druck.
        /// </remarks>
        public static IServiceCollection AddAgentServiceSlot<TContract>(this IServiceCollection services, string name,
            string displayName) where TContract : class
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Length > 64)
            {
                throw new ArgumentException($"Slot name '{name}' is longer than 64 characters.", nameof(name));
            }

            if (services.Any(d => d.ServiceType == typeof(AgentServiceSlot)
                                  && d.ImplementationInstance is AgentServiceSlot s && s.Name == name))
            {
                throw new InvalidOperationException($"Agent service slot '{name}' is declared twice.");
            }

            services.AddSingleton(new AgentServiceSlot(name, string.IsNullOrWhiteSpace(displayName) ? name : displayName,
                typeof(TContract)));
            return services;
        }
    }
}
