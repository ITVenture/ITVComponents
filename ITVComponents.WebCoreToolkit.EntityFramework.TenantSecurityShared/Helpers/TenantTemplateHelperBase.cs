using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.Formatting;
using ITVComponents.ParallelProcessing.TaskSchedulers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers
{
    public class TenantTemplateHelperBase<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TContext> : ITenantTemplateHelper<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>
        where TTenant: Tenant
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>, new ()
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>, new()
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>, new()
        where TTenantUser: TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>, new()
        where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>, new ()
        where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
        where TWebPluginConstant: WebPluginConstant<TTenant>, new()
        where TWebPluginGenericParameter: WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence: Sequence<TTenant>
        where TTenantSetting: TenantSetting<TTenant>, new()
        where TTenantFeatureActivation: TenantFeatureActivation<TTenant>, new()
        where TContext: ISecurityContext<TTenant, TUserId, TUser,TRole,TPermission,TUserRole,TRolePermission,TTenantUser, TRoleRole,TNavigationMenu,TTenantNavigation,TQuery,TQueryParameter,TTenantQuery,TWidget,TWidgetParam, TWidgetLocalization,TUserWidget,TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    {
        private readonly TContext db;
        private readonly ILogger<TenantTemplateHelperBase<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TContext>> logger;

        public TenantTemplateHelperBase(TContext db, ILogger<TenantTemplateHelperBase<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TContext>> logger)
        {
            this.db = db;
            this.logger = logger;
        }

        protected TContext Db => db;
        public TenantTemplateMarkup ExtractTemplate(TTenant tenant)
        {
            db.EnsureNavUniqueness();
            using (new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(db, new() { ShowAllTenants = true, HideGlobals = false }))
            {
                var roles = (from t in db.SecurityRoles
                    where t.TenantId == tenant.TenantId
                    select new RoleTemplateMarkup
                    {
                        IsSystemRole = t.IsSystemRole,
                        Name = t.RoleName,
                        Permissions = (from p in t.RolePermissions
                            select new PermissionTemplateMarkup
                            {
                                Name = p.Permission.PermissionName,
                                Description = p.Permission.Description,
                                Global = p.Permission.TenantId == null
                            }).ToArray()
                    }).ToArray();
                var settings = (from t in db.TenantSettings
                    where t.TenantId == tenant.TenantId
                    select new SettingTemplateMarkup
                    {
                        ParamName = t.SettingsKey.Replace("[","[[").Replace("]","]]"),
                        Value = t.SettingsValue.Replace("[", "[[").Replace("]", "]]"),
                        IsJsonSetting = t.JsonSetting
                    }).ToArray();
                var now = DateTime.Now;
                var features = (from t in db.TenantFeatureActivations
                    where
                        t.TenantId == tenant.TenantId &&
                        (t.ActivationEnd ?? now) >= now && (t.ActivationStart ?? now) <= now
                    select new FeatureTemplateMarkup
                    {
                        FeatureName = t.Feature.FeatureName,
                        DurationExpression = "Edit me!",
                        InfiniteDuration = false
                    }).ToArray();
                var plugIns = (from t in db.WebPlugins
                    where t.TenantId == tenant.TenantId
                    select new PlugInTemplateMarkup
                    {
                        AutoLoad = t.AutoLoad,
                        Constructor = t.Constructor.Replace("[", "[[").Replace("]", "]]"),
                        UniqueName = t.UniqueName
                    }).ToArray();
                var constants = (from t in db.WebPluginConstants
                    where t.TenantId == tenant.TenantId
                    select new ConstTemplateMarkup
                    {
                        Name=t.Name.Replace("[", "[[").Replace("]", "]]"),
                        Value=t.Value.Replace("[", "[[").Replace("]", "]]")
                    }).ToArray();

                var menus = (from t in db.TenantNavigation
                    where t.TenantId == tenant.TenantId
                    select new NavigationTemplateMarkup
                    {
                        Name = t.NavigationMenu.DisplayName,
                        UniqueKey = t.NavigationMenu.UrlUniqueness,
                        CustomPermission = t.Permission != null
                            ? new PermissionTemplateMarkup
                            {
                                Name = t.Permission.PermissionName,
                                Description = t.Permission.Description,
                                Global = t.TenantId == null
                            }
                            : null
                    }).ToArray();

                var queries = (from t in db.TenantDiagnosticsQueries
                    where t.TenantId == tenant.TenantId
                    select new QueryTemplateMarkup
                    {
                        Name = t.DiagnosticsQuery.DiagnosticsQueryName
                    }).ToArray();

                return new TenantTemplateMarkup
                {
                    Features = features,
                    Settings = settings,
                    Constants = constants,
                    PlugIns = plugIns,
                    Roles = roles,
                    Navigation = menus,
                    Queries = queries
                };
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

        public void ApplyTemplate(TTenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>> afterApply) 
        {
            db.EnsureNavUniqueness();
            using (new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(db, new() { ShowAllTenants = true, HideGlobals = false }))
            {
                var fmtRoot = new
                {
                    Now = DateTime.UtcNow,
                    Tenant = tenant
                };
                if (template.Roles != null)
                {
                    foreach (var role in template.Roles)
                    {
                        var tmp = GetRole(tenant.TenantId, role, true);
                        ApplyPermissions(tmp, role);
                    }
                }

                if (template.Settings != null)
                {
                    foreach (var setting in template.Settings)
                    {
                        setting.ParamName = fmtRoot.FormatText(setting.ParamName);
                        setting.Value = fmtRoot.FormatText(setting.Value);
                        var tmp = GetSetting(tenant.TenantId, setting, true);
                        if (tmp.TenantSettingId != 0)
                        {
                            tmp.JsonSetting = setting.IsJsonSetting;
                            tmp.SettingsValue = setting.Value;
                        }
                    }
                }

                if (template.Constants != null)
                {
                    foreach (var constant in template.Constants)
                    {
                        constant.Name = fmtRoot.FormatText(constant.Name);
                        constant.Value = fmtRoot.FormatText(constant.Value);
                        var tmp = GetConst(tenant.TenantId, constant, true);
                        if (tmp.WebPluginConstantId != 0)
                        {
                            tmp.Value = constant.Value;
                        }
                    }
                }

                if (template.PlugIns != null)
                {
                    foreach (var plugIn in template.PlugIns)
                    {
                        plugIn.UniqueName = fmtRoot.FormatText(plugIn.UniqueName);
                        plugIn.Constructor = fmtRoot.FormatText(plugIn.Constructor);
                        var tmp = GetPlugIn(tenant.TenantId, plugIn, true);
                        if (tmp.WebPluginId != 0)
                        {
                            tmp.AutoLoad = plugIn.AutoLoad;
                            tmp.Constructor = plugIn.Constructor;
                        }
                    }
                }

                if (template.Navigation != null)
                {
                    foreach (var menu in template.Navigation)
                    {
                        var tmp = GetNavigationMenu(tenant.TenantId, menu, true);
                        if (tmp.TenantNavigationMenuId != 0)
                        {
                            tmp.Permission = menu.CustomPermission != null
                                ? GetPermission(tenant.TenantId, menu.CustomPermission, true)
                                : null;
                        }
                    }
                }

                if (template.Queries != null)
                {
                    foreach (var query in template.Queries)
                    {
                        GetQuery(tenant.TenantId, query, true);
                    }
                }

                if (template.Features != null)
                {
                    foreach (var feature in template.Features)
                    {
                        var tmp = GetFeature(tenant.TenantId, feature, true);
                        if (!string.IsNullOrEmpty(feature.DurationExpression) && !feature.InfiniteDuration)
                        {
                            var timeEx = fmtRoot.FormatText(feature.DurationExpression);
                            var tt = new TimeTable(timeEx);
                            var nx = tt.GetNextExecutionTime(fmtRoot.Now);
                            tmp.ActivationStart = fmtRoot.Now;
                            tmp.ActivationEnd = nx;
                        }
                    }
                }

                db.SaveChanges();
                afterApply?.Invoke(db);
            }
        }

        public void RevokeTemplate(TTenant tenant, TenantTemplateMarkup template, Action<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>> afterRevoke)
        {
            db.EnsureNavUniqueness();
            using (new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(db, new() { ShowAllTenants = true, HideGlobals = false }))
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
                        var tmp = GetNavigationMenu(tenant.TenantId, menu, false);
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
                            tmp.ActivationEnd = DateTime.UtcNow;
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
                var tmp = GetPermission(role.TenantId, perm, true);
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
        }

        protected virtual void RevokePermissions(TRole role, RoleTemplateMarkup template, IList<int> permissionsToCheck)
        {
            foreach (var perm in template.Permissions)
            {
                var tmp = GetPermission(role.TenantId, perm, false);
                if (tmp != null)
                {
                    if (!perm.Global && !permissionsToCheck.Contains(tmp.PermissionId))
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
                n.TenantId == tenantId && (n.ActivationStart ?? nowU) <= nowU && (n.ActivationEnd ?? nowU) >= nowU);
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
            bool addIfMissing)
        {
            var mnu = db.Navigation.First(n => n.UrlUniqueness == menu.UniqueKey);
            var retVal = db.TenantNavigation.LocalFirstOrDefault(n =>
                n.TenantId == tenantId && n.NavigationMenuId == mnu.NavigationMenuId);
            if (retVal == null && addIfMissing)
            {
                retVal = new TTenantNavigation
                {
                    TenantId = tenantId,
                    Permission = menu.CustomPermission != null
                        ? GetPermission(tenantId, menu.CustomPermission, true)
                        : null,
                    NavigationMenuId = mnu.NavigationMenuId
                };
                EnsureParents(tenantId, mnu);
                db.TenantNavigation.Add(retVal);
            }
            else if (addIfMissing && menu.CustomPermission != null)
            {
                retVal.Permission = GetPermission(tenantId, menu.CustomPermission, true);
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
                n.PermissionName.ToLower() == perm.Name.ToLower() && (n.TenantId == tenantId || (perm.Global && n.TenantId == null)));
            if (retVal== null && perm.Global)
            {
                throw new InvalidOperationException($"Global Role {perm.Name} was not found!");
            }

            if (retVal == null && addIfMissing)
            {
                retVal = new TPermission
                {
                    TenantId = tenantId,
                    PermissionName = perm.Name,
                    Description = perm.Description
                };
                db.Permissions.Add(retVal);
            }

            return retVal;
        }

        protected virtual bool PermissionUsed(int permissionId)
        {
            return false;
        }

        private void EnsureParents(int tenantId, TNavigationMenu mnu)
        {
            var m = mnu.Parent;
            while (m != null)
            {
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
