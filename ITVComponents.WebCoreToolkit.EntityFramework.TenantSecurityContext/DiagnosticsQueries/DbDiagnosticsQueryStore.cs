using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.DiagnosticsQueries;
using Microsoft.AspNetCore.Http;
using DashboardParam = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.DashboardParam;
using DashboardWidget = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.DashboardWidget;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.DiagnosticsQueries
{
    /// <summary>
    /// DiagnosticsQueryStore that is bound to the Security Db-Context
    /// </summary>
    public class DbDiagnosticsQueryStore<TImpl>: DbDiagnosticsQueryStore<int, User, Role, Permission, UserRole, RolePermission, TenantUser, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser>
        where TImpl:SecurityContext<TImpl>
    {
        public DbDiagnosticsQueryStore(TImpl dbContext):base(dbContext)
        {
        }
    }
}
