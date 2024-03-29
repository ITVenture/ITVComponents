﻿using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.DiagnosticsQueries;
using Microsoft.AspNetCore.Identity;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.CustomUserProperty;
using DashboardParam = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.DashboardParam;
using DashboardWidget = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.DashboardWidget;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Role;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.DiagnosticsQueries
{
    /// <summary>
    /// DiagnosticsQueryStore that is bound to the Security Db-Context
    /// </summary>
    public class AspNetDbDiagnosticsQueryStore<TImpl>: DbDiagnosticsQueryStore<string, User, Role, Permission, UserRole, RolePermission, TenantUser, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate,AssetTemplatePath,AssetTemplateGrant,AssetTemplateFeature,SharedAsset,SharedAssetUserFilter,SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser>
    where TImpl:AspNetSecurityContext<TImpl>
    {
        public AspNetDbDiagnosticsQueryStore(TImpl dbContext):base(dbContext)
        {
        }
    }
}
