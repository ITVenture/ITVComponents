using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base
{
    [Index(nameof(SystemKey), IsUnique=true, Name="UQ_AssetTemplateSysKey")]
    public abstract class AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature:AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TTenant : Tenant
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    {
        [Key]
        public int AssetTemplateId { get; set; }

        public int? FeatureId { get; set; }

        public int? PermissionId { get; set; }

        public string Name { get; set; }

        public string SystemKey { get; set; }

        /// <summary>
        /// Wie streng die Bestaetigung der Argumente verlangt wird. Vorgabe <c>None</c> - damit verhaelt
        /// sich jede bestehende Vorlage unveraendert. Wer Argumente pflegt, hebt sie auf mindestens
        /// <c>Confirmed</c>, sonst haengt die Objektsicherheit am Wohlverhalten jedes Endpunkts.
        /// </summary>
        public AssetArgumentEnforcement ArgumentEnforcement { get; set; } = AssetArgumentEnforcement.None;

        /// <summary>
        /// Ob mit dieser Vorlage Ad-hoc-Tickets erzeugt werden duerfen - Freigaben, die nirgends stehen.
        /// Vorgabe aus: was sich nicht einzeln zurueckziehen laesst, sollte eine Entscheidung sein.
        /// </summary>
        public bool AllowAdHoc { get; set; }

        /// <summary>
        /// Wie lange ein Ticket dieser Vorlage hoechstens gilt. <b>Die Frist ist bei einem Ticket
        /// Pflicht</b>, denn sie ist der Hauptgrund, warum ein nicht widerrufbarer Link vertretbar ist.
        /// </summary>
        public int MaxAdHocMinutes { get; set; } = 60;

        /// <summary>
        /// Der Schluessel einer Gueltigkeitsregel, die der Host implementiert ("Auftrag ist offen").
        /// Leer = es zaehlt allein das Datum.
        /// </summary>
        [MaxLength(128)]
        public string ValidityRuleKey { get; set; }

        public virtual ICollection<TAssetTemplatePath> PathTemplates { get; set; } = new List<TAssetTemplatePath>();

        public virtual ICollection<TAssetTemplateGrant> Grants { get; set; } = new List<TAssetTemplateGrant>();

        public virtual ICollection<TAssetTemplateFeature> FeatureGrants { get; set; } = new List<TAssetTemplateFeature>();

        [ForeignKey(nameof(FeatureId))]
        public virtual Feature RequiredFeature { get; set; }

        [ForeignKey(nameof(PermissionId))]
        public virtual TPermission RequiredPermission { get; set; }
    }
}
