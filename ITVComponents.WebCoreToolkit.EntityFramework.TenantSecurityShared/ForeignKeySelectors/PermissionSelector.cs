using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Help.QueryExtenders;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.ForeignKeySelectors
{
    public class PermissionSelector<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser> : ForeignKeySelectorHelperBase<TPermission, int>
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
    {
        protected override Expression<Func<TPermission, string>> GetLabelExpressionImpl()
        {
            return p => p.PermissionName;
        }

        public override Sort[] DefaultSorts { get; } = new Sort[] { new Sort { Direction = SortDirection.Ascending, MemberName = "PermissionName" } };
    }
}
