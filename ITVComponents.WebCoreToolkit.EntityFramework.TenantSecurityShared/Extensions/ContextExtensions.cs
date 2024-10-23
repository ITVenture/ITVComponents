using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DuckTyping.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.GenericsHelperInterfaces;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.GenericsHelperInterfaces.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using SkiaSharp;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class ContextExtensions
    {
        private static ConcurrentDictionary<Type, IContextExtensions> implementations =
            new ConcurrentDictionary<Type, IContextExtensions>();

        public static bool IsCyclicRoleInheritance<TContext>(this TContext dbContext, int parentRole, int newChildRole)
        {
            return GetWrapper(dbContext).IsCyclicRoleInheritance(dbContext, parentRole, newChildRole);
        }

        public static bool IsCyclicRoleInheritance<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
            TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter,
            TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate,
            TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter,
            TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet,
            TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin,
            TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>(
            this TContext dbContext, int parentRole, int newChildRole)
            where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser,
                TRoleRole>
            where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
                TTenantUser, TRoleRole>
            where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
                TTenantUser, TRoleRole>
            where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole>
            where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
                TTenantUser, TRoleRole>
            where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>
            where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>
            where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
                TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
            where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
            where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
            where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
                TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam,
                TWidgetLocalization>
            where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
                TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam,
                TWidgetLocalization>
            where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission,
                TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget,
                TWidgetParam, TWidgetLocalization>
            where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
                TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam,
                TWidgetLocalization>
            where TUserProperty : CustomUserProperty<TUserId, TUser>
            where TUser : class
            where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission
                , TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                TAssetTemplateFeature>
            where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                TAssetTemplateFeature>
            where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                TAssetTemplateFeature>
            where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                TAssetTemplateFeature>
            where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
                TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature,
                TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
            where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
                TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
            where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission,
                TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath,
                TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter,
                TSharedAssetTenantFilter>
            where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission
                , TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet>
            where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet>
            where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission
                , TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet,
                TClientAppTemplate, TClientAppTemplatePermission>
            where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppTemplate,
                TClientAppTemplatePermission>
            where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission,
                TClientApp, TClientAppUser>
            where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
                TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp,
                TClientAppUser>
            where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission
                , TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp,
                TClientAppUser>
            where TTenant : Tenant
            where TContext : DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole,
                TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter,
                TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate,
                TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter,
                TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet,
                TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin,
                TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>
            where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TWebPluginConstant : WebPluginConstant<TTenant>
            where TWebPluginGenericParameter :
            WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
            where TSequence : Sequence<TTenant>
            where TTenantSetting : TenantSetting<TTenant>
            where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
            where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
                TTenantUser, TRoleRole>
        {
            if (parentRole == newChildRole)
            {
                return true;
            }

            var parentEntity = dbContext.SecurityRoles.Include(n => n.PermissiveRoles)
                .ThenInclude(n => n.PermissiveRole).Where(n => n.RoleId == parentRole)
                .SelectMany(n => n.PermissiveRoles.Select(p => p.PermissiveRole)).ToArray();
            var ids = parentEntity.Select(n => n.RoleId).ToArray();
            var potentialParents = new List<int>(ids);
            while (ids.Length > 0)
            {
                parentEntity = dbContext.SecurityRoles.Include(n => n.PermissiveRoles)
                    .ThenInclude(n => n.PermissiveRole).Where(n => ids.Contains(n.RoleId))
                    .SelectMany(n => n.PermissiveRoles.Select(p => p.PermissiveRole)).ToArray();
                ids = parentEntity.Select(n => n.RoleId).ToArray();
                potentialParents.AddRange(ids);
            }

            return potentialParents.Contains(newChildRole);
        }

        public static bool VerifyRoleName<TContext>(this TContext dbContext, string permissionName)
        {
            return GetWrapper(dbContext).VerifyRoleName(dbContext, permissionName);
            /*var dlg = typeof(ContextExtensions).GetMethod<Func<TContext, string, bool>>(dbContext.GetType(), nameof(VerifyRoleName));
            if (dlg != null)
            {
                return dlg(dbContext, permissionName);
            }

            return false;*/
        }

        public static bool VerifyRoleName<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>(this TContext dbContext, string permissionName)
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserWidget: UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty:CustomUserProperty<TUserId, TUser>
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
        where TTenant: Tenant
        where TContext: DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        {
            using (new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(dbContext, new(){ShowAllTenants = true, HideGlobals=false}))
            {
                return !dbContext.Permissions.Any(n => n.PermissionName.ToLower()==permissionName.ToLower() && n.TenantId == null); }
        }

        public static bool EnsureNavUniqueness<TContext>(this TContext dbContext)
        {
            return GetWrapper(dbContext).EnsureNavUniqueness(dbContext);
            /*var dlg = typeof(ContextExtensions).GetMethod<Action<TContext>>(dbContext.GetType(), nameof(EnsureNavUniqueness));
            if (dlg != null)
            {
                dlg(dbContext);
                return true;
            }

            return false;*/
        }

        public static void EnsureNavUniqueness<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>(this TContext dbContext)
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
            where TTenant: Tenant
        where TWebPlugin: WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant: WebPluginConstant<TTenant>
        where TWebPluginGenericParameter: WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence: Sequence<TTenant>
        where TTenantSetting: TenantSetting<TTenant>
        where TTenantFeatureActivation: TenantFeatureActivation<TTenant>
        where TContext : DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        {
            using (new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(dbContext, new() { ShowAllTenants = true, HideGlobals = false }))
            {
                var menus = dbContext.Navigation.Where(n => string.IsNullOrEmpty(n.RefTag)).ToArray();
                foreach (var menu in menus)
                {
                    menu.RefTag = Guid.NewGuid().ToString("D");
                }

                dbContext.SaveChanges();
            }
        }

        public static async Task CleanupSystemLog(this ICoreSystemContext context, DateTime minLogTime)
        {
            context.Database.ExecuteSqlInterpolated($"delete from Systemlog where EventTime < {minLogTime}");
        }

        private static IContextExtensions<TContext> GetWrapper<TContext>(TContext wrapper)
        {
            var act = wrapper.GetType();
            var dc = act.ImplementGenericMethods(typeof(ContextExtensions), methodFlags: BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.NonPublic, fixTypeEntries: [(name: "TContext", type: act)]);
            var fx = dc.First(n => n.GetParameters().Length == 0 && n.Name == nameof(GetWrapper));
            var tmpWrapper = fx.Invoke(null, null);
            if (tmpWrapper is not IContextExtensions<TContext> owrap)
            {
                var tip = typeof(ContextExtensionDecorator<,>).MakeGenericType(typeof(TContext), act);
                var impl = tip.GetConstructors().First(n => n.GetParameters().Length == 1).Invoke(new[] { tmpWrapper });
                return (IContextExtensions<TContext>)impl;
            }

            return owrap;
        }

        private static IContextExtensions<TContext> GetWrapper<TContext>()
        {
            return (IContextExtensions<TContext>)implementations.GetOrAdd(typeof(TContext),
                t => typeof(ContextExtensions).WrapType<IContextExtensions<TContext>>(typeof(TContext), minGenericParameterCount: 2, fixParameters:[ (name:"TContext",type:typeof(TContext))]));
        }
    }
}
