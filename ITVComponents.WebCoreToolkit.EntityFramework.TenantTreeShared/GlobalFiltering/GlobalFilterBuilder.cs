using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.EFRepo.Options;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.GlobalFiltering
{
    public static class GlobalFilterBuilder
    {
        public static void ConfigureGlobalFilters<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TContext>(IServiceCollection services)
            where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
            where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
            where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
            where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
            where TTenantUser: TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
            where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>
            where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>
            where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
            where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
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
            where TTenant: HierarchyTenant
            where TContext : IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, HierarchyTenantContextSecurityTrustConfig>
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation: TenantFeatureActivation<TTenant>
            where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        {
            services.Configure<DbContextModelBuilderOptions<TContext>>(o =>
            {
                o.ConfigureGlobalFilter<TPermission>(pr => ShowAllTenants || !FilterAvailable || !IncludeParentTree && pr.TenantId != null  && pr.Tenant.TenantName.ToLower() == CurrentTenant || IncludeParentTree  && pr.TenantId != null && CurrentTenantTree.Contains(pr.TenantId.Value) || pr.TenantId == null && !HideGlobals);
                o.ConfigureGlobalFilter<TTenantNavigation>(nav => ShowAllTenants || !FilterAvailable || !IncludeParentTree && nav.Tenant.TenantName.ToLower() == CurrentTenant && (nav.PermissionId == null || nav.Permission.TenantId == null || nav.Permission.Tenant.TenantName.ToLower() == CurrentTenant) || IncludeParentTree && CurrentTenantTree.Contains(nav.TenantId) && (nav.PermissionId == null || nav.Permission.TenantId == null || nav.Permission.Tenant.TenantName.ToLower() == CurrentTenant));
                o.ConfigureGlobalFilter<TNavigationMenu>(nav => string.IsNullOrEmpty(nav.Url) || ShowAllTenants || !FilterAvailable || !IncludeParentTree && (nav.IsPublic || nav.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant)) && ((nav.PermissionId == null || nav.EntryPoint.TenantId == null || nav.EntryPoint.Tenant.TenantName.ToLower() == CurrentTenant)) || IncludeParentTree && nav.Tenants.Any(n => CurrentTenantTree.Contains(n.TenantId)) && ((nav.PermissionId == null || nav.EntryPoint.TenantId == null || nav.EntryPoint.Tenant.TenantName.ToLower() == CurrentTenant)));
                o.ConfigureGlobalFilter<TRolePermission>(perm => ShowAllTenants || !FilterAvailable || perm.Tenant.TenantName.ToLower() == CurrentTenant && perm.Permission != null);
                o.ConfigureGlobalFilter<TQuery>(qry => ShowAllTenants || !FilterAvailable || !IncludeParentTree && qry.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant) || IncludeParentTree && qry.Tenants.Any(n => CurrentTenantTree.Contains(n.TenantId)));
                o.ConfigureGlobalFilter<TQueryParameter>(param => ShowAllTenants || !FilterAvailable || !IncludeParentTree && param.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant) || IncludeParentTree && param.DiagnosticsQuery.Tenants.Any(n => CurrentTenantTree.Contains(n.TenantId)));
                o.ConfigureGlobalFilter<TTenantQuery>(tdq => ShowAllTenants || !FilterAvailable || !IncludeParentTree && tdq.Tenant.TenantName.ToLower() == CurrentTenant || IncludeParentTree && CurrentTenantTree.Contains(tdq.TenantId));
                o.ConfigureGlobalFilter<TTenantSetting>(stt => ShowAllTenants || !FilterAvailable || !IncludeParentTree && stt.Tenant.TenantName.ToLower() == CurrentTenant || IncludeParentTree && CurrentTenantTree.Contains(stt.TenantId));
                o.ConfigureGlobalFilter<TTenantUser>(tu => !FilterAvailable || ((ShowAllTenants || !IncludeParentTree && tu.Tenant.TenantName.ToLower() == CurrentTenant || IncludeParentTree && CurrentTenantTree.Contains(tu.TenantId)) && (!HideDisabledUsers || (tu.Enabled ?? true))));
                o.ConfigureGlobalFilter<TRole>(ro => ShowAllTenants || !FilterAvailable || !IncludeParentTree && ro.Tenant.TenantName.ToLower() == CurrentTenant || IncludeParentTree && CurrentTenantTree.Contains(ro.TenantId));
                o.ConfigureGlobalFilter<TUserRole>(ur => !FilterAvailable || ((ShowAllTenants || (!IncludeParentTree && ur.User.Tenant.TenantName.ToLower() == CurrentTenant && ur.Role.Tenant.TenantName.ToLower() == CurrentTenant) || (IncludeParentTree && CurrentTenantTree.Contains(ur.User.TenantId) && CurrentTenantTree.Contains(ur.Role.TenantId))) && (!HideDisabledUsers || (ur.User.Enabled ?? true))));
                o.ConfigureGlobalFilter<TWebPlugin>(wp => ShowAllTenants || !FilterAvailable || !IncludeParentTree && wp.TenantId != null && wp.Tenant.TenantName.ToLower() == CurrentTenant || IncludeParentTree && wp.TenantId != null && CurrentTenantTree.Contains(wp.TenantId.Value) || wp.TenantId == null && !HideGlobals);
                o.ConfigureGlobalFilter<TWebPluginGenericParameter>(wp => ShowAllTenants || !FilterAvailable || !IncludeParentTree && wp.Plugin.TenantId != null && wp.Plugin.Tenant.TenantName.ToLower() == CurrentTenant || IncludeParentTree && wp.Plugin.TenantId != null && CurrentTenantTree.Contains(wp.Plugin.TenantId.Value) || wp.Plugin.TenantId == null && !HideGlobals);
                o.ConfigureGlobalFilter<TWebPluginConstant>(wc => ShowAllTenants || !FilterAvailable || !IncludeParentTree && wc.TenantId != null && wc.Tenant.TenantName.ToLower() == CurrentTenant || IncludeParentTree && wc.TenantId != null && CurrentTenantTree.Contains(wc.TenantId.Value) || wc.TenantId == null && !HideGlobals);
                o.ConfigureGlobalFilter<TWidget>(dw => ShowAllTenants || !FilterAvailable || dw.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant));
                o.ConfigureGlobalFilter<TWidgetParam>(dw => ShowAllTenants || !FilterAvailable || dw.Parent.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant));
                o.ConfigureGlobalFilter<TUserWidget>(uw => ShowAllTenants || !FilterAvailable || (uw.Widget.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName.ToLower() == CurrentTenant) && uw.Tenant.TenantName == CurrentTenant && uw.UserName == CurrentUserName));
                o.ConfigureGlobalFilter<TTenantFeatureActivation>(fa => ShowAllTenants || !FilterAvailable || fa.Tenant.TenantName.ToLower() == CurrentTenant);
                o.ConfigureGlobalFilter<TClientAppUser>(ca => ShowAllTenants || !FilterAvailable || ca.TenantUser.Tenant.TenantName.ToLower() == CurrentTenant);
                o.ConfigureGlobalFilter<TSequence>(sq => ShowAllTenants || !FilterAvailable || !IncludeParentTree && sq.Tenant.TenantName.ToLower() == CurrentTenant || IncludeParentTree && CurrentTenantTree.Contains(sq.TenantId));
                ConfigureTrees<TContext, TUserId>(o);
            });
        }

        public static MethodInfo GetConfigureMethod(Dictionary<Type,Dictionary<string, Type>> genericArguments)
        {
            return typeof(GlobalFilterBuilder).ImplementGenericMethods(genericArguments)
                .First(n => n.Name == "ConfigureGlobalFilters");
            /*.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod)
            .First(n => n.IsGenericMethod && n.Name == "ConfigureGlobalFilters");
        var p = t.GetGenericArguments();
        var p2 = (from n in p join a in genericArguments on n.Name equals a.Key select a.Value).ToArray();
        return t.MakeGenericMethod(p2);*/
        }

        public static void ConfigureTrees<TContext, TUserId>(DbContextModelBuilderOptions<TContext> modelBuilderOptions)
        {
            ConfigureUpwardsTree(modelBuilderOptions);
            ConfigureUpwardsRoleTree<TContext,TUserId>(modelBuilderOptions);
            ConfigureDownwardsTree(modelBuilderOptions);
            ConfigureDownwardsRoleTree<TContext,TUserId>(modelBuilderOptions);
        }

        public static void ConfigureUpwardsTree<TContext>(
            DbContextModelBuilderOptions<TContext> modelBuilderOptions)
        {
            modelBuilderOptions.ConfigureGlobalFilter<UpwardsTenantView>(u => FilterAvailable && !ShowAllTenants && (IncludeParentTree && u.OutermostLeafTenantName == CurrentTenant || !IncludeParentTree && u.ParentTenantName == u.OutermostLeafTenantName && u.ParentTenantName == CurrentTenant));
        }

        public static void ConfigureUpwardsRoleTree<TContext,TUserId>(
            DbContextModelBuilderOptions<TContext> modelBuilderOptions)
        {
            modelBuilderOptions.ConfigureGlobalFilter<UpwardsRoleUserView<TUserId>>(u => true);
        }

        public static void ConfigureDownwardsTree<TContext>(
            DbContextModelBuilderOptions<TContext> modelBuilderOptions)
        {
            modelBuilderOptions.ConfigureGlobalFilter<DownwardsTenantView>(d => !FilterAvailable || ShowAllTenants || (IncludeChildTree && d.TopmostTenantName == CurrentTenant));
        }

        public static void ConfigureDownwardsRoleTree<TContext, TUserId>(
            DbContextModelBuilderOptions<TContext> modelBuilderOptions)
        {
            modelBuilderOptions.ConfigureGlobalFilter<DownwardsUserRoleView<TUserId>>(d => !FilterAvailable || ShowAllTenants || (IncludeChildTree && d.ViewpointTenantName == CurrentTenant));
        }

        [ExpressionPropertyRedirect("ShowAllTenants")]
        private static bool ShowAllTenants => false;

        [ExpressionPropertyRedirect("IncludeParentTree")]
        private static bool IncludeParentTree => false;

        [ExpressionPropertyRedirect("IncludeChildTree")]
        private static bool IncludeChildTree => false;

        [ExpressionPropertyRedirect("FilterAvailable")]
        private static bool FilterAvailable => false;

        [ExpressionPropertyRedirect("CurrentTenant")]
        private static string CurrentTenant => "";

        [ExpressionPropertyRedirect("CurrentTenantTree")]
        private static IQueryable<int> CurrentTenantTree => Array.Empty<int>().AsQueryable();

        [ExpressionPropertyRedirect("HideGlobals")]
        private static bool HideGlobals => false;

        [ExpressionPropertyRedirect("HideDisabledUsers")]
        private static bool HideDisabledUsers => false;

        [ExpressionPropertyRedirect("CurrentUserName")]
        private static string CurrentUserName => "";
    }
}
