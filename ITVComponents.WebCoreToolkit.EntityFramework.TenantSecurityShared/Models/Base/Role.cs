using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    [Index(nameof(RoleNameUniqueness),IsUnique=true,Name="IX_UniqueRoleName")]
    public abstract class Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser> : ITVComponents.WebCoreToolkit.Models.Role
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
    {
        public Role()
        {
        }

        [Key]
        public int RoleId { get; set; }
        
        public int TenantId { get;set; }
        
        public bool IsSystemRole { get; set; }
        
        [DatabaseGenerated(DatabaseGeneratedOption.Computed),MaxLength(1024),Required]
        public string RoleNameUniqueness { get; set; }

        public virtual ICollection<TUserRole> UserRoles { get; set; } = new List<TUserRole>();

        public virtual ICollection<TRolePermission> RolePermissions { get; set; } = new List<TRolePermission>();
        
        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }
    }
}
