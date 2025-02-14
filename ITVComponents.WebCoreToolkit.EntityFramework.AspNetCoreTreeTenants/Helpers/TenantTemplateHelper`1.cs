using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Helpers
{
    public class TenantTreeTemplateHelper<TContext>:TenantTemplateHelperBase<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,AssetTemplate,AssetTemplatePath,AssetTemplateGrant, AssetTemplateFeature, SharedAsset,SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting, HierarchyTenantFeatureActivation, HierarchyTenantContextSecurityTrustConfig, TContext>
        where TContext : AspNetTreeSecurityContext<TContext>
    {
        public TenantTreeTemplateHelper(TContext db, ILogger<TenantTreeTemplateHelper<TContext>> logger) : base(db, logger)
        {
        }
    }
}
