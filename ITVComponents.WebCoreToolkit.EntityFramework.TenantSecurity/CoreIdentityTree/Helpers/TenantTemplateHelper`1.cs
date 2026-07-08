using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.TemplateHandling;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.ConfigMarkupModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Helpers
{
    public class TenantTreeTemplateHelper<TContext>:TenantTemplateHelperBase<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission, HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty,AssetTemplate,AssetTemplatePath,AssetTemplateGrant, AssetTemplateFeature, SharedAsset,SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant, HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting, HierarchyTenantFeatureActivation, HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin, HierarchyTenantContextSecurityTrustConfig, TContext>
        where TContext : AspNetTreeSecurityContext<TContext>
    {
        private const string ParentTag = "PARENT##";
        public TenantTreeTemplateHelper(IToolkitContextFactory contextFactory, ILogger<TenantTreeTemplateHelper<TContext>> logger, IEnumerable<ITenantTemplatePartHandler> partHandlers = null) : base(contextFactory, logger, partHandlers)
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

            // Check the change-tracker's Local buffer as well as the database: a sibling role created earlier in
            // this same apply pass (but not yet SaveChanges'd) lives only in Local. Without this fallback a grant
            // to a not-yet-persisted sibling would wrongly report "Role ... was not found!".
            var ret = Db.SecurityRoles.Local.FirstOrDefault(r => r.TenantId == tenantId && r.RoleName == s)
                      ?? Db.SecurityRoles.FirstOrDefault(r => r.TenantId == tenantId && r.RoleName == s);
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

        protected override ExternalOAuthServiceTemplateMarkup SelectExternalOAuthServiceTemplateMarkup(HierarchyExternalOAuthService serviceInst)
        {
            var tmp = base.SelectExternalOAuthServiceTemplateMarkup(serviceInst);
            return new HierarchyExternalOAuthServiceTemplateMarkup
            {
                UniqueConnectionName = tmp.UniqueConnectionName,
                AuthorizationEndpoint = tmp.AuthorizationEndpoint,
                TokenEndpoint = tmp.TokenEndpoint,
                RevocationEndpoint = tmp.RevocationEndpoint,
                ClientId = tmp.ClientId,
                Scope = tmp.Scope,
                Global = tmp.Global,
                AuthenticationType = tmp.AuthenticationType,
                Inheritable = serviceInst.Inheritable
            };
        }

        protected override HierarchyExternalOAuthService GetExternalOAuthService(int tenantId, ExternalOAuthServiceTemplateMarkup service, bool addIfMissing)
        {
            var retVal = base.GetExternalOAuthService(tenantId, service, addIfMissing);
            if (retVal != null && service is HierarchyExternalOAuthServiceTemplateMarkup hsvc)
            {
                retVal.Inheritable = hsvc.Inheritable;
            }

            return retVal;
        }
    }
}
