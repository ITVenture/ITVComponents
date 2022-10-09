using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    [Index(nameof(Name), IsUnique = true, Name = "UQ_AppPermissionSetName")]
    public class AppPermissionSet<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TAppPermission : AppPermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet:AppPermissionSet<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
    {
        [Key]
        public int AppPermissionSetId { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; }

        public virtual ICollection<TAppPermission> Permissions { get; set; } = new List<TAppPermission>();
    }
}
