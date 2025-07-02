using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels;
using Microsoft.Extensions.Logging;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.CustomUserProperty;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.Role;
using User = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model.User;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Security
{
    internal class AspNetDbTreeSecurityRepository<TImpl>:TenantTreeShared.Security.DbSecurityRepository<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting, HierarchyTenantFeatureActivation, HierarchyTenantContextSecurityTrustConfig>
    where TImpl: AspNetTreeSecurityContext<TImpl>
    {
        private const string default1 = "Identity.Application";

        public AspNetDbTreeSecurityRepository(TImpl securityContext, ILogger<AspNetDbTreeSecurityRepository<TImpl>> logger):base(securityContext, logger)
        {
        }

        protected override IEnumerable<UserRole> AllRoles(User user)
        {
            return user.TenantUsers.SelectMany(u => u.Roles);
        }

        protected override Expression<Func<User, bool>> UserFilter(WebCoreToolkit.Models.User user)
        {
            return (n =>
                n.UserName.ToLower()== user.UserName.ToLower() && (n.AuthenticationType == null || user.AuthenticationType == null || n.AuthenticationType.AuthenticationTypeName == user.AuthenticationType));
        }

        protected override Expression<Func<User, bool>> UserFilter(string[] userLabels, string authType)
        {
            var lbl = (from t in userLabels select t.ToLower()).ToArray();
            return n => lbl.Contains(n.UserName.ToLower()) &&
                        (n.AuthenticationType == null || n.AuthenticationType.AuthenticationTypeName == authType);
        }

        protected override WebCoreToolkit.Models.User SelectUser(User src)
        {
            return new WebCoreToolkit.Models.User
            {
                UserName = src.UserName,
                AuthenticationType = src.AuthenticationType?.AuthenticationTypeName
            };
        }

        protected override IEnumerable<CustomUserProperty<string, User>> UserProps(User user)
        {
            return user.UserProperties;
        }

        protected override Expression<Func<User, string>> UserId { get; } = (user) => user.Id;

        protected override Expression<Func<UserTenantLevel<User>, string>> IdOfUserLevelRecord => n => n.User.Id;

        protected override HierarchyTenantContextSecurityTrustConfig ConfigureTrustConfigImpl(HierarchyTenantContextSecurityTrustConfig trustConfig, string callingMethod)
        {
            return trustConfig;
        }
    }
}
