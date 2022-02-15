using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.AspNetCore.Identity;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.CustomUserProperty;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Role;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Security
{
    internal class AspNetDbSecurityRepository:TenantSecurityShared.Security.DbSecurityRepository<string, Models.User, Role, Permission, UserRole, RolePermission, TenantUser, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, UserWidget, CustomUserProperty>
    {
        private const string default1 = "Identity.Application";

        public AspNetDbSecurityRepository(AspNetSecurityContext securityContext):base(securityContext)
        {
        }

        protected override IEnumerable<UserRole> AllRoles(Models.User user)
        {
            return user.TenantUsers.SelectMany(u => u.Roles);
        }

        protected override Expression<Func<Models.User, bool>> UserFilter(WebCoreToolkit.Models.User user)
        {
            return (n =>
                n.UserName == user.UserName && n.AuthenticationType.AuthenticationTypeName == user.AuthenticationType);
        }

        protected override Expression<Func<Models.User, bool>> UserFilter(string[] userLabels, string authType)
        {
            return n => userLabels.Contains(n.UserName) &&
                        n.AuthenticationType.AuthenticationTypeName == authType;
        }

        protected override WebCoreToolkit.Models.User SelectUser(Models.User src)
        {
            return new WebCoreToolkit.Models.User
            {
                UserName = src.UserName,
                AuthenticationType = src.AuthenticationType.AuthenticationTypeName
            };
        }

        protected override IEnumerable<CustomUserProperty<string, Models.User>> UserProps(Models.User user)
        {
            return user.UserProperties;
        }

        protected override Expression<Func<Models.User, string>> UserId { get; } = (user) => user.Id;
    }
}
