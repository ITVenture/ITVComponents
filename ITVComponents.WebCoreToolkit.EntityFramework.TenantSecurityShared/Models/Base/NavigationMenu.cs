using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    [Index(nameof(UrlUniqueness), IsUnique = true, Name="IX_UniqueUrl")]
    public abstract class NavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission:Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TNavigationMenu: NavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation: TenantNavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
    {
        public NavigationMenu()
        {
        }

        [Key]
        public int NavigationMenuId { get; set; }

        [Required]
        public string DisplayName { get; set; }

        [MaxLength(1024)]
        public string Url { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed),MaxLength(1024),Required]
        public string UrlUniqueness { get; set; }

        public int? ParentId { get; set; }

        public int? SortOrder { get; set; }

        public int? PermissionId { get; set; }

        public string SpanClass { get; set; }

        [ForeignKey(nameof(ParentId))]
        public virtual TNavigationMenu Parent { get; set; }

        public virtual ICollection<TNavigationMenu> Children { get;set; } = new List<TNavigationMenu>();
        
        public virtual ICollection<TTenantNavigation> Tenants { get; set; } = new List<TTenantNavigation>();

        [ForeignKey(nameof(PermissionId))]
        public virtual TPermission EntryPoint { get; set; }
    }
}
