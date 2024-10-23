using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.ForeignKeySelectors;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    [Index(nameof(PermissionNameUniqueness),IsUnique=true,Name="IX_UniquePermissionName")]
    [ForeignKeySelection(typeof(PermissionSelector<,,,,,,,,>))]
    public abstract class Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole> : ITVComponents.WebCoreToolkit.Models.Permission
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TTenantUser: TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TTenant: Tenant
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    {
        public Permission()
        {
        }

        [Key]
        public int PermissionId { get; set; }

        [MaxLength(2048)]
        public string Description { get; set; }
        
        public int? TenantId { get; set; }

        //public bool IsGlobal { get;set; }
        
        [DatabaseGenerated(DatabaseGeneratedOption.Computed),MaxLength(1024),Required]
        public string PermissionNameUniqueness { get; set; }

        public virtual ICollection<TRolePermission> RolePermissions { get; set; } = new List<TRolePermission>();

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }
    }
}
