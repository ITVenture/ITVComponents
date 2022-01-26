using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    [Index(nameof(PermissionNameUniqueness),IsUnique=true,Name="IX_UniquePermissionName")]
    [ForeignKeySelection("new ForeignKeyData<int> {Key = t.PermissionId, Label = t.PermissionName, FullRecord=t.ToDictionary(true)}", "orderby t.PermissionName")]
    public abstract class Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser> : ITVComponents.WebCoreToolkit.Models.Permission
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
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
        public virtual Tenant Tenant { get; set; }
    }
}
