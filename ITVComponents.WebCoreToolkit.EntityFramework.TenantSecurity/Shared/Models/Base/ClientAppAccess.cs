using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base
{
    // UQ_TUserPerApp (TenantUserId, ClientAppId) ist NICHT hier: seit TenantUserId optional ist, braucht
    // der Index einen Filter auf "TenantUserId IS NOT NULL" - und [Index] kann keine Filter. SQL Server
    // behandelt NULLs im Unique-Index als gleich und liesse sonst genau EINEN Maschinenzugang pro
    // Datenbank zu, PostgreSQL dagegen beliebig viele. Ohne Filter laufen die beiden Datenbanken also
    // auseinander, und auf PostgreSQL faellt es nie auf. Gesetzt wird er in ClientAppModelBuilder.
    [Index(nameof(Label), IsUnique=true, Name="UQ_ClientAppAccess")]
    public class ClientAppAccess<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess>
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess>
        where TClientAppAccess: ClientAppAccess<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess>
        where TTenant : Tenant
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    {
        [Key]
        public int ClientAppAccessId { get; set; }

        /// <summary>
        /// Der systemweit eindeutige Schluessel dieses Zugangs. Er reist als <c>##APPUSER##&lt;Label&gt;#</c>
        /// durch die Anmeldung und ist das, was die Rechteaufloesung nachschlaegt.
        /// </summary>
        /// <remarks>
        /// Systemweit eindeutig, nicht je Anwendung - er wird ohne Mandantenkontext nachgeschlagen. Bei
        /// maschinell erzeugten Zugaengen gehoert deshalb ein Zufallsanteil hinein; "Terminal-01" je
        /// Filiale geht nicht.
        /// </remarks>
        [Required, MaxLength(128)]
        public string Label { get; set; }

        /// <summary>
        /// Der Benutzer, in dessen Auftrag die Anwendung handelt - oder <c>null</c>.
        /// </summary>
        /// <remarks>
        /// <b>Das ist die Weiche zwischen den beiden Faellen:</b>
        /// <list type="bullet">
        /// <item><c>null</c> = <b>Maschine</b>. Die Rechte kommen direkt aus den Buendeln der Anwendung,
        /// ohne Benutzer-Join und ohne Schnittmenge. Ein Kassenterminal ist keine Person und braucht
        /// keinen Schattenbenutzer in Benutzerlisten, im Onboarding und in Mandanten-Benutzerzahlen.</item>
        /// <item>gesetzt = <b>Delegation</b>. Die Anwendung handelt fuer diesen Benutzer und bekommt
        /// hoechstens dessen Rechte, gedeckelt durch die Buendel der Anwendung.</item>
        /// </list>
        /// </remarks>
        public int? TenantUserId { get; set; }

        public int ClientAppId { get; set; }

        /// <summary>
        /// Der Hash des Zugangsgeheimnisses. Der Klartext verlaesst den Server genau einmal, bei der
        /// Kopplung, und wird nie gespeichert.
        /// </summary>
        [MaxLength(512)]
        public string SecretHash { get; set; }

        /// <summary>
        /// Sprechende Bezeichnung des gekoppelten Geraets, fuer die Verwaltungsliste. Rein beschreibend.
        /// </summary>
        [MaxLength(200)]
        public string DeviceLabel { get; set; }

        public DateTime CreatedUtc { get; set; }

        /// <summary>Ablauf des Zugangs, oder <c>null</c> fuer unbefristet.</summary>
        public DateTime? ExpiresUtc { get; set; }

        /// <summary>
        /// Gesetzt, sobald der Zugang widerrufen wurde. Ein Widerruf trifft genau EIN Geraet - das ist der
        /// Grund, warum das Geheimnis hier haengt und nicht an der Anwendung.
        /// </summary>
        public DateTime? RevokedUtc { get; set; }

        /// <summary>
        /// Letzte erfolgreiche Anmeldung. Das Feld, an dem man ein stillgelegtes Geraet erkennt.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; }

        [ForeignKey(nameof(ClientAppId))]
        public virtual TClientApp ClientApp { get; set; }

        [ForeignKey(nameof(TenantUserId))]
        public virtual TTenantUser TenantUser { get; set; }
    }
}
