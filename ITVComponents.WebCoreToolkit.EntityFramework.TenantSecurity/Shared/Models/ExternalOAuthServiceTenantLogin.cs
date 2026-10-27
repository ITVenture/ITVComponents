using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    /// <summary>
    /// Die Zugangsdaten eines Mandanten (oder der Plattform) fuer einen externen Dienst: das OAuth-Token und, je nach
    /// Anmeldetyp, die festen Zugangsdaten (<see cref="CredentialJson"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Name stammt aus der Zeit, als hier nur das OAuth-Token lag. Er bleibt, damit keine neue generische Entitaet am
    /// Sicherheitskontext die Stelligkeit aller Typen verschiebt (siehe docs/Plan-ExternalServices-Credentials.md).
    /// </para>
    /// <para>
    /// <see cref="TenantId"/> null = Zugangsdaten der <b>Plattform</b>; Mandanten fallen nur darauf zurueck, wenn die
    /// Definition es erlaubt (<c>AllowPlatformCredentials</c>). Je Dienst gibt es hoechstens eine Zeile je Mandant und
    /// hoechstens eine Plattform-Zeile (der zweite Index ist providerabhaengig gefiltert und steht in den SyntaxHelpern).
    /// </para>
    /// </remarks>
    [Index(nameof(OAuthServiceId), nameof(TenantId), IsUnique = true, Name = "UQ_ExternalOAuthServiceTenantLogin")]
    public class ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TTenant : Tenant
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        [Key]
        public int ExternalOAuthServiceTenantLoginId { get; set; }

        /// <summary>Der Mandant, dem die Zugangsdaten gehoeren; null = die Plattform.</summary>
        public int? TenantId { get; set; }

        public int OAuthServiceId { get; set; }

        public string Token { get; set; }

        public bool Revoked { get; set; }

        /// <summary>
        /// Die festen Zugangsdaten (Benutzer/Client und Geheimnis, je nach Anmeldetyp), verschluesselt mit dem Schluessel
        /// des Besitzers dieser Zeile - null bei Diensten, die nur ein OAuth-Token kennen.
        /// </summary>
        public string CredentialJson { get; set; }

        /// <summary>
        /// Ob Nachkommen im Mandantenbaum ohne eigene Zeile diese benutzen - derselbe Mechanismus wie bei Settings und
        /// Plugins (die naechste Zeile mit <c>ParentLevel == 1 || Inheritable</c> gewinnt). Vorgabe <c>false</c>, anders als
        /// dort: Zugangsdaten werden ausdruecklich weitergegeben, nicht aus Versehen.
        /// </summary>
        public bool Inheritable { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        [ForeignKey(nameof(OAuthServiceId))]
        public virtual TExternalOAuthService OAuthService { get; set; }
    }
}
