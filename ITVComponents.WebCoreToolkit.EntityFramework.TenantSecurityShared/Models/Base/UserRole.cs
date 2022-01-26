using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    [Index(nameof(RoleId), nameof(TenantUserId),IsUnique=true,Name="IX_UniqueUserRole")]
    public abstract class UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
    where TRole:Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
    where TPermission: Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
    where TUserRole:UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
    where TRolePermission:RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
    where TTenantUser:TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
    {
        [Key]
        public int UserRoleId { get; set; }

        public int TenantUserId { get; set; }

        public int RoleId { get;set; }

        [ForeignKey(nameof(TenantUserId))]
        public virtual TTenantUser User { get;set; }

        [ForeignKey(nameof(RoleId))]
        public virtual TRole Role { get; set; }
    }
}
