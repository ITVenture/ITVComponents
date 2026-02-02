using ITVComponents.EFRepo.Extensions;
using ITVComponents.Formatting;
using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.ParallelProcessing.TaskSchedulers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers
{
    public class TenantTemplateHelperBase<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig, TContext> : ITenantTemplateHelper<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>
        where TTenant: Tenant
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new ()
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TTenantUser: TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>, new()
        where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>, new ()
        where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
        where TWebPluginConstant: WebPluginConstant<TTenant>, new()
        where TWebPluginGenericParameter: WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
        where TSequence: Sequence<TTenant>
        where TTenantSetting: TenantSetting<TTenant>, new()
        where TTenantFeatureActivation: TenantFeatureActivation<TTenant>, new()
        where TContext: ISecurityContext<TTenant, TUserId, TUser,TRole,TPermission,TUserRole,TRolePermission,TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,TTenantNavigation,TQuery,TQueryParameter,TTenantQuery,TWidget,TWidgetParam, TWidgetLocalization,TUserWidget,TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        private readonly TContext db;
        private readonly ILogger<TenantTemplateHelperBase<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig, TContext>> logger;

        public TenantTemplateHelperBase(TContext db, ILogger<TenantTemplateHelperBase<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig, TContext>> logger)
        {
            this.db = db;
            this.logger = logger;
        }

        protected TContext Db => db;
        public TenantTemplateMarkup ExtractTemplate(TTenant tenant)
        {
            db.EnsureNavUniqueness();
            using (new FullSecurityAccessHelper<TTrustConfig>(db, new() { ShowAllTenants = true, HideGlobals = false }))
            {
                var roles = (from t in db.SecurityRoles.Include(n => n.RolePermissions).Include(n => n.PermittedRoles)
                            .Include(n => n.PermittedGlobalRoles).ThenInclude(g => g.GlobalRole)
                    where t.TenantId == tenant.TenantId select t).AsEnumerable()
                    .Select(SelectRoleTemplateMarkup).ToArray();
                var settings = (from t in db.TenantSettings
                    where t.TenantId == tenant.TenantId
                    select t).AsEnumerable().Select(SelectSettingTemplateMarkup).ToArray();
                var now = DateTime.Now;
                var features = (from t in db.TenantFeatureActivations
                    where
                        t.TenantId == tenant.TenantId &&
                        (t.ActivationEnd ?? now) >= now && (t.ActivationStart ?? now) <= now
                    select t).AsEnumerable().Select(SelectFeatureTemplateMarkup).ToArray();
                var plugIns = (from t in db.WebPlugins.Include(p => p.Parameters)
                    where t.TenantId == tenant.TenantId
                    select t).AsEnumerable().Select(SelectPlugInTemplateMarkup).ToArray();
                var constants = (from t in db.WebPluginConstants
                    where t.TenantId == tenant.TenantId
                    select t).AsEnumerable().Select(SelectConstTemplateMarkup).ToArray();

                var menus = (from t in db.TenantNavigation
                    where t.TenantId == tenant.TenantId
                    select t).AsEnumerable().Select(SelectNavigationTemplateMarkup).ToArray();

                var queries = (from t in db.TenantDiagnosticsQueries
                    where t.TenantId == tenant.TenantId
                    select t).AsEnumerable().Select(SelectQueryTemplateMarkup).ToArray();

                var permissions = (from t in db.Permissions
                    where t.TenantId == tenant.TenantId
                    select t).AsEnumerable().Select(SelectPermissionTemplateMarkup).ToArray();
                return new TenantTemplateMarkup
                {
                    Features = features,
                    Settings = settings,
                    Constants = constants,
                    PlugIns = plugIns,
                    Roles = roles,
                    Navigation = menus,
                    Queries = queries,
                    ExplicitPermissions = permissions
                };
            }
        }

        protected virtual PermissionTemplateMarkup SelectPermissionTemplateMarkup(TPermission permissionInst)
        {
            return new PermissionTemplateMarkup
            {
                Description = permissionInst.Description,
                Global = false,
                Name = permissionInst.PermissionName
            };
        }

        protected virtual QueryTemplateMarkup SelectQueryTemplateMarkup(TTenantQuery queryImpl)
        {
            return new QueryTemplateMarkup
            {
                Name = queryImpl.DiagnosticsQuery.DiagnosticsQueryName
            };
        }

        protected virtual NavigationTemplateMarkup SelectNavigationTemplateMarkup(TTenantNavigation navigationInst)
        {
            return new NavigationTemplateMarkup
            {
                Name = navigationInst.NavigationMenu.DisplayName,
                UniqueKey = navigationInst.NavigationMenu.UrlUniqueness,
                CustomPermission = navigationInst.Permission != null
                    ? navigationInst.Permission.PermissionName
                    : null
            };
        }

        protected virtual ConstTemplateMarkup SelectConstTemplateMarkup(TWebPluginConstant constInst)
        {
            return new ConstTemplateMarkup
            {
                Name=constInst.Name.Replace("[", "[[").Replace("]", "]]"),
                Value=constInst.Value.Replace("[", "[[").Replace("]", "]]")
            };
        }

        protected virtual PlugInTemplateMarkup SelectPlugInTemplateMarkup(TWebPlugin pluginInst)
        {
            return new PlugInTemplateMarkup
            {
                AutoLoad = pluginInst.AutoLoad,
                Constructor = pluginInst.Constructor.Replace("[", "[[").Replace("]", "]]"),
                UniqueName = pluginInst.UniqueName.Replace("[", "[[").Replace("]", "]]"),
                GenericArguments = pluginInst.Parameters.Select(n => SelectPlugInGenericArgumentTemplateMarkup(n)).ToArray()
            };
        }

        protected virtual PlugInGenericArgumentTemplateMarkup SelectPlugInGenericArgumentTemplateMarkup(TWebPluginGenericParameter plugInGenericArgumentInst)
        {
            return new PlugInGenericArgumentTemplateMarkup{GenericTypeName = plugInGenericArgumentInst.GenericTypeName};
        }

        protected virtual FeatureTemplateMarkup SelectFeatureTemplateMarkup(TTenantFeatureActivation tenantFeatureInst)
        {
            return new FeatureTemplateMarkup
            {
                FeatureName = tenantFeatureInst.Feature.FeatureName,
                DurationExpression = "Edit me!",
                InfiniteDuration = false
            };
        }

        protected virtual SettingTemplateMarkup SelectSettingTemplateMarkup(TTenantSetting tenantSettingInst)
        {
            return new SettingTemplateMarkup
            {
                ParamName = tenantSettingInst.SettingsKey.Replace("[","[[").Replace("]","]]"),
                Value = tenantSettingInst.SettingsValue.Replace("[", "[[").Replace("]", "]]"),
                IsJsonSetting = tenantSettingInst.JsonSetting
            };
        }

        protected virtual RoleTemplateMarkup SelectRoleTemplateMarkup(TRole roleInst)
        {
            return new RoleTemplateMarkup
            {
                IsSystemRole = roleInst.IsSystemRole,
                Name = roleInst.RoleName,
                Permissions = (from p in roleInst.RolePermissions
                    where p.RoleRoleId == null && p.OriginId == null
                    select p.Permission.PermissionName).ToArray(),
                RoleGrants = (from g in roleInst.PermittedRoles select 
                    GetRoleGrantTitle(roleInst,g)).ToArray(),
                GlobalRoleGrants = (from p in roleInst.PermittedGlobalRoles
                    where p.RoleRoleId == null && p.OriginId == null
                    select p.GlobalRole.RoleName).ToArray()
            };
        }

        protected virtual string GetRoleGrantTitle(TRole role, TRoleRole roleRole)
        {
            if (roleRole.PermittedRole.TenantId == role.TenantId)
            {
                return roleRole.PermittedRole.RoleName;
            }

            throw new InvalidOperationException(
                "Permitted Roles in different Tenant is not supported with this templateHelper instance.");
        }

        public void ApplyAllTenantsFor(int tenantTypeId)
        {
            using (new FullSecurityAccessHelper<TTrustConfig>(db,
                       new() { ShowAllTenants = true, HideGlobals = false }))
            {
                var tp = db.TenantTypes.Include(n => n.TenantTemplate).First(n => n.TenantTypeId == tenantTypeId);
                if (tp.TenantTemplate != null){
                var tpi = JsonHelper.FromJsonString<TenantTemplateMarkup>(tp.TenantTemplate.Markup,
                    SerializationTypingMode.NativePolymorphism);
                var tenants = (from t in db.Tenants where t.TenantTypeId == tenantTypeId select t).ToList();
                var i = 0;
                //PrepareAllEntities();
                foreach (var tenant in tenants)
                {
                    ApplyTemplatePrivate(tenant, tpi, false);
                    i++;
                    if (i % 1000 == 0)
                    {
                        Console.WriteLine("Saving 1000...");
                        db.SaveChanges();
                        Console.WriteLine("jawoll...");
                    }
                }

                db.SaveChanges();
                }
            }
        }

        public void ApplyTemplate(TTenant tenant, TenantTemplateMarkup template)
        {
            ApplyTemplate(tenant, template, null);
        }

        public void RevokeTemplate(TTenant tenant, TenantTemplateMarkup template)
        {
            RevokeTemplate(tenant,template,null);
        }

        public void ApplyTemplate(TTenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> afterApply) 
        {
            db.EnsureNavUniqueness();
            using (new FullSecurityAccessHelper<TTrustConfig>(db, new() { ShowAllTenants = true, HideGlobals = false }))
            {
                ApplyTemplatePrivate(tenant, template,true);
                afterApply?.Invoke(db);
            }
        }

        private void ApplyTemplatePrivate(TTenant tenant, TenantTemplateMarkup template, bool autoSave)
        {
            var fmtRoot = new
            {
                Now = DateTime.UtcNow,
                Tenant = tenant
            };

            var pns = new List<string>();
            if (template.ExplicitPermissions != null)
            {
                foreach (var perm in template.ExplicitPermissions)
                {
                    var tmp = GetPermission(tenant.TenantId, perm, true);
                    pns.Add(tmp.PermissionName);
                }
            }

            if (template.Roles != null)
            {
                var rn = new List<string>();
                foreach (var role in template.Roles)
                {
                    var tmp = GetRole(tenant.TenantId, role, true);
                    rn.Add(tmp.RoleName);
                    ApplyPermissions(tmp, role);
                    ApplyRoleGrants(tenant, tmp, role);
                    ApplyGlobalRoleGrants(tenant, tmp, role);
                }

                var rmRoles = (from t in db.SecurityRoles.Include(r => r.PermittedRoles).Include(r => r.PermissiveRoles).Include(r => r.RolePermissions).Where(n => n.TenantId == tenant.TenantId)
                               join r in rn on t.RoleName.ToLower() equals r.ToLower() into lj
                               from l in lj.DefaultIfEmpty()
                               where string.IsNullOrEmpty(l)
                               select t).ToArray();
                db.RolePermissions.RemoveRange(rmRoles.SelectMany(n => n.RolePermissions));
                db.RoleRoles.RemoveRange(rmRoles.SelectMany(n => n.PermissiveRoles).Union(rmRoles.SelectMany(n => n.PermittedRoles)));
                db.SecurityRoles.RemoveRange(rmRoles);
            }

            if (template.Settings != null)
            {
                var settingsNames = new List<string>();
                foreach (var s in template.Settings)
                {
                    var setting = new SettingTemplateMarkup { IsJsonSetting = s.IsJsonSetting };
                    setting.ParamName = fmtRoot.FormatText(s.ParamName);
                    settingsNames.Add(setting.ParamName);
                    setting.Value = fmtRoot.FormatText(s.Value);
                    var tmp = GetSetting(tenant.TenantId, setting, true);
                    if (tmp.TenantSettingId != 0)
                    {
                        tmp.JsonSetting = setting.IsJsonSetting;
                        tmp.SettingsValue = setting.Value;
                    }
                }

                db.TenantSettings.RemoveRange(from t in db.TenantSettings.Where(n => n.TenantId == tenant.TenantId)
                                              join r in settingsNames
                    on t.SettingsKey.ToLower() equals r.ToLower() into lj
                                              from l in lj.DefaultIfEmpty()
                                              where string.IsNullOrEmpty(l)
                                              select t);
            }

            if (template.Constants != null)
            {
                var constNames = new List<string>();
                foreach (var c in template.Constants)
                {
                    var constant = new ConstTemplateMarkup();
                    constant.Name = fmtRoot.FormatText(c.Name);
                    constant.Value = fmtRoot.FormatText(c.Value);
                    constNames.Add(constant.Name);
                    var tmp = GetConst(tenant.TenantId, constant, true);
                    if (tmp.WebPluginConstantId != 0)
                    {
                        tmp.Value = constant.Value;
                    }
                }

                db.WebPluginConstants.RemoveRange(from t in db.WebPluginConstants.Where(n => n.TenantId == tenant.TenantId)
                                                  join r in constNames
                                                      on t.Name.ToLower() equals r.ToLower() into lj
                                                  from l in lj.DefaultIfEmpty()
                                                  where string.IsNullOrEmpty(l)
                                                  select t);
            }

            if (template.PlugIns != null)
            {
                var piKeys = new List<string>();
                foreach (var p in template.PlugIns)
                {
                    var plugIn = new PlugInTemplateMarkup
                    {
                        AutoLoad = p.AutoLoad,
                        GenericArguments = (from t in p.GenericArguments
                                            select new PlugInGenericArgumentTemplateMarkup
                                            { GenericTypeName = t.GenericTypeName, TypeExpression = t.TypeExpression })
                            .ToArray()
                    };
                    plugIn.UniqueName = fmtRoot.FormatText(plugIn.UniqueName);
                    plugIn.Constructor = fmtRoot.FormatText(plugIn.Constructor);
                    piKeys.Add(plugIn.UniqueName);
                    var tmp = GetPlugIn(tenant.TenantId, plugIn, true);
                    if (tmp.WebPluginId != 0)
                    {
                        tmp.AutoLoad = plugIn.AutoLoad;
                        tmp.Constructor = plugIn.Constructor;
                    }
                }

                var rems = (from t in db.WebPlugins.Include(p => p.Parameters)
                        .Where(n => n.TenantId == tenant.TenantId)
                            join r in piKeys
                                on t.UniqueName.ToLower() equals r.ToLower() into lj
                            from l in lj.DefaultIfEmpty()
                            where string.IsNullOrEmpty(l)
                            select t).ToArray();
                db.GenericPluginParams.RemoveRange(rems.SelectMany(n => n.Parameters));
                db.WebPlugins.RemoveRange(rems);
            }

            if (template.Navigation != null)
            {
                var urlUqs = new List<string>();
                foreach (var menu in template.Navigation)
                {
                    var tmp = GetNavigationMenu(tenant.TenantId, menu, true, urlUqs);
                    if (tmp.TenantNavigationMenuId != 0)
                    {
                        tmp.Permission = menu.CustomPermission != null
                            ? GetPermission(tenant.TenantId, menu.CustomPermission)
                            : null;
                    }
                }

                db.TenantNavigation.RemoveRange(from t in db.TenantNavigation.Include(n => n.NavigationMenu)
                                                join r in urlUqs on t.NavigationMenu.UrlUniqueness.ToLower() equals r.ToLower() into lj
                                                from j in lj.DefaultIfEmpty()
                                                where string.IsNullOrEmpty(j) && t.TenantId == tenant.TenantId
                                                select t);
            }

            if (template.Queries != null)
            {
                var qn = new List<string>();
                foreach (var query in template.Queries)
                {
                    qn.Add(query.Name);
                    GetQuery(tenant.TenantId, query, true);
                }

                db.TenantDiagnosticsQueries.RemoveRange(
                    from t in db.TenantDiagnosticsQueries.Include(n => n.DiagnosticsQuery)
                    join
                        r in qn on t.DiagnosticsQuery.DiagnosticsQueryName.ToLower() equals r.ToLower() into lj
                    from j in lj.DefaultIfEmpty()
                    where string.IsNullOrEmpty(j) && t.TenantId == tenant.TenantId
                    select t);
            }

            if (template.Features != null)
            {
                var fn = new List<string>();
                foreach (var feature in template.Features)
                {
                    var tmp = GetFeature(tenant.TenantId, feature, true);
                    fn.Add(feature.FeatureName);
                    if (!string.IsNullOrEmpty(feature.DurationExpression) && !feature.InfiniteDuration && tmp.TenantFeatureActivationId == 0)
                    {
                        var timeEx = fmtRoot.FormatText(feature.DurationExpression);
                        var tt = new TimeTable(timeEx);
                        var nx = tt.GetNextExecutionTime(fmtRoot.Now);
                        tmp.ActivationStart = fmtRoot.Now;
                        tmp.ActivationEnd = nx;
                    }
                }

                db.TenantFeatureActivations.RemoveRange(from t in db.TenantFeatureActivations.Include(a => a.Feature)
                                                        join r in fn on t.Feature.FeatureName.ToLower() equals r.ToLower() into lj
                                                        from l in lj.DefaultIfEmpty()
                                                        where t.TenantId == tenant.TenantId && string.IsNullOrEmpty(l)
                                                        select t);
            }

            db.Permissions.RemoveRange(from p in db.Permissions.Where(n => n.TenantId == tenant.TenantId)
                                       join
                                           r in pns on p.PermissionName.ToLower() equals r.ToLower() into lj
                                       from l in lj.DefaultIfEmpty()
                                       where string.IsNullOrEmpty(l)
                                       select p);
            if (autoSave)
            {
                db.SaveChanges();
            }
        }

        protected virtual void ApplyRoleGrants(TTenant tenant, TRole tmp, RoleTemplateMarkup role)
        {
            db.RoleRoles.Include(n => n.PermittedRole).Include(n => n.PermissiveRole).Where(n => n.PermissiveRole == tmp).Load();
            var permittedRoles = role.RoleGrants.Select(n => SelectPermittedRole(tenant, n)).ToArray();
            var rn = db.RoleRoles.Local.Where(n => n.PermissiveRole == tmp).Select(n => new { n.PermittedRole.RoleName, n.PermittedRole.TenantId })
                .Union(permittedRoles.Select(n => new { n.RoleName, n.TenantId })).ToArray();
            var raw = (from r in rn
                join ori in db.RoleRoles.Local.Where(oriL => oriL.PermissiveRole == tmp)
                    on r equals new { ori.PermittedRole.RoleName, ori.PermittedRole.TenantId } into oriLj
                from oriNj in oriLj.DefaultIfEmpty()
                join nw in permittedRoles
                    on r equals new { nw.RoleName, nw.TenantId } into nwLj
                from nwNj in nwLj.DefaultIfEmpty()
                where (oriNj == null && nwNj != null) || (oriNj != null && nwNj == null)
                select new { r.RoleName, r.TenantId, Add = nwNj != null, Delete = oriNj != null }).ToArray();
            db.RoleRoles.RemoveRange(from t in db.RoleRoles.Local.Where(n => n.PermissiveRole == tmp) join d in raw.Where(n => n.Delete)
                on new {t.PermittedRole.RoleName,t.PermittedRole.TenantId} equals new {d.RoleName,d.TenantId}
                                     select t);
            db.RoleRoles.AddRange(from t in permittedRoles join i in raw.Where(n => n.Add) on 
                new {t.RoleName, t.TenantId} equals new {i.RoleName, i.TenantId}
                                  select new TRoleRole
                                  {
                                      PermittedRole = t,
                                      PermissiveRole = tmp
                                  });
        }

        protected virtual void ApplyGlobalRoleGrants(TTenant tenant, TRole tmp, RoleTemplateMarkup role)
        {
            db.GlobalToLocalRoles.Include(n => n.LocalRole).Include(n => n.GlobalRole).Where(n => n.LocalRole == tmp).Load();
            var permittedRoles = role.GlobalRoleGrants.Select(n => SelectGlobalRole(n)).ToArray();
            var rn = db.GlobalToLocalRoles.Local.Where(n => n.LocalRole == tmp).Select(n => new { n.GlobalRole.RoleName})
                .Union(permittedRoles.Select(n => new { n.RoleName})).ToArray();
            var raw = (from r in rn
                join ori in db.GlobalToLocalRoles.Local.Where(oriL => oriL.LocalRole== tmp)
                    on r equals new { ori.GlobalRole.RoleName } into oriLj
                from oriNj in oriLj.DefaultIfEmpty()
                join nw in permittedRoles
                    on r equals new { nw.RoleName } into nwLj
                from nwNj in nwLj.DefaultIfEmpty()
                where (oriNj == null && nwNj != null) || (oriNj != null && nwNj == null)
                select new { r.RoleName, Add = nwNj != null, Delete = oriNj != null }).ToArray();
            db.GlobalToLocalRoles.RemoveRange(from t in db.GlobalToLocalRoles.Local.Where(n => n.LocalRole== tmp)
                join d in raw.Where(n => n.Delete)
                    on new { t.GlobalRole.RoleName} equals new { d.RoleName}
                select t);
            db.GlobalToLocalRoles.AddRange(from t in permittedRoles
                join i in raw.Where(n => n.Add) on
                    new { t.RoleName } equals new { i.RoleName }
                select new TGRoleLRole()
                {
                    GlobalRole = t,
                    LocalRole = tmp
                });
        }

        protected virtual TGlobalRole SelectGlobalRole(string roleName)
        {
            var ret = db.GlobalRoles.LocalFirstOrDefault(n => n.RoleName == roleName);
            if (ret == null)
            {
                throw new Exception($"Role {roleName} was not found!");
            }

            return ret;
        }

        protected virtual TRole SelectPermittedRole(TTenant tenant, string s)
        {
            var ret = db.SecurityRoles.FirstOrDefault(n => n.TenantId == tenant.TenantId && n.RoleName == s);
            if (ret == null)
            {
                ret = db.SecurityRoles.Local.FirstOrDefault(n => n.TenantId == tenant.TenantId && n.RoleName == s);
            }

            if (ret == null)
            {
                throw new Exception($"Role {s} was not found!");
            }

            return ret;
        }

        public void RevokeTemplate(TTenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> afterRevoke)
        {
            db.EnsureNavUniqueness();
            using (new FullSecurityAccessHelper<TTrustConfig>(db, new() { ShowAllTenants = true, HideGlobals = false }))
            {
                var fmtRoot = new
                {
                    Now = DateTime.UtcNow,
                    Tenant = tenant
                };

                List<int> permissionsToCheck = new List<int>();
                if (template.Roles != null)
                {
                    foreach (var role in template.Roles)
                    {
                        var tmp = GetRole(tenant.TenantId, role, false);
                        if (tmp != null)
                        {
                            RevokePermissions(tmp, role, permissionsToCheck);
                            var users = (from u in db.TenantUsers
                                join r in db.TenantUserRoles on u.TenantUserId equals r.TenantUserId
                                where u.TenantId == tenant.TenantId
                                select r).ToArray();
                            db.TenantUserRoles.RemoveRange(users);
                            RevokeRole(tenant.TenantId, tmp);
                            db.SecurityRoles.Remove(tmp);
                        }
                    }
                }

                if (template.Settings != null)
                {
                    foreach (var setting in template.Settings)
                    {
                        setting.ParamName = fmtRoot.FormatText(setting.ParamName);
                        setting.Value = fmtRoot.FormatText(setting.Value);
                        var tmp = GetSetting(tenant.TenantId, setting, false);
                        if (tmp != null)
                        {
                            RevokeSetting(tenant.TenantId, tmp);
                            db.TenantSettings.Remove(tmp);
                        }
                    }
                }

                if (template.Constants != null)
                {
                    foreach (var constant in template.Constants)
                    {
                        constant.Name = fmtRoot.FormatText(constant.Name);
                        constant.Value = fmtRoot.FormatText(constant.Value);
                        var tmp = GetConst(tenant.TenantId, constant, false);
                        if (tmp != null)
                        {
                            RevokeConstant(tenant.TenantId, tmp);
                            db.WebPluginConstants.Remove(tmp);
                        }
                    }
                }

                if (template.PlugIns != null)
                {
                    foreach (var plugIn in template.PlugIns)
                    {
                        plugIn.UniqueName = fmtRoot.FormatText(plugIn.UniqueName);
                        plugIn.Constructor = fmtRoot.FormatText(plugIn.Constructor);
                        var tmp = GetPlugIn(tenant.TenantId, plugIn, false);
                        if (tmp != null)
                        {
                            RevokePlugin(tenant.TenantId, tmp);
                            db.WebPlugins.Remove(tmp);
                        }
                    }
                }

                if (template.Navigation != null)
                {
                    foreach (var menu in template.Navigation)
                    {
                        var tmp = GetNavigationMenu(tenant.TenantId, menu, false, null);
                        if (tmp != null)
                        {
                            if (tmp.PermissionId != null && !permissionsToCheck.Contains(tmp.PermissionId.Value))
                            {
                                permissionsToCheck.Add(tmp.PermissionId.Value);
                            }

                            RevokeNavigation(tenant.TenantId, tmp);
                            db.TenantNavigation.Remove(tmp);
                        }
                    }
                }

                if (template.Queries != null)
                {
                    foreach (var query in template.Queries)
                    {
                        var tmp = GetQuery(tenant.TenantId, query, false);
                        if (tmp != null)
                        {
                            RevokeQuery(tenant.TenantId, tmp);
                            db.TenantDiagnosticsQueries.Remove(tmp);
                        }
                    }
                }

                if (template.Features != null)
                {
                    foreach (var feature in template.Features)
                    {
                        var tmp = GetFeature(tenant.TenantId, feature, false);
                        if (tmp != null && feature.InfiniteDuration)
                        {
                            RevokeFeature(tenant.TenantId, tmp);
                            //tmp.ActivationEnd = DateTime.UtcNow;
                        }
                    }
                }

                db.SaveChanges();
                RemoveUnUsedPermissions(tenant.TenantId, permissionsToCheck);
                db.SaveChanges();
                afterRevoke?.Invoke(db);
            }
        }

        protected virtual void RevokeFeature(int tenantId, TTenantFeatureActivation feature)
        {
        }

        protected virtual void RevokeQuery(int tenantId, TTenantQuery query)
        {
        }

        protected virtual void RevokeNavigation(int tenantId, TTenantNavigation menu)
        {
        }

        protected virtual void RevokePlugin(int tenantId, TWebPlugin plugIn)
        {
        }

        protected virtual void RevokeConstant(int tenantId, TWebPluginConstant constant)
        {
        }

        protected virtual void RevokeSetting(int tenantId, TTenantSetting setting)
        {
        }

        protected virtual void RevokeRole(int tenantId, TRole role)
        {
        }

        protected virtual void ApplyPermissions(TRole role, RoleTemplateMarkup template)
        {
            foreach (var perm in template.Permissions)
            {
                var tmp = GetPermission(role.TenantId, perm);
                if (role.RoleId == 0 || tmp.PermissionId == 0 || !db.RolePermissions.Any(n =>
                        n.TenantId == role.TenantId && n.PermissionId == tmp.PermissionId && n.RoleId == role.RoleId))
                {
                    TRolePermission lnk = new TRolePermission
                    {
                        Permission = tmp,
                        TenantId = role.TenantId,
                        Role = role
                    };

                    db.RolePermissions.Add(lnk);
                }
            }

            var removes = (from t in db.RolePermissions.Where(n => n.RoleRoleId == null && n.OriginId == null &&
                                                        n.RoleId == role.RoleId) join r in template.Permissions
                    on t.Permission.PermissionName.ToLower() equals r.ToLower() into lj
                from l in lj.DefaultIfEmpty()
                          where string.IsNullOrEmpty(l)
                          select t).ToArray();
            db.RolePermissions.RemoveRange(removes);
        }

        protected virtual void RevokePermissions(TRole role, RoleTemplateMarkup template, IList<int> permissionsToCheck)
        {
            foreach (var perm in template.Permissions)
            {
                var tmp = GetPermission(role.TenantId, perm);
                if (tmp != null)
                {
                    if (tmp.TenantId != null && !permissionsToCheck.Contains(tmp.PermissionId))
                    {
                        permissionsToCheck.Add(tmp.PermissionId);
                    }

                    var lnk = db.RolePermissions.LocalFirstOrDefault(n =>
                        n.TenantId == role.TenantId && n.PermissionId == tmp.PermissionId && n.RoleId == role.RoleId);
                    if (lnk != null)
                    {
                        db.RolePermissions.Remove(lnk);
                    }
                }
            }
        }

        protected virtual TTenantFeatureActivation GetFeature(int tenantId, FeatureTemplateMarkup feature,
            bool addIfMissing)
        {
            var nowU = DateTime.UtcNow;
            var ftu = db.Features.First(n => n.FeatureName.ToLower() == feature.FeatureName.ToLower());
            var retVal = db.TenantFeatureActivations.LocalFirstOrDefault(n =>
                n.TenantId == tenantId);
            if (retVal == null && addIfMissing)
            {
                retVal = new TTenantFeatureActivation()
                {
                    TenantId = tenantId,
                    FeatureId = ftu.FeatureId
                };

                db.TenantFeatureActivations.Add(retVal);
            }

            return retVal;
        }

        protected virtual TTenantQuery GetQuery(int tenantId, QueryTemplateMarkup query, bool addIfMissing)
        {
            var qry = db.DiagnosticsQueries.First(n => n.DiagnosticsQueryName.ToLower() == query.Name.ToLower());
            var retVal = db.TenantDiagnosticsQueries.LocalFirstOrDefault(n =>
                n.DiagnosticsQueryId == qry.DiagnosticsQueryId && n.TenantId == tenantId);
            if (retVal == null && addIfMissing)
            {
                retVal = new TTenantQuery
                {
                    TenantId = tenantId,
                    DiagnosticsQueryId = qry.DiagnosticsQueryId
                };

                db.TenantDiagnosticsQueries.Add(retVal);
            }

            return retVal;
        }

        protected virtual TTenantNavigation GetNavigationMenu(int tenantId, NavigationTemplateMarkup menu,
            bool addIfMissing, List<string> urlUqs)
        {
            var mnu = db.Navigation.First(n => n.UrlUniqueness == menu.UniqueKey);
            urlUqs.AddIfMissing(mnu.UrlUniqueness);
            var retVal = db.TenantNavigation.LocalFirstOrDefault(n =>
                n.TenantId == tenantId && n.NavigationMenuId == mnu.NavigationMenuId);
            if (retVal == null && addIfMissing)
            {
                retVal = new TTenantNavigation
                {
                    TenantId = tenantId,
                    Permission = menu.CustomPermission != null
                        ? GetPermission(tenantId, menu.CustomPermission)
                        : null,
                    NavigationMenuId = mnu.NavigationMenuId
                };
                EnsureParents(tenantId, mnu, urlUqs);
                db.TenantNavigation.Add(retVal);
            }
            else if (addIfMissing && menu.CustomPermission != null)
            {
                retVal.Permission = GetPermission(tenantId, menu.CustomPermission);
            }

            return retVal;
        }

        protected virtual TWebPlugin GetPlugIn(int tenantId, PlugInTemplateMarkup plugIn, bool addIfMissing)
        {
            var retVal = db.WebPlugins.LocalFirstOrDefault(n => n.TenantId == tenantId && n.UniqueName == plugIn.UniqueName);
            if (retVal == null && addIfMissing)
            {
                retVal = new TWebPlugin()
                {
                    TenantId = tenantId,
                    Constructor= plugIn.Constructor,
                    UniqueName = plugIn.UniqueName,
                    AutoLoad = plugIn.AutoLoad
                };

                db.WebPlugins.Add(retVal);
                db.GenericPluginParams.AddRange((from t in plugIn.GenericArguments
                                                 select new TWebPluginGenericParameter
                                                 {
                                                     GenericTypeName = t.GenericTypeName,
                                                     TypeExpression = t.TypeExpression,
                                                     Plugin = retVal
                                                 }));
            }
            else if (retVal != null)
            {
                retVal.Constructor = plugIn.Constructor;
                retVal.AutoLoad = plugIn.AutoLoad;
                var oriparams = db.GenericPluginParams.Where(n => n.WebPluginId == retVal.WebPluginId).ToArray();
                var ac = (from op in oriparams
                    join jp in plugIn.GenericArguments on op.GenericTypeName equals jp.GenericTypeName into lj
                    from l in lj.DefaultIfEmpty()
                    select new { O = op, J = l }).ToArray();
                foreach(var i in ac)
                {
                    if (i.J != null)
                    {
                        i.O.TypeExpression = i.J.TypeExpression;
                    }
                    else
                    {
                        db.GenericPluginParams.Remove(i.O);
                    }
                }
            }

            return retVal;
        }

        protected virtual TWebPluginConstant GetConst(int tenantId, ConstTemplateMarkup constant, bool addIfMissing)
        {
            var retVal = db.WebPluginConstants.LocalFirstOrDefault(n => n.TenantId == tenantId && n.Name == constant.Name);
            if (retVal == null && addIfMissing)
            {
                retVal = new TWebPluginConstant
                {
                    TenantId = tenantId,
                    Value = constant.Value,
                    Name = constant.Name
                };

                db.WebPluginConstants.Add(retVal);
            }

            return retVal;
        }

        protected virtual TTenantSetting GetSetting(int tenantId, SettingTemplateMarkup setting, bool addIfMissing)
        {
            var retVal = db.TenantSettings.LocalFirstOrDefault(n =>
                n.TenantId == tenantId && n.SettingsKey == setting.ParamName);
            if (retVal == null && addIfMissing)
            {
                retVal = new TTenantSetting()
                {
                    TenantId = tenantId,
                    SettingsKey = setting.ParamName,
                    JsonSetting = setting.IsJsonSetting,
                    SettingsValue = setting.Value
                };
                db.TenantSettings.Add(retVal);
            }

            return retVal;
        }

        protected virtual TRole GetRole(int tenantId, RoleTemplateMarkup role, bool addIfMissing)
        {
            var retVal = db.SecurityRoles.LocalFirstOrDefault(n =>
                n.TenantId == tenantId && n.RoleName.ToLower() == role.Name.ToLower());
            if (retVal== null && addIfMissing)
            {
                retVal = new TRole
                {
                    TenantId = tenantId,
                    RoleName = role.Name,
                    IsSystemRole = role.IsSystemRole
                };
                db.SecurityRoles.Add(retVal);
            }

            return retVal;
        }

        protected virtual TPermission GetPermission(int tenantId, PermissionTemplateMarkup perm, bool addIfMissing)
        {
            var retVal = db.Permissions.LocalFirstOrDefault(n =>
                n.TenantId == tenantId && n.PermissionName.ToLower() == perm.Name.ToLower());
            if (retVal == null && addIfMissing)
            {
                retVal = new TPermission()
                {
                    TenantId = tenantId,
                    PermissionName = perm.Name,
                    Description = perm.Description
                };
                db.Permissions.Add(retVal);
            }

            return retVal;
        }

        protected virtual TPermission GetPermission(int tenantId, string perm)
        {
            var retVal = db.Permissions.LocalFirstOrDefault(n =>
                n.PermissionName.ToLower() == perm && n.TenantId == tenantId)??
                         db.Permissions.LocalFirstOrDefault(n =>
                             n.PermissionName.ToLower() == perm && n.TenantId == null);
            if (retVal== null)
            {
                throw new InvalidOperationException($"Global Permission {perm} was not found!");
            }

            return retVal;
        }

        protected virtual bool PermissionUsed(int permissionId)
        {
            return false;
        }

        private void EnsureParents(int tenantId, TNavigationMenu mnu, List<string> urlUqs)
        {
            var m = mnu.Parent;
            while (m != null)
            {
                urlUqs.AddIfMissing(m.UrlUniqueness);
                var parentEntry = db.TenantNavigation.LocalFirstOrDefault(n =>
                    n.TenantId == tenantId && n.NavigationMenuId == m.NavigationMenuId);
                if (parentEntry == null)
                {
                    parentEntry = new TTenantNavigation
                    {
                        NavigationMenuId = m.NavigationMenuId,
                        TenantId = tenantId
                    };

                    db.TenantNavigation.Add(parentEntry);
                    logger.LogInformation($"TenantNavigation for {m.UrlUniqueness} is created with default settings.");
                }

                m = m.Parent;
            }
        }

        private void RemoveUnUsedPermissions(int tenantId, IList<int> permissionsToCheck)
        {
            foreach (var permId in permissionsToCheck)
            {
                if (!db.RolePermissions.Any(n => n.PermissionId == permId && n.TenantId == tenantId) &&
                    !db.TenantNavigation.Any(n => n.PermissionId == permId && n.TenantId == tenantId) &&
                    !PermissionUsed(permId))
                {
                    var perm = db.Permissions.First(n => n.TenantId == tenantId && n.PermissionId == permId);
                    db.Permissions.Remove(perm);
                }
            }
        }
    }
}
