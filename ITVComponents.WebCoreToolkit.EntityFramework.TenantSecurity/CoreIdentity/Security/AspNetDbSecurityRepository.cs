using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Castle.Core.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.CustomUserProperty;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.Role;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Security
{
    internal class AspNetDbSecurityRepository<TImpl>:Shared.Security.DbSecurityRepository<Tenant, string, Models.User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig>
    where TImpl:AspNetSecurityContext<TImpl>
    {
        private const string default1 = "Identity.Application";

        public AspNetDbSecurityRepository(ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection.IToolkitContextFactory contextFactory, ILogger<AspNetDbSecurityRepository<TImpl>> logger, ITVComponents.WebCoreToolkit.Caching.IEntityChangeSignal changeSignal = null):base(contextFactory, logger, changeSignal)
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
        protected override BaseTenantContextSecurityTrustConfig ConfigureTrustConfigImpl(BaseTenantContextSecurityTrustConfig trustConfig, string callingMethod)
        {
            return trustConfig;
        }

        protected override FlatExternalOAuthService GetExternalServiceInternal(IBaseTenantContext<Tenant, FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig> context, string name)
        {
            return context.ExternalOAuthServices.FirstOrDefault(n => n.CalculatedUniqueServiceName == name)??context.ExternalOAuthServices.FirstOrDefault(n =>
                       n.UniqueConnectionName == name && n.TenantId != null &&
                       n.TenantId == context.CurrentTenantId) ??
                   context.ExternalOAuthServices.FirstOrDefault(n =>
                       n.UniqueConnectionName == name && n.TenantId == null);
        }
    }
}
