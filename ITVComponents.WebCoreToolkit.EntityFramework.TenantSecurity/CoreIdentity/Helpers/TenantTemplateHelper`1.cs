using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.TemplateHandling;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Helpers
{
    public class TenantTemplateHelper<TContext>:TenantTemplateHelperBase<Tenant, string, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,AssetTemplate,AssetTemplatePath,AssetTemplateGrant, AssetTemplateFeature, SharedAsset,SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig, TContext>
        where TContext : AspNetSecurityContext<TContext>
    {
        public TenantTemplateHelper(IToolkitContextFactory contextFactory, ILogger<TenantTemplateHelper<TContext>> logger, IEnumerable<ITenantTemplatePartHandler> partHandlers = null) : base(contextFactory, logger, partHandlers)
        {
        }
    }
}
