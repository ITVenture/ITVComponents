using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.GlobalFiltering
{
    public static class GlobalFilterBuilder
    {
        public static void ConfigureGlobalFilters<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TContext>(IServiceCollection services)
            where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
            where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
            where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
            where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
            where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
            where TNavigationMenu : NavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
            where TTenantNavigation : TenantNavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
            where TQuery : DiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
            where TTenantQuery : TenantDiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
            where TQueryParameter : DiagnosticsQueryParameter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
            where TWidget : DashboardWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
            where TWidgetParam : DashboardParam<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
            where TWidgetLocalization : DashboardWidgetLocalization<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
            where TUserWidget : UserWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
            where TUserProperty : CustomUserProperty<TUserId, TUser>
            where TUser : class
            where TAssetTemplate : AssetTemplate<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
            where TAssetTemplatePath : AssetTemplatePath<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
            where TAssetTemplateGrant : AssetTemplateGrant<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
            where TAssetTemplateFeature : AssetTemplateFeature<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
            where TSharedAsset : SharedAsset<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
            where TSharedAssetUserFilter : SharedAssetUserFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
            where TSharedAssetTenantFilter : SharedAssetTenantFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
            where TAppPermission : AppPermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
            where TAppPermissionSet : AppPermissionSet<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
            where TClientAppTemplatePermission : ClientAppTemplatePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
            where TClientAppTemplate : ClientAppTemplate<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
            where TClientAppPermission : ClientAppPermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
            where TClientApp : ClientApp<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
            where TClientAppUser : ClientAppUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
            where TContext : ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser>
        {
            services.Configure<DbContextModelBuilderOptions<TContext>>(o =>
            {
                o.ConfigureGlobalFilter<TPermission>(pr => ShowAllTenants || !FilterAvailable || pr.TenantId != null && pr.Tenant.TenantName.ToLower() == CurrentTenant || pr.TenantId == null && !HideGlobals);
                o.ConfigureGlobalFilter<TTenantNavigation>(nav => ShowAllTenants || !FilterAvailable || nav.Tenant.TenantName.ToLower() == CurrentTenant && (nav.PermissionId == null || nav.Permission.TenantId == null || nav.Permission.Tenant.TenantName.ToLower() == CurrentTenant));
                o.ConfigureGlobalFilter<TNavigationMenu>(nav => string.IsNullOrEmpty(nav.Url) || ShowAllTenants || !FilterAvailable || nav.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant) && ((nav.PermissionId == null || nav.EntryPoint.TenantId == null || nav.EntryPoint.Tenant.TenantName.ToLower() == CurrentTenant)));
                o.ConfigureGlobalFilter<TRolePermission>(perm => ShowAllTenants || !FilterAvailable || perm.Tenant.TenantName.ToLower() == CurrentTenant && perm.Permission != null);
                o.ConfigureGlobalFilter<TQuery>(qry => ShowAllTenants || !FilterAvailable || qry.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant));
                o.ConfigureGlobalFilter<TQueryParameter>(param => ShowAllTenants || !FilterAvailable || param.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant));
                o.ConfigureGlobalFilter<TTenantQuery>(tdq => ShowAllTenants || !FilterAvailable || tdq.Tenant.TenantName.ToLower() == CurrentTenant);
                o.ConfigureGlobalFilter<TenantSetting>(stt => ShowAllTenants || !FilterAvailable || stt.Tenant.TenantName.ToLower() == CurrentTenant);
                o.ConfigureGlobalFilter<TTenantUser>(tu => !FilterAvailable || ((ShowAllTenants || tu.Tenant.TenantName.ToLower() == CurrentTenant) && (!HideDisabledUsers || (tu.Enabled ?? true))));
                o.ConfigureGlobalFilter<TRole>(ro => ShowAllTenants || !FilterAvailable || ro.Tenant.TenantName.ToLower() == CurrentTenant);
                o.ConfigureGlobalFilter<TUserRole>(ur => !FilterAvailable || ((ShowAllTenants || (ur.User.Tenant.TenantName.ToLower() == CurrentTenant && ur.Role.Tenant.TenantName.ToLower() == CurrentTenant)) && (!HideDisabledUsers || (ur.User.Enabled ?? true))));
                o.ConfigureGlobalFilter<WebPlugin>(wp => ShowAllTenants || !FilterAvailable || wp.TenantId != null && wp.Tenant.TenantName.ToLower() == CurrentTenant || wp.TenantId == null && !HideGlobals);
                o.ConfigureGlobalFilter<WebPluginGenericParameter>(wp => ShowAllTenants || !FilterAvailable || wp.Plugin.TenantId != null && wp.Plugin.Tenant.TenantName.ToLower() == CurrentTenant || wp.Plugin.TenantId == null && !HideGlobals);
                o.ConfigureGlobalFilter<WebPluginConstant>(wc => ShowAllTenants || !FilterAvailable || wc.TenantId != null && wc.Tenant.TenantName.ToLower() == CurrentTenant || wc.TenantId == null && !HideGlobals);
                o.ConfigureGlobalFilter<TWidget>(dw => ShowAllTenants || !FilterAvailable || dw.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant));
                o.ConfigureGlobalFilter<TWidgetParam>(dw => ShowAllTenants || !FilterAvailable || dw.Parent.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant));
                o.ConfigureGlobalFilter<TUserWidget>(uw => ShowAllTenants || !FilterAvailable || (uw.Widget.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant) && uw.Tenant.TenantName == CurrentTenant && uw.UserName == CurrentUserName));
                o.ConfigureGlobalFilter<TenantFeatureActivation>(fa => ShowAllTenants || !FilterAvailable || fa.Tenant.TenantName.ToLower() == CurrentTenant);
                o.ConfigureGlobalFilter<TClientAppUser>(ca => ShowAllTenants || !FilterAvailable || ca.TenantUser.Tenant.TenantName.ToLower() == CurrentTenant);
            });
        }

        public static MethodInfo GetConfigureMethod(Dictionary<string, Type> genericArguments)
        {
            var t = typeof(GlobalFilterBuilder)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod)
                .First(n => n.IsGenericMethod && n.Name == "ConfigureGlobalFilters");
            var p = t.GetGenericArguments();
            var p2 = (from n in p join a in genericArguments on n.Name equals a.Key select a.Value).ToArray();
            return t.MakeGenericMethod(p2);
        }

        [ExpressionPropertyRedirect("ShowAllTenants")]
        private static bool ShowAllTenants => false;

        [ExpressionPropertyRedirect("FilterAvailable")]
        private static bool FilterAvailable => false;

        [ExpressionPropertyRedirect("CurrentTenant")]
        private static string CurrentTenant => "";

        [ExpressionPropertyRedirect("HideGlobals")]
        private static bool HideGlobals => false;

        [ExpressionPropertyRedirect("HideDisabledUsers")]
        private static bool HideDisabledUsers => false;

        [ExpressionPropertyRedirect("CurrentUserName")]
        private static string CurrentUserName => "";
    }
}
