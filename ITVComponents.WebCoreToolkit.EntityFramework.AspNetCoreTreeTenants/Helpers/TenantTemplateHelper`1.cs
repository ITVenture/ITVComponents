using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Helpers
{
    public class TenantTreeTemplateHelper<TContext>:TenantTemplateHelperBase<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,AssetTemplate,AssetTemplatePath,AssetTemplateGrant, AssetTemplateFeature, SharedAsset,SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting, HierarchyTenantFeatureActivation, HierarchyTenantContextSecurityTrustConfig, TContext>
        where TContext : AspNetTreeSecurityContext<TContext>
    {
        private const string ParentTag = "PARENT##";
        public TenantTreeTemplateHelper(TContext db, ILogger<TenantTreeTemplateHelper<TContext>> logger) : base(db, logger)
        {
        }

        protected override Role SelectPermittedRole(HierarchyTenant tenant, string s)
        {
            var tenantId = tenant.TenantId;
            if (s.StartsWith(ParentTag) && tenant.ParentTenantId != null)
            {
                tenantId = tenant.ParentTenantId.Value;
                s = s.Substring(ParentTag.Length);
            }
            else if (s.StartsWith(ParentTag))
            {
                throw new Exception("selected Tenant does not have a parent.");
            }

            var ret = Db.SecurityRoles.FirstOrDefault(r => r.TenantId == tenantId && r.RoleName == s);
            if (ret == null)
            {
                throw new Exception($"Role {s} was not found!");
            }

            return ret;
        }

        protected override string GetRoleGrantTitle(Role role, RoleRole roleRole)
        {
            if (roleRole.PermittedRole.TenantId == role.TenantId)
            {
                return roleRole.PermittedRole.RoleName;
            }

            return $"{ParentTag}{roleRole.PermittedRole.RoleName}";
        }
    }
}
