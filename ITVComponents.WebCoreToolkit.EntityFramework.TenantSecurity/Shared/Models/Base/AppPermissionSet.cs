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
    [Index(nameof(ClientAppTemplateId), nameof(Name), IsUnique = true, Name = "UQ_AppPermissionSetName")]
    public class AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
        where TAppPermissionSet:AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
        where TTenant : Tenant
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    {
        [Key]
        public int AppPermissionSetId { get; set; }

        /// <summary>
        /// Das Template, zu dem dieses Buendel gehoert.
        /// </summary>
        /// <remarks>
        /// Rechtebuendel sind anwendungsspezifisch - "Vollzugriff" heisst bei einem Kassen-Agenten etwas
        /// anderes als bei einem Archiv-Dienst. Frueher war <see cref="Name"/> systemweit eindeutig, ein
        /// Buendel dieses Namens konnte es also genau einmal geben.
        /// <para>
        /// Die Zugehoerigkeit traegt ausserdem die <b>Obergrenze</b>: eine ClientApp darf nur Buendel ihres
        /// eigenen Templates fuehren. Damit ist die Grenze eine Fremdschluessel-Invariante und keine
        /// Rechenregel, die jemand vergessen kann.
        /// </para>
        /// <para>
        /// <b>Mit Navigation</b>, und das ist keine Bequemlichkeit: der Konfigurations-Austausch setzt beim
        /// Import die Navigation und nicht die Fremdschluessel-Spalte. Das Template entsteht im selben Lauf,
        /// und <c>SaveChanges()</c> laeuft einmal ganz am Schluss - zum Zuweisungszeitpunkt ist die Identity
        /// des Templates also noch 0. Nur die Zuweisung des OBJEKTS laesst EF die Id spaeter nachfuellen.
        /// Ein erster Entwurf ohne Navigation haette den Export eines Templates samt Buendeln in eine
        /// frische Umgebung unmoeglich gemacht.
        /// </para>
        /// </remarks>
        public int ClientAppTemplateId { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; }

        [ForeignKey(nameof(ClientAppTemplateId))]
        public virtual TClientAppTemplate ClientAppTemplate { get; set; }

        public virtual ICollection<TAppPermission> Permissions { get; set; } = new List<TAppPermission>();
    }
}
