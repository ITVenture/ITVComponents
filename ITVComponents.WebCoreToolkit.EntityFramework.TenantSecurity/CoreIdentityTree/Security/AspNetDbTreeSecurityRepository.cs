using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.VirtualModels;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.CustomUserProperty;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.Role;
using User = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.User;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Security
{
    internal class AspNetDbTreeSecurityRepository<TImpl>:TreeShared.Security.DbSecurityRepository<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting, HierarchyTenantFeatureActivation, HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin, HierarchyTenantContextSecurityTrustConfig>
    where TImpl: AspNetTreeSecurityContext<TImpl>
    {
        private const string default1 = "Identity.Application";

        public AspNetDbTreeSecurityRepository(TImpl securityContext, ISecurityAccessProvider securityAccessProvider, IOptions<ExternalOAuthServiceBufferingOptions> serviceBufferOptions, ILogger<AspNetDbTreeSecurityRepository<TImpl>> logger):base(securityContext, securityAccessProvider, serviceBufferOptions, logger)
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
