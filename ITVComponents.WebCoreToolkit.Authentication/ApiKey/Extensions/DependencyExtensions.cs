using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Authentication.ApiKey.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Activates the default-apikey user-mapper.
        /// </summary>
        /// <remarks>
        /// <b>Vorsicht: das ist eine Sicherheitsentscheidung.</b> Der Standard-Resolver vergleicht den
        /// Schluessel im <b>Klartext</b> gegen <c>Users.UserName</c> - der Schluessel steht damit lesbar in
        /// der Benutzertabelle. Wer eine Datenbanksicherung, eine Auskunftsabfrage oder einen zu weit
        /// gefassten Verwaltungszugang hat, kann sich als dieses Geraet ausgeben.
        /// <para>
        /// Diese Methode wird aus <c>WebPartInit</c> <b>unbedingt</b> gerufen, sobald ein Host API-Key-Auth
        /// konfiguriert - niemand ruft sie also bewusst auf. Wer den gehashten Weg will, registriert
        /// <see cref="UseClientAppApiKeyResolver"/> <b>nach</b> der WebPart-Konfiguration: hier wird per
        /// <c>AddTransient</c> registriert und nicht per <c>TryAdd</c>, die spaetere Registrierung gewinnt.
        /// </para>
        /// </remarks>
        /// <param name="services">the servicecollection to inject the resolver into</param>
        /// <returns>the provided servicecollection</returns>
        public static IServiceCollection UseDefaultApiKeyResolver(this IServiceCollection services)
        {
            return services.AddTransient<IGetApiKeyQuery, DefaultApiKeyUserResolver>();
        }

        /// <summary>
        /// Loest API-Schluessel gegen die ClientApp-Zugaenge auf - gehasht, mit Mandant, Ablauf und
        /// Widerruf.
        /// </summary>
        /// <remarks>
        /// Setzt eine Umsetzung von <see cref="Security.ClientApps.IClientAppAccessQuery"/> voraus; die
        /// EF-Schicht bringt sie mit. <b>Nach</b> der WebPart-Konfiguration registrieren, sonst
        /// ueberschreibt <see cref="UseDefaultApiKeyResolver"/> sie still.
        /// </remarks>
        /// <param name="services">the servicecollection to inject the resolver into</param>
        /// <returns>the provided servicecollection</returns>
        public static IServiceCollection UseClientAppApiKeyResolver(this IServiceCollection services)
        {
            return services.AddTransient<IGetApiKeyQuery, ClientAppApiKeyResolver>();
        }
    }
}
