using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.ConfigMarkupModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Helpers
{
    public class TenantTreeTemplateHelper<TContext>:TenantTemplateHelperBase<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,AssetTemplate,AssetTemplatePath,AssetTemplateGrant, AssetTemplateFeature, SharedAsset,SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting, HierarchyTenantFeatureActivation, HierarchyTenantContextSecurityTrustConfig, TContext>
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

        protected override SettingTemplateMarkup SelectSettingTemplateMarkup(HierarchyTenantSetting tenantSettingInst)
        {
            var tmp = base.SelectSettingTemplateMarkup(tenantSettingInst);
            return new HierarchyTenantSettingTemplateMarkup
            {
                ParamName = tmp.ParamName,
                IsJsonSetting = tmp.IsJsonSetting,
                Value = tmp.Value,
                Inheritable = tenantSettingInst.Inheritable
            };
        }

        protected override ConstTemplateMarkup SelectConstTemplateMarkup(HierarchyWebPluginConstant constInst)
        {
            var tmp = base.SelectConstTemplateMarkup(constInst);
            return new HierarchyWebPluginConstantTemplateMarkup
            {
                Name = tmp.Name,
                Value = tmp.Value,
                Inheritable = constInst.Inheritable
            };
        }

        protected override HierarchyWebPluginConstant GetConst(int tenantId, ConstTemplateMarkup constant, bool addIfMissing)
        {
            var retVal = base.GetConst(tenantId, constant, addIfMissing);
            if (retVal != null && constant is HierarchyWebPluginConstantTemplateMarkup hwpcm)
            {
                retVal.Inheritable = hwpcm.Inheritable;
            }
            return retVal;
        }

        protected override HierarchyTenantSetting GetSetting(int tenantId, SettingTemplateMarkup setting, bool addIfMissing)
        {
            var retVal = base.GetSetting(tenantId, setting, addIfMissing);
            if (retVal != null && setting is HierarchyTenantSettingTemplateMarkup htsm)
            {
                retVal.Inheritable = htsm.Inheritable;
            }

            return retVal;
        }

        protected override PlugInTemplateMarkup SelectPlugInTemplateMarkup(HierarchyWebPlugin pluginInst)
        {
            var tmp = base.SelectPlugInTemplateMarkup(pluginInst);
            return new HierarchyPlugInTemplateMarkup
            {
                GenericArguments = tmp.GenericArguments,
                AutoLoad = tmp.AutoLoad,
                Constructor = tmp.Constructor,
                UniqueName = tmp.UniqueName,
                Inheritable = pluginInst.Inheritable
            };
        }

        protected override HierarchyWebPlugin GetPlugIn(int tenantId, PlugInTemplateMarkup plugIn, bool addIfMissing)
        {
            var retVal = base.GetPlugIn(tenantId, plugIn, addIfMissing);
            if (retVal != null && plugIn is HierarchyPlugInTemplateMarkup hipt)
            {
                retVal.Inheritable = hipt.Inheritable;
            }

            return retVal;
        }
    }
}
