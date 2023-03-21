using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class ContextExtensions
    {
        public static bool VerifyRoleName<TContext>(this TContext dbContext, string permissionName)
        {
            var dlg = typeof(ContextExtensions).GetMethod<Func<TContext, string, bool>>(dbContext.GetType(), nameof(VerifyRoleName));
            if (dlg != null)
            {
                return dlg(dbContext, permissionName);
            }

            return false;
        }

        public static bool VerifyRoleName<TContext, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser>(this TContext dbContext, string permissionName)
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
        where TUserWidget: UserWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty:CustomUserProperty<TUserId, TUser>
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
        where TContext: DbContext, ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser>
        {
            using (new FullSecurityAccessHelper(dbContext, true, false))
            {
                return !dbContext.Permissions.Any(n => n.PermissionName.ToLower()==permissionName.ToLower() && n.TenantId == null); }
        }

        public static bool EnsureNavUniqueness<TContext>(this TContext dbContext)
        {
            var dlg = typeof(ContextExtensions).GetMethod<Action<TContext>>(dbContext.GetType(), nameof(EnsureNavUniqueness));
            if (dlg != null)
            {
                dlg(dbContext);
                return true;
            }

            return false;
        }

        public static void EnsureNavUniqueness<TContext, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser>(this TContext dbContext)
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
        where TContext : DbContext, ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser>
        {
            using (new FullSecurityAccessHelper(dbContext, true, false))
            {
                var menus = dbContext.Navigation.Where(n => string.IsNullOrEmpty(n.RefTag)).ToArray();
                foreach (var menu in menus)
                {
                    menu.RefTag = Guid.NewGuid().ToString("D");
                }

                dbContext.SaveChanges();
            }
        }

        public static async Task CleanupSystemLog(this IBaseTenantContext context, DateTime minLogTime)
        {
            context.Database.ExecuteSqlInterpolated($"delete from Systemlog where EventTime < {minLogTime}");
        }
    }
}
