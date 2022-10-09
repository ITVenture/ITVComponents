using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security.ApplicationToken;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Security.ApplicationToken
{
    public abstract class ApplicationTokenService<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TContext> : IApplicationTokenService
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
        where TWidget : DashboardWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TWidgetParam : DashboardParam<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TUserWidget : UserWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TAssetTemplate : AssetTemplate<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>, new()
        where TSharedAssetUserFilter : SharedAssetUserFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>, new()
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>, new()
        where TAppPermission : AppPermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TContext : DbContext, ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser>
    {
        private readonly TContext dbContext;

        protected ApplicationTokenService(TContext dbContext)
        {
            this.dbContext = dbContext;
        }

        /// <summary>
        /// Gets the ApplicationUser label for the specified user
        /// </summary>
        /// <param name="principal">the user for which to get the app-user label.</param>
        /// <param name="applicationKey">the app-key of the requesting application</param>
        /// <returns>the app-user label for the given user</returns>
        public string GetApplicationUserLabel(IPrincipal principal, string applicationKey)
        {
            var userName = principal.Identity.Name;
            return null;
        }

        /// <summary>
        /// Checks for the given user and application key, if the provided refresh-token is valid
        /// </summary>
        /// <param name="principal">the user, that is logged in or queries a new access-token</param>
        /// <param name="applicationKey">the key of the application that is trying to log in as the given user</param>
        /// <param name="refreshToken">the provided refresh-token</param>
        /// <returns>a value indicating whether the given access-token is valid</returns>
        public virtual bool VerifyRefreshToken(IPrincipal principal, string applicationKey, string refreshToken)
        {
            var tmp = dbContext.ClientAppUsers.First(n => n.Label == applicationKey);
            return false;
        }

        /// <summary>
        /// Creates or updates the refresh-token for the given user-application combination
        /// </summary>
        /// <param name="principal">the user for which to update the refresh-token</param>
        /// <param name="applicationKey">the application for which to set the new refresh-token</param>
        /// <param name="refreshToken">the new refresh-token</param>
        /// <param name="oldToken">the old refresh-token</param>
        public virtual void UpdateRefreshToken(IPrincipal principal, string applicationKey, string refreshToken, string oldToken)
        {
            
        }

        /// <summary>
        /// Revokes the given access-token for the given user and application
        /// </summary>
        /// <param name="principal">the user for which to revoke the given refresh-token</param>
        /// <param name="applicationKey">the application-key for which to revoke the token</param>
        /// <param name="refreshToken">the refresh-token that is currently set for that application-user combination</param>
        public virtual void RevokeRefreshToken(IPrincipal principal, string applicationKey, string refreshToken)
        {
            
        }
    }
}
