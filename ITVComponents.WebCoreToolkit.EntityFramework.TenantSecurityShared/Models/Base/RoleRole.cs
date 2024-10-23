using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    public abstract class RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TRole:Role<TTenant,TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TRoleRole:RoleRole<TTenant,TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TTenant : Tenant
    where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    {
        public int RoleRoleId { get; set; }

        public int? PermissiveRoleId { get; set; }

        public int? PermittedRoleId { get; set; }

        [ForeignKey(nameof(PermittedRoleId))]
        public virtual TRole PermittedRole { get; set; }

        [ForeignKey(nameof(PermissiveRoleId))]
        public virtual TRole PermissiveRole { get; set; }

        public virtual ICollection<TRolePermission> ResultingLinks { get; set; } = new List<TRolePermission>();
    }
}
