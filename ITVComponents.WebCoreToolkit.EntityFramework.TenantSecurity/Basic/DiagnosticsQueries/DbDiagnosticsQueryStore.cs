using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using Microsoft.AspNetCore.Http;
using DashboardParam = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.DashboardParam;
using DashboardWidget = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.DashboardWidget;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.DiagnosticsQueries
{
    /// <summary>
    /// DiagnosticsQueryStore that is bound to the Security Db-Context
    /// </summary>
    public class DbDiagnosticsQueryStore<TImpl>: DbDiagnosticsQueryStore<Tenant, int, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, FlatWebPlugin, FlatWebPluginConstant, FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig>
        where TImpl:SecurityContext<TImpl>
    {
        public DbDiagnosticsQueryStore(TImpl dbContext):base(dbContext)
        {
        }
    }
}
