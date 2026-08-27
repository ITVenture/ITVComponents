using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base
{
    public abstract class SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset: SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter: SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter: SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TTenant : Tenant
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    {
        [Key]
        public int SharedAssetId { get; set; }

        public int AssetTemplateId { get; set; }

        [Required, MaxLength(128)]
        public string AssetKey { get; set; }

        [Required, MaxLength(128)]
        public string AnonymousAccessTokenRaw { get; set; }

        [MaxLength(512)]
        public string AssetTitle { get; set; }

        [Required, MaxLength(1024)]
        public string RootPath { get; set; }

        public int TenantId { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NotBefore { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NotAfter { get; set; }

        /// <summary>
        /// Worauf diese Freigabe zeigt: die Argumentwerte der Vorlage, in kanonischer Form.
        /// <para>
        /// Bewusst als JSON an der Zeile und nicht als eigene Tabelle. Die Werte werden ausschliesslich
        /// zusammen mit ihrer Freigabe gelesen, und dieselbe Form wandert spaeter in ein Ad-hoc-Ticket -
        /// ein Format und ein Vergleichsweg fuer beide Faelle. Eine eigene Tabelle haette die Werte
        /// ausserdem entweder in die generische Typkette der Freigabe gezwungen oder mandantengebundene
        /// Daten in eine mandantenfreie Tabelle gelegt.
        /// </para>
        /// </summary>
        public string ArgumentValuesJson { get; set; }

        /// <summary>
        /// An wen die Freigabe gerichtet ist - typischerweise eine E-Mail-Adresse.
        /// <para>
        /// <b>Steht neben dem Filter, nicht darin.</b> Der Platzhalter <c>#ANONYMOUS#</c> in den
        /// Benutzerfiltern ist ein exakter Vergleich und die sicherheitskritischste Zeile des ganzen
        /// Mechanismus; eine Adresse hineinzufalten wuerde daraus einen Praefix-Vergleich machen.
        /// </para>
        /// <para>
        /// <b>Und es ist eine Behauptung, kein Nachweis:</b> wer den Link hat, ist wer der Link sagt. Fuer
        /// Zuordnung und Protokoll taugt das, als Identitaet nicht.
        /// </para>
        /// </summary>
        [MaxLength(256)]
        public string RecipientLabel { get; set; }

        [ForeignKey(nameof(AssetTemplateId))]
        public virtual TAssetTemplate Template { get; set; }

        public virtual ICollection<TSharedAssetUserFilter> UserFilters { get; set; } =
            new List<TSharedAssetUserFilter>();

        public virtual ICollection<TSharedAssetTenantFilter> TenantFilters { get; set; } =
            new List<TSharedAssetTenantFilter>();
        
        [ForeignKey(nameof(TenantId))]
        public virtual TTenant AssetOwner { get; set; }
    }
}
