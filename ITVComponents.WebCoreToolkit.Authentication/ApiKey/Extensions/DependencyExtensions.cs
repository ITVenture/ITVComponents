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
        /// Diese Methode ruft <c>WebPartInit</c> automatisch, wenn ein Host API-Key-Auth konfiguriert und
        /// <c>ApiKey:UseClientAppResolver</c> NICHT gesetzt ist. Sie ist damit die Vorgabe - und das
        /// heisst: wer nichts entscheidet, bekommt den Klartext-Vergleich.
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
        /// EF-Schicht bringt sie mit.
        /// <para>
        /// <b>Im Regelfall nicht selbst rufen</b> - <c>ApiKey:UseClientAppResolver = true</c> in der
        /// WebPart-Konfiguration setzt sie, und dann gibt es keine Reihenfolge-Frage. Der direkte Aufruf
        /// bleibt fuer Hosts, die ihre Dienste ohne WebParts verdrahten.
        /// </para>
        /// </remarks>
        /// <param name="services">the servicecollection to inject the resolver into</param>
        /// <returns>the provided servicecollection</returns>
        public static IServiceCollection UseClientAppApiKeyResolver(this IServiceCollection services)
        {
            return services.AddTransient<IGetApiKeyQuery, ClientAppApiKeyResolver>();
        }
    }
}
