using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base
{
    [Index(nameof(ClientKey), IsUnique = true, Name = "UQ_ClientAppKey")]
    public class ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess>
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TClientAppPermission: ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess>
        where TClientApp: ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess>
        where TClientAppAccess: ClientAppAccess<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess>
        where TTenant : Tenant
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    {
        [Key]
        public int ClientAppId { get; set; }

        /// <summary>
        /// Der Mandant, dem diese Anwendung gehoert.
        /// </summary>
        /// <remarks>
        /// Frueher war eine ClientApp mandantenlos und damit fuer jeden Mandanten sichtbar; der
        /// Mandantenbezug entstand erst ueber den Umweg <c>ClientAppAccess.TenantUser.Tenant</c>. Damit
        /// haette eine plattformweit definierte Anwendung in JEDEM Mandanten Rechte gehabt, ohne dass sie
        /// dort je ein Administrator vergeben haette.
        /// <para>
        /// Jetzt gilt: die Plattform bestimmt ueber das Template, was zur Auswahl steht - der
        /// Mandanten-Administrator bestimmt, welchen Freiheitsgrad er dieser Anwendung tatsaechlich
        /// zugesteht.
        /// </para>
        /// </remarks>
        public int TenantId { get; set; }

        /// <summary>
        /// Das Template, aus dem diese Anwendung entstanden ist. Es begrenzt, welche Rechtebuendel ihr
        /// zugeordnet werden duerfen.
        /// </summary>
        /// <remarks>
        /// Ohne Navigation: <c>TClientAppTemplate</c> steht nicht in der Typparameter-Liste dieser Klasse,
        /// und die Template-Seite kennt <c>TClientApp</c> nicht. Die Beziehung wird deshalb als einzige
        /// dieser Familie per Fluent-API konfiguriert (siehe <c>ClientAppModelBuilder</c>).
        /// </remarks>
        public int ClientAppTemplateId { get; set; }

        [Required, MaxLength(200)]
        public string ClientName { get; set; }

        /// <summary>
        /// Die oeffentliche Kennung der Anwendung. <b>Systemweit</b> eindeutig.
        /// </summary>
        /// <remarks>
        /// Systemweit und nicht je Mandant, weil es beim Anmelden noch KEINEN Mandantenkontext gibt: das
        /// Geraet legt seinen Schluessel vor, und daraus muss der Mandant erst gefunden werden.
        /// <para>
        /// War frueher <c>nvarchar(max)</c> ohne Index - in SQL Server nicht einmal indizierbar, also als
        /// Nachschlagefeld unbrauchbar.
        /// </para>
        /// </remarks>
        [Required, MaxLength(128)]
        public string ClientKey { get; set; }

        /// <summary>
        /// Reserviert fuer den vertraulichen Anwendungs-Fluss (JWT). <b>Nicht</b> das Geheimnis eines
        /// Geraets - das haengt am einzelnen Zugang (<c>ClientAppAccess.SecretHash</c>), damit ein Widerruf
        /// ein Geraet trifft und nicht alle.
        /// </summary>
        [Required]
        public string ClientSecret { get; set; }

        /// <summary>
        /// Schaltet die Anwendung als Ganzes ab. Ein abgeschalteter Eintrag laesst keinen seiner Zugaenge
        /// mehr herein - der Sammelschalter neben dem Widerruf am einzelnen Geraet.
        /// </summary>
        public bool Enabled { get; set; } = true;

        public DateTime CreatedUtc { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        public virtual ICollection<TClientAppPermission> AppPermissions { get; set; } = new List<TClientAppPermission>();

        /// <summary>
        /// Die Zugaenge dieser Anwendung - je gekoppeltem Geraet einer.
        /// </summary>
        public virtual ICollection<TClientAppAccess> Accesses { get; set; } = new List<TClientAppAccess>();
    }
}
