﻿using System;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Security.SharedAssets
{
    public class SharedAssetProvider<TContext>:SharedAssetInfoProvider<int, User, Role, Permission, UserRole, RolePermission, TenantUser, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, TContext>
    where TContext: SecurityContext<TContext>
    {
        public SharedAssetProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, TContext database, IServiceProvider services) : base(userNameMapper, securityRepo, database, services)
        {
        }
    }
}
