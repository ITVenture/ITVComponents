using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Interceptors
{
    public class SecurityModificationInterceptor<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole> : TenantSecurityShared.Interceptors.SecurityModificationInterceptor<TTenant,TUserId,TUser,TRole,TPermission,TUserRole,TRolePermission,TTenantUser,TRoleRole>
    where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>, new()
    where TTenantUser: TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    where TTenant : HierarchyTenant
    where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    {
        public SecurityModificationInterceptor(IServiceProvider services) : base(services)
        {
        }

        protected override void HandleCrossTenantReference(DbContextEventData eventData, EntityEntry entry, List<Action<DbContext>> modifyActions)
        {
        }
    }
}
