using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ITVComponents.Formatting;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Security
{
    internal class DbSecurityRepository:TenantSecurityShared.Security.DbSecurityRepository<int, User, Role, Permission, UserRole, RolePermission, TenantUser, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, UserWidget, CustomUserProperty>
    {
        public DbSecurityRepository(SecurityContext securityContext):base(securityContext)
        {
        }

        protected override IEnumerable<UserRole> AllRoles(User user)
        {
            return user.TenantUsers.SelectMany(u => u.Roles);
        }

        protected override Expression<Func<User, bool>> UserFilter(WebCoreToolkit.Models.User user)
        {
            return (n =>
                n.UserName == user.UserName && n.AuthenticationType.AuthenticationTypeName == user.AuthenticationType);
        }

        protected override Expression<Func<User, bool>> UserFilter(string[] userLabels, string authType)
        {
            return n => userLabels.Contains(n.UserName) &&
                        n.AuthenticationType.AuthenticationTypeName == authType;
        }

        protected override WebCoreToolkit.Models.User SelectUser(User src)
        {
            return new WebCoreToolkit.Models.User
            {
                UserName = src.UserName,
                AuthenticationType = src.AuthenticationType.AuthenticationTypeName
            };
        }

        protected override IEnumerable<CustomUserProperty<int, User>> UserProps(User user)
        {
            return user.UserProperties;
        }

        protected override Expression<Func<User, int>> UserId { get; } = (user) => user.UserId;
    }
}
