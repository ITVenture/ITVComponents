using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    [Index(nameof(RoleId), nameof(PermissionId), nameof(TenantId), nameof(OriginId),IsUnique=true,Name="IX_UniqueRolePermission")]
    public abstract class RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TTenantUser: TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TTenant : Tenant
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    {
        [Key]
        public int RolePermissionId { get; set; }

        public int? RoleId { get;set; }

        public int PermissionId { get; set; }

        public int TenantId { get; set; }

        public int? OriginId { get; set; }

        public int? RoleRoleId { get; set; }

        [ForeignKey(nameof(PermissionId))]
        public virtual TPermission Permission{ get;set; }

        [ForeignKey(nameof(RoleId))]
        public virtual TRole Role { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        [ForeignKey(nameof(OriginId))]
        public virtual TRolePermission Origin { get; set; }

        [ForeignKey(nameof(RoleRoleId))]
        public virtual TRoleRole LinkedBy { get; set; }

        public virtual ICollection<TRolePermission> RoleInheritanceChildren { get; set; } = new List<TRolePermission>();
    }
}
