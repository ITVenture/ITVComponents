using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Security.ApplicationToken;
using ITVComponents.WebCoreToolkit.Security.ApplicationToken;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Security.ApplicationToken
{
    internal class ApplicationTokenService<TContextImpl>: ApplicationTokenService<string, User, Role, Permission, UserRole, RolePermission, TenantUser, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, TContextImpl> where TContextImpl:AspNetSecurityContext<TContextImpl>
    {
        public ApplicationTokenService(TContextImpl dbContext):base(dbContext)
        {
        }
    }
}
