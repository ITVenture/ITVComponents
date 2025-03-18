using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    [Index(nameof(RoleId), nameof(TenantUserId),IsUnique=true,Name="IX_UniqueUserRole")]
    public abstract class UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TRole:Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TPermission: Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TUserRole:UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TRolePermission:RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TTenantUser:TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole> 
    where TTenant : Tenant
    where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    {
        [Key]
        public int UserRoleId { get; set; }

        public int? TenantUserId { get; set; }

        public int? RoleId { get;set; }

        [ForeignKey(nameof(TenantUserId))]
        public virtual TTenantUser User { get;set; }

        [ForeignKey(nameof(RoleId))]
        public virtual TRole Role { get; set; }
    }
}
