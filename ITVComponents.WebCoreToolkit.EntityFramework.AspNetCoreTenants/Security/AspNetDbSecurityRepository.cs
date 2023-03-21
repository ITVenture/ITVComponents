using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Castle.Core.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.CustomUserProperty;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Role;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Security
{
    internal class AspNetDbSecurityRepository<TImpl>:TenantSecurityShared.Security.DbSecurityRepository<string, Models.User, Role, Permission, UserRole, RolePermission, TenantUser, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser>
    where TImpl:AspNetSecurityContext<TImpl>
    {
        private const string default1 = "Identity.Application";

        public AspNetDbSecurityRepository(TImpl securityContext, ILogger<AspNetDbSecurityRepository<TImpl>> logger):base(securityContext, logger)
        {
        }

        protected override IEnumerable<UserRole> AllRoles(Models.User user)
        {
            return user.TenantUsers.SelectMany(u => u.Roles);
        }

        protected override Expression<Func<Models.User, bool>> UserFilter(WebCoreToolkit.Models.User user)
        {
            return (n =>
                n.UserName.ToLower()== user.UserName.ToLower() && (n.AuthenticationType == null || user.AuthenticationType == null || n.AuthenticationType.AuthenticationTypeName == user.AuthenticationType));
        }

        protected override Expression<Func<Models.User, bool>> UserFilter(string[] userLabels, string authType)
        {
            var lbl = (from t in userLabels select t.ToLower()).ToArray();
            return n => lbl.Contains(n.UserName.ToLower()) &&
                        (n.AuthenticationType == null || n.AuthenticationType.AuthenticationTypeName == authType);
        }

        protected override WebCoreToolkit.Models.User SelectUser(Models.User src)
        {
            return new WebCoreToolkit.Models.User
            {
                UserName = src.UserName,
                AuthenticationType = src.AuthenticationType?.AuthenticationTypeName
            };
        }

        protected override IEnumerable<CustomUserProperty<string, Models.User>> UserProps(Models.User user)
        {
            return user.UserProperties;
        }

        protected override Expression<Func<Models.User, string>> UserId { get; } = (user) => user.Id;
    }
}
