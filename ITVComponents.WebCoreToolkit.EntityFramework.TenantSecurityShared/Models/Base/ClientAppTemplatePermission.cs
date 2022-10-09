using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base
{
    public class ClientAppTemplatePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TAppPermission : AppPermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
    {
        [Key]
        public int ClientAppTemplatePermissionId { get; set; }

        public int ClientAppTemplateId { get; set; }

        public int AppPermissionSetId { get; set; }

        public virtual TAppPermissionSet PermissionSet { get; set; }

        public virtual TClientAppTemplate ClientAppTemplate { get; set; }
    }
}
