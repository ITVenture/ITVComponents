using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    [Index(nameof(RoleNameUniqueness),IsUnique=true,Name="IX_UniqueRoleName")]
    public abstract class Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole> : ITVComponents.WebCoreToolkit.Models.Role
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TTenantUser: TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TTenant : Tenant
        where TRoleRole: RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    {
        public Role()
        {
        }

        [Key]
        public int RoleId { get; set; }
        
        public int TenantId { get;set; }
        
        public bool IsSystemRole { get; set; }

        [ExcludeFromDictionary]
        public string? RoleMetaData { get; set; }
        
        [DatabaseGenerated(DatabaseGeneratedOption.Computed),MaxLength(1024),Required]
        public string RoleNameUniqueness { get; set; }

        public virtual ICollection<TUserRole> UserRoles { get; set; } = new List<TUserRole>();

        public virtual ICollection<TRolePermission> RolePermissions { get; set; } = new List<TRolePermission>();

        public virtual ICollection<TRoleRole> PermissiveRoles { get; set; } = new List<TRoleRole>();

        public virtual ICollection<TRoleRole> PermittedRoles { get; set; } = new List<TRoleRole>();

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }
    }
}
