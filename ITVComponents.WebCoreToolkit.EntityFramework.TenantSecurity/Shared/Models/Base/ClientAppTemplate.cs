using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base
{
    [Index(nameof(Name), IsUnique = true, Name="IX_UniqueTemplateName")]
    // Nullbar und eindeutig: SQL Server haengt den Filter "WHERE [TypeKey] IS NOT NULL" selbst an, PostgreSQL
    // zaehlt NULLs ohnehin als verschieden (siehe ClientAppAccess).
    [Index(nameof(TypeKey), IsUnique = true, Name = "UQ_ClientAppTemplateTypeKey")]
    public class ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate> : IClientAppTemplateCore
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
        where TTenant : Tenant
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    {
        [Key]
        public int ClientAppTemplateId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; }

        /// <summary>
        /// Unter welchem Objektnamen Agenten dieses Templates ihren Katalog bereitstellen. Leer = der Standardname
        /// (<c>AgentCatalog.DefaultObjectName</c>).
        /// </summary>
        /// <remarks>
        /// Für den Fall, dass ein Agent auf einen Ersatz-Katalog ausweichen muss — vom Administrator setzbar,
        /// ohne dass Agent und Web den Namen aushandeln.
        /// </remarks>
        [MaxLength(128)]
        public string CatalogObjectName { get; set; }

        /// <summary>
        /// Was ein Geraet dieses Templates ist, als fester Schluessel (etwa <c>pos-agent</c>). Ein Geraet nennt
        /// ihn beim Koppeln; wer bestaetigt, bekommt dann nur Anwendungen dieses Templates zur Auswahl.
        /// </summary>
        /// <remarks>
        /// Bewusst nicht <see cref="Name"/>: der ist in der Maske aenderbar, und ein umbenanntes Template
        /// liesse jedes ausgerollte Geraet ins Leere koppeln. Leer = Geraete dieses Templates koennen nur mit
        /// ihrem <c>ClientKey</c> koppeln. Eindeutig, sofern gesetzt.
        /// </remarks>
        [MaxLength(64)]
        public string TypeKey { get; set; }

        /// <summary>
        /// Die Rechtebuendel, die dieses Template anbietet.
        /// </summary>
        /// <remarks>
        /// Frueher lief das ueber eine Verknuepfungstabelle (<c>ClientAppTemplatePermission</c>) auf global
        /// geteilte Buendel. Die Buendel gehoeren jetzt direkt zum Template - aus der Verknuepfung wird ein
        /// Fremdschluessel, und die Zuordnung ist damit eindeutig statt geteilt.
        /// <para>
        /// <b>Nur globale Rechte.</b> Ein Template ist mandantenlos und wird in jeden Mandanten angewandt;
        /// ein mandantengebundenes Recht (<c>Permission.TenantId != null</c>) liesse sich dort nicht
        /// aufloesen und wuerde ueber den Namensvergleich schlimmstenfalls auf ein gleichnamiges fremdes
        /// Recht durchschlagen.
        /// </para>
        /// </remarks>
        public virtual ICollection<TAppPermissionSet> PermissionSets { get; set; } =
            new List<TAppPermissionSet>();
    }
}
