using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text.RegularExpressions;
using Castle.Core.Logging;
using ITVComponents.Formatting;
using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Security;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Helpers;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Logging;
using CustomUserProperty = ITVComponents.WebCoreToolkit.Models.CustomUserProperty;
using Feature = ITVComponents.WebCoreToolkit.Models.Feature;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Permission = ITVComponents.WebCoreToolkit.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.Models.Role;
using User = ITVComponents.WebCoreToolkit.Models.User;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security
{
    public abstract class DbSecurityRepository<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : ISecurityRepository
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser: TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty : CustomUserProperty<TUserId, TUser>, new()
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
        where TUser : class
        where TTenant : Tenant
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>, new()
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>, new()
    {
        private readonly ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> securityContext;
        private readonly ILogger logger;

        // Per-instance memoization for IsAuthenticated. The repository is scoped together with the
        // DbContext (per circuit / per request). Without this, parallel async lifecycle callbacks
        // can each trigger an EF query against the same scoped DbContext concurrently → "A second
        // operation was started on this context" crash. See [[feedback-dbcontext-reentry]].
        private readonly ConcurrentDictionary<string, bool> isAuthenticatedCache = new();
        private readonly ITVComponents.WebCoreToolkit.Caching.IEntityChangeSignal changeSignal;
        private DateTime authCacheStampUtc = DateTime.UtcNow;

        protected DbSecurityRepository(ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> securityContext,
            ILogger logger, ITVComponents.WebCoreToolkit.Caching.IEntityChangeSignal changeSignal = null)
        {
            this.securityContext = securityContext;
            this.logger = logger;
            this.changeSignal = changeSignal;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets a list of users in the current application
        /// </summary>
        public virtual ICollection<User> Users
        {
            get
            {
                using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext,
                    ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
                return (from u in securityContext.Users.ToList()
                    select SelectUser(u)).ToList();
            }
        }

        /// <summary>
        /// Gets a list of Roles that can be granted to users in the current application
        /// </summary>
        public virtual ICollection<Role> Roles
        {
            get
            {
                using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
                return (from r in securityContext.SecurityRoles select r).ToList<Role>();
            }
        }

        /// <summary>
        /// Gets a collection of defined Permissions in the current application
        /// </summary>
        public virtual ICollection<Permission> Permissions
        {
            get
            {
                using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
                return (from p in securityContext.Permissions select p).ToList<Permission>();
            }
        }

        /// <summary>
        /// Gets an enumeration of Roles that are assigned to the given user
        /// </summary>
        /// <param name="user">the user for which to get the roles</param>
        /// <returns>an enumerable of all the user-roles</returns>
        public virtual IEnumerable<Role> GetRoles(User user)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            return (from r in AllRoles(securityContext.Users.First(UserFilter(user))) select r.Role).ToArray();
        }

        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> requiredPermissions,
            string permissionScope)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false }));

            return (from a in (from t in securityContext.SecurityRoles.Where(r =>
                                r.Tenant.TenantName == permissionScope)
                            select new
                            {
                                PermissionMap = t.RolePermissions.Select(n =>
                                    new { t.RoleName, Permission = n.Permission.PermissionName })
                            })
                        .SelectMany(i => i.PermissionMap).AsEnumerable()
                    join p in requiredPermissions on a.Permission equals p
                    select a.RoleName).Distinct().Select(n => new Role{RoleName = n}).ToArray();
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for the given user
        /// </summary>
        /// <param name="user">the user for which to get the custom properties</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public virtual IEnumerable<CustomUserProperty> GetCustomProperties(User user, CustomUserPropertyType propertyType)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            return (from p in UserProps(securityContext.Users.First(UserFilter(user))) where p.PropertyType == propertyType select p).ToArray();

        }

        /// <summary>
        /// Gets the string representation of the given property. This is only supported in 1:1 user environments
        /// </summary>
        /// <param name="user">the user for which go get the property</param>
        /// <param name="propertyName">the name of the desired property</param>
        /// <param name="propertyType">the expected property-type</param>
        /// <returns>the string representation of the requested property</returns>
        public string GetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType)
        {
            string retVal = null;
            var tmp = GetCustomProperties(user, propertyType).FirstOrDefault(n => n.PropertyName == propertyName);
            if (tmp != null)
            {
                retVal = tmp.Value;
            }

            return retVal;
        }

        /// <summary>
        /// Gets the string representation of the given property. This is only supported in 1:1 user environments
        /// </summary>
        /// <param name="user">the user for which go get the property</param>
        /// <param name="propertyName">the name of the desired property</param>
        /// <param name="propertyType">the expected property-type</param>
        /// <returns>the string representation of the requested property</returns>
        public T GetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType)
        {
            T retVal = default(T);
            var tmpVal = GetCustomProperty(user, propertyName, propertyType);
            if (!string.IsNullOrEmpty(tmpVal))
            {
                if (propertyType == CustomUserPropertyType.Claim || propertyType == CustomUserPropertyType.Literal)
                {
                    if (TypeConverter.TryConvert(tmpVal, typeof(T), out var result))
                    {
                        retVal = (T)result;
                    }
                }
                else
                {
                    retVal = JsonHelper.FromJsonString<T>(tmpVal, SerializationTypingMode.StaticTyping);
                }
            }

            return retVal;
        }

        public bool SetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType, string value)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            var dbuser = securityContext.Users.First(UserFilter(user));
            var prop = securityContext.UserProperties.FirstOrDefault(n =>
                n.PropertyName == propertyName && n.User == dbuser && n.PropertyType == propertyType);
            if (prop == null && !string.IsNullOrEmpty(value))
            {
                prop = new TUserProperty
                {
                    PropertyType = propertyType,
                    PropertyName = propertyName,
                    User = dbuser
                };

                securityContext.UserProperties.Add(prop);
            }
            else if (prop != null && string.IsNullOrEmpty(value))
            {
                securityContext.UserProperties.Remove(prop);
                prop = null;
            }

            if (prop != null)
            {
                prop.Value = value;
            }

            securityContext.SaveChanges();
            return true;
        }

        public bool SetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType, T value)
        {
            string stringVal = null;
            if (propertyType == CustomUserPropertyType.Claim || propertyType == CustomUserPropertyType.Literal)
            {
                stringVal = value?.ToString();
            }
            else if (value != null)
            {
                stringVal = JsonHelper.ToJson(value, SerializationTypingMode.StaticTyping, null);
            }

            return SetCustomProperty(user, propertyName, propertyType, stringVal);
        }

        public virtual bool IsAuthenticated(string[] userLabels, string userAuthenticationType)
        {
            InvalidateAuthCacheIfStale();
            var t = securityContext.CurrentTenantId;
            var cacheKey = BuildAuthCacheKey(userLabels, userAuthenticationType, t, scope: null);
            return isAuthenticatedCache.GetOrAdd(cacheKey, _ => IsAuthenticatedCore(userLabels, userAuthenticationType, t));
        }

        /// <summary>
        /// Clears the per-instance IsAuthenticated memoization when a security-relevant entity changed since
        /// the cache was last filled, so revoked/granted access takes effect within a live circuit. No-op when
        /// no change-signal is registered (EntityWriteTracker inactive).
        /// </summary>
        private void InvalidateAuthCacheIfStale()
        {
            if (changeSignal != null &&
                changeSignal.GetLastChange(ITVComponents.WebCoreToolkit.Caching.EntityChangeTopics.Security) > authCacheStampUtc)
            {
                isAuthenticatedCache.Clear();
                authCacheStampUtc = DateTime.UtcNow;
            }
        }

        private bool IsAuthenticatedCore(string[] userLabels, string userAuthenticationType, int? t)
        {
            if (t != null)
            {
                var ti = t.Value;
                IQueryable<TUser> tenantUsers;
                if (userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern)))
                {
                    tenantUsers = securityContext.TenantUsers.Where(tu => tu.TenantId == ti).Select(u => u.User);
                }
                else
                {
                    var filteredLabels = (from ul in userLabels
                        where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                        select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                    var appUsers = securityContext.ClientAppUsers.Where(n => n.TenantUser.TenantId == ti);
                    tenantUsers = appUsers
                        .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                        .Select(n => n.TenantUser.User);
                }

                return tenantUsers.Any(UserFilter(userLabels, userAuthenticationType));
            }

            return false;
        }

        public bool IsAuthenticated(string[] userLabels, string forScope, string userAuthenticationType)
        {
            InvalidateAuthCacheIfStale();
            var cacheKey = BuildAuthCacheKey(userLabels, userAuthenticationType, tenantId: null, scope: forScope);
            return isAuthenticatedCache.GetOrAdd(cacheKey, _ => IsAuthenticatedScopedCore(userLabels, forScope, userAuthenticationType));
        }

        private bool IsAuthenticatedScopedCore(string[] userLabels, string forScope, string userAuthenticationType)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false }));
            var t = securityContext.Tenants.FirstOrDefault(n => n.TenantName == forScope)?.TenantId;
            if (t != null)
            {
                var ti = t.Value;
                IQueryable<TUser> tenantUsers;
                if (userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern)))
                {
                    tenantUsers = securityContext.TenantUsers.Where(tu => tu.TenantId == ti).Select(u => u.User);
                }
                else
                {
                    var filteredLabels = (from ul in userLabels
                        where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                        select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                    var appUsers = securityContext.ClientAppUsers.Where(n => n.TenantUser.TenantId == ti);
                    tenantUsers = appUsers
                        .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                        .Select(n => n.TenantUser.User);
                }

                return tenantUsers.Any(UserFilter(userLabels, userAuthenticationType));
            }

            return false;
        }

        private static string BuildAuthCacheKey(string[] userLabels, string userAuthenticationType, int? tenantId, string scope)
        {
            var labels = userLabels is null || userLabels.Length == 0 ? "-" : string.Join("", userLabels);
            return $"{labels}{userAuthenticationType ?? "-"}{tenantId?.ToString() ?? "-"}{scope ?? "-"}";
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public virtual IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType, CustomUserPropertyType propertyType)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            IQueryable<TUser> tenantUsers;
            if (userLabels.All(n => string.IsNullOrEmpty(n) || !Regex.IsMatch(n, Global.AppUserKeyPattern)))
            {
                tenantUsers = securityContext.Users;  //securityContext.TenantUsers.Select(u => u.User);
            }
            else
            {
                var filteredLabels = (from ul in userLabels
                    where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                    select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                var appUsers = securityContext.ClientAppUsers;
                tenantUsers = appUsers
                    .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                    .Select(n => n.TenantUser.User);
            }
            return (from u in tenantUsers.Where(UserFilter(userLabels,userAuthenticationType))
                    .Join(securityContext.UserProperties, UserId, p => p.UserId, (tu,tp) => tp)
                    where u.PropertyType == propertyType
                select u).ToArray();
        }

        public virtual IEnumerable<T> GetUserIds<T>(string[] userLabels, string userAuthenticationType)
        {
            if (typeof(T) != typeof(TUserId))
            {
                throw new InvalidOperationException($"Expected Type was: {typeof(T)}");
            }

            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            IQueryable<TUser> tenantUsers;
            if (userLabels.All(n => string.IsNullOrEmpty(n) || !Regex.IsMatch(n, Global.AppUserKeyPattern)))
            {
                tenantUsers = securityContext.TenantUsers.Select(u => u.User);
            }
            else
            {
                var filteredLabels = (from ul in userLabels
                    where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                    select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                var appUsers = securityContext.ClientAppUsers;
                tenantUsers = appUsers
                    .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                    .Select(n => n.TenantUser.User);
            }

            return tenantUsers.Where(UserFilter(userLabels, userAuthenticationType)).Select(UserId).Cast<T>();
        }

        public virtual T GetUserId<T>(string[] userLabels, string userAuthenticationType)
        {
            var tmp = GetUserIds<T>(userLabels, userAuthenticationType).ToArray();
            if (tmp.Length != 1)
            {
                throw new InvalidOperationException("Use GetUserIds in Environment with User-Mappings!");
            }

            return tmp[0];
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="originalClaims">the claims that were originally attached to the current identity</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public virtual IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims,
            string userAuthenticationType)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            var typeClaims = securityContext.AuthenticationClaimMappings.Where(n =>
                n.AuthenticationType.AuthenticationTypeName == userAuthenticationType).ToArray();
            var claimMapRaw = new Dictionary<string, ClaimData[]>(from t in originalClaims group t by t.Type into g select new KeyValuePair<string, ClaimData[]>(g.Key,g.ToArray()));
            var claimMap = new ClaimMap(claimMapRaw);
            var preMapped = from t in originalClaims
                join i in typeClaims on t.Type equals i.IncomingClaimName
                select new
                {
                    Original = new ClaimMapRoot
                    {
                        Value = t.Value,
                        Issuer = t.Issuer,
                        OriginalIssuer = t.OriginalIssuer,
                        Type = t.Type,
                        ValueType = t.ValueType,
                        ClaimMap = claimMap
                    },
                    Map = i
                };
            return (from t in preMapped where string.IsNullOrEmpty(t.Map.Condition) || ExpressionParser.Parse(t.Map.Condition, t.Original) is bool and true
                   select TryGetClaim(t.Map,t.Original)).Where(n => n is { Length: > 0 }).SelectMany(n => n).Where(n => n != null);
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given user through its roles
        /// </summary>
        /// <param name="user">the user for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given user</returns>
        public virtual IEnumerable<Permission> GetPermissions(User user)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext,
                ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            var tmpRr = (from r in AllRoles(securityContext.Users.First(UserFilter(user)))
                select new
                {
                    rp = r.Role.RolePermissions,
                    grp = r.Role.PermittedGlobalRoles.SelectMany(gpr => gpr.GlobalRole.RolePermissions)
                }).ToArray();
            var preRet = tmpRr
                .SelectMany(rp => rp.rp).Select(p => new Permission
                {
                    //PermissionName = $"{(!p.Permission.IsGlobal?p.Tenant.TenantName:"")}{p.Permission.PermissionName}"
                    PermissionName = p.Permission.PermissionName
                }).Union(tmpRr.SelectMany(grp => grp.grp).Select(p => new Permission
                {
                    //PermissionName = $"{(!p.Permission.IsGlobal?p.Tenant.TenantName:"")}{p.Permission.PermissionName}"
                    PermissionName = p.Permission.PermissionName
                })).Distinct();

            var rett = preRet.ToArray();
            return rett;
        }

        /// <summary>
        /// Gets an enumeration of Permissions for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of permissions for the given user-labels</returns>
        public virtual IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            IQueryable<TUser> tenantUsers;
            string[] preFilteredPerms = null;
            if (userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern)))
            {
                tenantUsers = securityContext.TenantUsers.Where(tu => tu.TenantId == securityContext.CurrentTenantId.Value).Select(u => u.User);
            }
            else
            {
                var filteredLabels = (from ul in userLabels
                    where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                    select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                var appUsers = securityContext.ClientAppUsers.Where(n => n.TenantUser.TenantId == securityContext.CurrentTenantId.Value);
                preFilteredPerms = appUsers.SelectMany(n => n.ClientApp.AppPermissions).SelectMany(n => n.PermissionSet.Permissions)
                    .Select(n => n.Permission.PermissionName).Distinct().ToArray();
                tenantUsers = appUsers
                    .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                    .Select(n => n.TenantUser.User);
            }

            var permRaw = (from tr in tenantUsers.Where(UserFilter(userLabels, userAuthenticationType))
                    .Join(securityContext.TenantUsers, UserId, tr => tr.UserId, (tu, tt) => tt)
                join ur in securityContext.TenantUserRoles /*.Where(n => n.TenantUserId != null && n.RoleId != null)*/
                    on tr.TenantUserId equals ur.TenantUserId.Value
                join r in securityContext.SecurityRoles.Include(n => n.RolePermissions).ThenInclude(rp => rp.Permission)
                    .Include(n => n.PermittedGlobalRoles).ThenInclude(grp => grp.GlobalRole)
                    .ThenInclude(gr => gr.RolePermissions)
                    .ThenInclude(grp => grp.Permission) on new { RoleId = ur.RoleId.Value, tr.TenantId } equals new
                    { r.RoleId, r.TenantId }
                //join rp in securityContext.RolePermissions/*.Where(n => n.RoleId != null)*/ on new {r.RoleId, r.TenantId} equals new {RoleId=rp.RoleId, rp.TenantId}
                //join rt in securityContext.Tenants on rp.TenantId equals rt.TenantId
                /*join p in securityContext.Permissions on rp.PermissionId equals p.PermissionId
                select new Permission
                {
                    //PermissionName = p.PermissionName != rt.TenantName?$"{(!p.IsGlobal?rt.TenantName:"")}{p.PermissionName}":p.PermissionName
                    PermissionName = p.PermissionName
                }*/
                select r);
            var qLoc = permRaw.SelectMany(n => n.RolePermissions.Select(rp => new Permission
            {
                PermissionName = rp.Permission.PermissionName
            }));
            var qGlob = permRaw.SelectMany(n => n.PermittedGlobalRoles.SelectMany(gr => gr.GlobalRole.RolePermissions))
                .Select(gp => new Permission
                {
                    PermissionName = gp.Permission.PermissionName
                });
            var permRaw2 = qLoc.Union(qGlob).Distinct();
            var permRawArr = permRaw2.ToArray();
            if (preFilteredPerms != null)
            {
                permRawArr = (from t in permRawArr join p in preFilteredPerms on t.PermissionName equals p select t)
                    .ToArray();
            }

            return permRawArr;
        }

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string forScope, string userAuthenticationType)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false }));
            IQueryable<TUser> tenantUsers;
            string[] preFilteredPerms = null;
            if (userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern)))
            {
                tenantUsers = securityContext.TenantUsers.Where(tu => tu.Tenant.TenantName == forScope).Select(u => u.User);
            }
            else
            {
                var filteredLabels = (from ul in userLabels
                                      where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                                      select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                var appUsers = securityContext.ClientAppUsers.Where(n => n.TenantUser.Tenant.TenantName == forScope);
                preFilteredPerms = appUsers.SelectMany(n => n.ClientApp.AppPermissions).SelectMany(n => n.PermissionSet.Permissions)
                    .Select(n => n.Permission.PermissionName).Distinct().ToArray();
                tenantUsers = appUsers
                    .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                    .Select(n => n.TenantUser.User);
            }

            var permRaw = (from tr in tenantUsers.Where(UserFilter(userLabels, userAuthenticationType))
                    .Join(securityContext.TenantUsers, UserId, tr => tr.UserId, (tu, tt) => tt)
                join ur in securityContext.TenantUserRoles /*.Where(n => n.TenantUserId != null && n.RoleId != null)*/
                    on tr.TenantUserId equals ur.TenantUserId.Value
                join r in securityContext.SecurityRoles.Include(n => n.RolePermissions).ThenInclude(rp => rp.Permission)
                        .Include(n => n.PermittedGlobalRoles).ThenInclude(pgr => pgr.GlobalRole)
                        .ThenInclude(gr => gr.RolePermissions).ThenInclude(grp => grp.Permission)
                    on new { RoleId = ur.RoleId.Value, tr.TenantId } equals new { r.RoleId, r.TenantId }
                select r
                //join rp in securityContext.RolePermissions/*.Where(n => n.RoleId != null)*/ on new { r.RoleId, r.TenantId } equals new { RoleId = rp.RoleId, rp.TenantId }
                //join rt in securityContext.Tenants on rp.TenantId equals rt.TenantId
                //join p in securityContext.Permissions on rp.PermissionId equals p.PermissionId
                /*where tr.Tenant.TenantName == forScope
                select new Permission
                {
                    //PermissionName = p.PermissionName != rt.TenantName?$"{(!p.IsGlobal?rt.TenantName:"")}{p.PermissionName}":p.PermissionName
                    PermissionName = p.PermissionName
                }*/);//.Distinct().ToArray();
            var qLoc = permRaw.SelectMany(n => n.RolePermissions.Select(rp => new Permission
            {
                PermissionName = rp.Permission.PermissionName
            }));
            var qGlob = permRaw.SelectMany(n => n.PermittedGlobalRoles.SelectMany(gr => gr.GlobalRole.RolePermissions))
                .Select(gp => new Permission
                {
                    PermissionName = gp.Permission.PermissionName
                });
            var permRaw2 = qLoc.Union(qGlob).Distinct();
            var permRawArr = permRaw2.ToArray();
            if (preFilteredPerms != null)
            {
                permRawArr = (from t in permRawArr join p in preFilteredPerms on t.PermissionName equals p select t)
                    .ToArray();
            }

            return permRawArr;
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given Role
        /// </summary>
        /// <param name="role">the role for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given role</returns>
        public virtual IEnumerable<Permission> GetPermissions(Role role)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            if (role is TRole dbRole)
            {
                return (from p in dbRole.RolePermissions select p.Permission).Union(from p in dbRole.PermittedGlobalRoles.SelectMany(gr => gr.GlobalRole.RolePermissions) select p.Permission).Distinct();
            }



            return (from p in securityContext.SecurityRoles.Include(n => n.RolePermissions).ThenInclude(rp=>rp.Permission)
                .Include(n => n.PermittedGlobalRoles).ThenInclude(pgr => pgr.GlobalRole).ThenInclude(gr => gr.RolePermissions).ThenInclude(grp => grp.Permission).First(r => r.RoleName == role.RoleName).RolePermissions select new Permission
            {
                //PermissionName = $"{(!p.Permission.IsGlobal ? p.Tenant.TenantName : "")}{p.Permission.PermissionName}"
                PermissionName = p.Permission.PermissionName
            }).Distinct().ToArray();
        }

        /// <summary>
        /// Gets a value indicating whether the specified Permission-Scope exists
        /// </summary>
        /// <param name="permissionScopeName">the permissionScope to check for existence</param>
        /// <returns>a value indicating whether the specified permissionScope is valid</returns>
        public virtual bool PermissionScopeExists(string permissionScopeName)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            return securityContext.Tenants.Any(n => n.TenantName == permissionScopeName);
        }

        public virtual IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string authType)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false }));

            if (userLabels.Any(n => Regex.IsMatch(n, Global.AppUserKeyPattern)))
            {
                IQueryable<TUser> tenantUsers;
                var filteredLabels = (from ul in userLabels
                    where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                    select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                var appUsers = securityContext.ClientAppUsers;
                return (from d in appUsers
                    orderby d.TenantUser.Tenant.DisplayName
                    select new ScopeInfo { ScopeDisplayName = d.TenantUser.Tenant.DisplayName, ScopeName = d.TenantUser.Tenant.TenantName })
                    .ToArray();
            }

            return (from d in (from t in securityContext.Users.Where(UserFilter(userLabels, authType))
                        .Join(securityContext.TenantUsers, UserId, u => u.UserId, (tu, tt) => tt.Tenant)
                    select t).Distinct()
                orderby d.DisplayName
                select new ScopeInfo { ScopeDisplayName = d.DisplayName, ScopeName = d.TenantName }).ToArray();
        }

        /// <summary>
        /// Creates a TimeZone helper object that can be used to perform calculations between localtime and utc-time for the given tenant
        /// </summary>
        /// <param name="permissionScopeName">the target permission scope</param>
        /// <returns>a helper object that performs datetime calculations</returns>
        public TimeZoneHelper GetTimeZoneHelper(string permissionScopeName)
        {
            var timezone = GetTimeZone(permissionScopeName);
            return new TimeZoneHelper(timezone);
        }

        /// <summary>
        /// Gets a list of activated features for a specific permission-Scope
        /// </summary>
        /// <param name="permissionScopeName">the name of the current permission-prefix selected by the current user</param>
        /// <returns>returns a list of activated features</returns>
        public virtual IEnumerable<Feature> GetFeatures(string permissionScopeName)
        {
            IDisposable tmp = null;
            try
            {
                bool useCurrentTenant = string.IsNullOrEmpty(permissionScopeName) && securityContext.CurrentTenantId != null;
                int? tenantToUse = null;
                if (!useCurrentTenant)
                {
                    tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = true}));
                }
                else
                {
                    tenantToUse = securityContext.CurrentTenantId;
                }

                    var dt = DateTime.UtcNow;//DateTime.SpecifyKind(DateTime.UtcNow,DateTimeKind.Local);
                var raw = (from t in securityContext.Features
                    join a in securityContext.TenantFeatureActivations.Where(ta =>
                                ((!useCurrentTenant && ta.Tenant.TenantName == permissionScopeName) || (useCurrentTenant && ta.TenantId == tenantToUse))
                                && (ta.ActivationStart== null || ta.ActivationStart <= dt)
                                && (ta.ActivationEnd == null || ta.ActivationEnd >= dt))
                            .GroupBy(g => new {g.FeatureId, g.Tenant.TenantName})
                            .Select(n => new {n.Key.FeatureId, n.Key.TenantName})
                        on t.FeatureId equals a.FeatureId into lfaj
                    from hoj in lfaj.DefaultIfEmpty()
                    select new {T = t, A = hoj.TenantName}).ToArray();

                /*EntityQueryable<Feature> mmp = (EntityQueryable<Feature>)raw;
                logger.LogDebug(mmp.DebugView.Query);*/
                return raw.Select(n => new Feature
                {
                    FeatureName = n.T.FeatureName,
                    FeatureDescription = n.T.FeatureDescription,
                    Enabled = n.T.Enabled || !string.IsNullOrEmpty(n.A)
                }).ToArray();
            }
            finally
            {
                tmp?.Dispose();
            }
        }

        public virtual string Decrypt(string encryptedValue, string permissionScopeName)
        {
            return securityContext.DecryptForScope(encryptedValue, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public virtual byte[] Decrypt(byte[] encryptedValue, string permissionScopeName)
        {
            return securityContext.DecryptForScope(encryptedValue, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public virtual byte[] Decrypt(byte[] encryptedValue, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            return securityContext.DecryptForScope(encryptedValue, permissionScopeName, initializationVector, salt, n => ConfigureTrustConfig(n));
        }

        public virtual Stream GetDecryptStream(Stream baseStream, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            return securityContext.GetDecryptStreamForScope(baseStream, permissionScopeName, initializationVector,
                salt, n => ConfigureTrustConfig(n));
        }

        public virtual Stream GetDecryptStream(Stream baseStream, string permissionScopeName)
        {
            return securityContext.GetDecryptStreamForScope(baseStream, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public virtual string Encrypt(string value, string permissionScopeName)
        {
            return securityContext.EncryptForScope(value, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public virtual byte[] Encrypt(byte[] value, string permissionScopeName)
        {
            return securityContext.EncryptForScope(value, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public virtual byte[] Encrypt(byte[] value, string permissionScopeName, out byte[] initializationVector, out byte[] salt)
        {
            return securityContext.EncryptForScope(value, permissionScopeName, out initializationVector, out salt, n => ConfigureTrustConfig(n));
        }

        public virtual Stream GetEncryptStream(Stream baseStream, string permissionScopeName, out byte[] initializationVector,
            out byte[] salt)
        {
            return securityContext.GetEncryptStreamForScope(baseStream, permissionScopeName, out initializationVector,
                out salt, n => ConfigureTrustConfig(n));
        }

        public virtual Stream GetEncryptStream(Stream baseStream, string permissionScopeName)
        {
            return securityContext.GetEncryptStreamForScope(baseStream, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public string EncryptJsonObject(object value, string permissionScopeName)
        {
            return securityContext.EncryptJsonObjectForScope(value, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public Permission[] GetKnownPermissions(string permissionScope)
        {
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false }));
            return (from p in securityContext.Permissions where p.TenantId == null || p.Tenant.TenantName == permissionScope select new Permission{PermissionName = p.PermissionName}).ToArray();
        }

        public ExternalServiceConnection GetExternalService(string name, bool decryptSecret = false)
        {
            var svc= GetExternalServiceInternal(securityContext, name);
            if (svc!= null)
            {
                var retVal = ToExternalDefinition(svc, decryptSecret);
                /*if (decryptSecret && !string.IsNullOrEmpty(retVal.ClientSecret))
                {
                    if (retVal.Global)
                    {
                        retVal.ClientSecret = retVal.ClientSecret.Decrypt();
                    }
                    else
                    {
                        retVal.ClientSecret = Decrypt(retVal.ClientSecret, securityContext.CurrentTenantName);
                    }
                }*/

                return retVal;
            }

            return null;
        }

        public void PrepareExternalServiceConnect(OAuthState oAuthState)
        {
            var state = new TExternalOAuthServiceState
            {
                TenantId = securityContext.CurrentTenantId.Value,
                ExpiresAt = DateTime.Now.AddMinutes(5).ToUniversalTime(),
                State = oAuthState.State,
                CodeVerifier = oAuthState.CodeVerifier,
                Used = false
            };

            var connection = GetExternalServiceInternal(securityContext, oAuthState.ConnectionName);
            if (connection != null)
            {
                state.OAuthServiceId = connection.OAuthServiceId;
                securityContext.ExternalOAuthServiceStates.Add(state);
                securityContext.SaveChanges();
            }
        }

        public OAuthState GetOAuthRequest(string connectionName, string state)
        {
            var now = DateTime.UtcNow;
            bool switchRequired = false;

            LogEnvironment.LogDebugEvent("Looking up all services", LogSeverity.Warning);
            using var tmp = new FullSecurityAccessHelper<TTrustConfig>(securityContext,
                ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false }));
            var bufferedState = securityContext.ExternalOAuthServiceStates.Include(n => n.Connection)
                .Include(n => n.Tenant)
                .FirstOrDefault(n =>
                    n.Connection.CalculatedUniqueServiceName == connectionName && n.State == state && !n.Used &&
                    n.ExpiresAt > now);

            if (bufferedState != null)
            {
                switchRequired = bufferedState.TenantId != securityContext.CurrentTenantId;
                bufferedState.Used = true;
                securityContext.SaveChanges();

                string explicitTenant = null;
                if (switchRequired)
                {
                    explicitTenant = bufferedState.Tenant.TenantName;
                }

                return new OAuthState
                {
                    ConnectionName = bufferedState.Connection.CalculatedUniqueServiceName,
                    ExpiresAt = bufferedState.ExpiresAt,
                    State = bufferedState.State,
                    CodeVerifier = bufferedState.CodeVerifier,
                    ScopeSwitchRequired = switchRequired,
                    ExplicitScope = explicitTenant
                };
            }

            return null;
        }

        public void StoreExternalServiceToken(string connectionName, TranslatedTokenResponse token)
        {
            var connection = GetExternalServiceInternal(securityContext, connectionName);
            var login = securityContext.ExternalOAuthServiceTenantLogins.FirstOrDefault(n =>
                n.OAuthServiceId == connection.OAuthServiceId && n.TenantId == securityContext.CurrentTenantId);
            var encToken = new TranslatedTokenResponse()
            {
                Scope = token.Scope, AccessToken = Encrypt(token.AccessToken, securityContext.CurrentTenantName), ExpiresAt = token.ExpiresAt,
                RefreshToken = Encrypt(token.RefreshToken, securityContext.CurrentTenantName),
                TokenType = token.TokenType
            };

            if (login == null)
            {
                login = new TExternalOAuthServiceTenantLogin
                {
                    TenantId = securityContext.CurrentTenantId.Value,
                    OAuthServiceId = connection.OAuthServiceId,
                    Token = JsonHelper.ToJson(encToken,SerializationTypingMode.StaticTyping)
                };
                securityContext.ExternalOAuthServiceTenantLogins.Add(login);
            }
            else
            {
                login.Token = JsonHelper.ToJson(encToken, SerializationTypingMode.StaticTyping);
                login.Revoked = false;
            }

            securityContext.SaveChanges();
        }

        public TranslatedTokenResponse GetBufferedToken(string connectionName, bool forRevoke, bool throwIfNull, out ExternalServiceConnection connectionInfo, out Action<TranslatedTokenResponse> updateToken)
        {
            var connection = GetExternalServiceInternal(securityContext, connectionName);
            if (connection != null)
            {
                connectionInfo = ToExternalDefinition(connection, true);
                var login = securityContext.ExternalOAuthServiceTenantLogins.FirstOrDefault(n =>
                    n.OAuthServiceId == connection.OAuthServiceId && n.TenantId == securityContext.CurrentTenantId);
                if (login is { Revoked: false })
                {
                    var tmp = JsonHelper.FromJsonString<TranslatedTokenResponse>(login.Token,
                        SerializationTypingMode.StaticTyping);
                    var token = new TranslatedTokenResponse
                    {
                        AccessToken = Decrypt(tmp.AccessToken, securityContext.CurrentTenantName),
                        ExpiresAt = tmp.ExpiresAt,
                        RefreshToken = Decrypt(tmp.RefreshToken, securityContext.CurrentTenantName),
                        Scope = tmp.Scope,
                        TokenType = tmp.TokenType
                    };

                    if (!forRevoke)
                    {
                        updateToken = newToken =>
                        {
                            var encToken = new TranslatedTokenResponse
                            {
                                Scope = newToken.Scope,
                                AccessToken = Encrypt(newToken.AccessToken, securityContext.CurrentTenantName),
                                ExpiresAt = newToken.ExpiresAt,
                                RefreshToken = Encrypt(newToken.RefreshToken, securityContext.CurrentTenantName),
                                TokenType = newToken.TokenType
                            };
                            login.Token = JsonHelper.ToJson(encToken, SerializationTypingMode.StaticTyping);
                            securityContext.SaveChanges();
                        };
                    }
                    else
                    {
                        updateToken = _ => { };
                        login.Revoked = true;
                        login.Token = "{}";
                        securityContext.SaveChanges();
                    }

                    return token;
                }

                if (!throwIfNull)
                {
                    updateToken = newToken =>
                    {
                        var encToken = new TranslatedTokenResponse
                        {
                            Scope = newToken.Scope,
                            AccessToken = Encrypt(newToken.AccessToken, securityContext.CurrentTenantName),
                            ExpiresAt = newToken.ExpiresAt,
                            RefreshToken = Encrypt(newToken.RefreshToken, securityContext.CurrentTenantName),
                            TokenType = newToken.TokenType
                        };

                        if (login != null)
                        {
                            login.Revoked = false;
                            login.Token = JsonHelper.ToJson(encToken, SerializationTypingMode.StaticTyping);
                        }
                        else
                        {
                            login = new TExternalOAuthServiceTenantLogin
                            {
                                TenantId = securityContext.CurrentTenantId.Value,
                                Revoked = false,
                                OAuthServiceId = connection.OAuthServiceId,
                                Token = JsonHelper.ToJson(encToken, SerializationTypingMode.StaticTyping)
                            };

                            securityContext.ExternalOAuthServiceTenantLogins.Add(login);

                        }

                        securityContext.SaveChanges();
                    };

                    return null;
                }
            }

            throw new InvalidOperationException($"No appropriate Token found for {connectionName}");
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        protected TTrustConfig ConfigureTrustConfig(TTrustConfig trustConfig,
            [CallerMemberName] string callingMethod = null)
        {
            return ConfigureTrustConfigImpl(trustConfig, callingMethod);
        }

        protected abstract User SelectUser(TUser src);

        protected abstract System.Linq.Expressions.Expression<Func<TUser, bool>> UserFilter(User user);

        protected abstract System.Linq.Expressions.Expression<Func<TUser, bool>> UserFilter(string[] userLabels, string authType);

        protected abstract IEnumerable<TUserRole> AllRoles(TUser user);

        protected abstract IEnumerable<CustomUserProperty<TUserId, TUser>> UserProps(TUser user);

        protected abstract Expression<Func<TUser, TUserId>> UserId { get;}

        protected abstract TTrustConfig ConfigureTrustConfigImpl(TTrustConfig trustConfig, string callingMethod);

        protected abstract TExternalOAuthService GetExternalServiceInternal(IBaseTenantContext<TTenant,TWebPlugin,TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> context,string name);

        private ExternalServiceConnection ToExternalDefinition(TExternalOAuthService svc, bool decryptSecret)
        {
            var retVal = new ExternalServiceConnection
            {
                AuthorizationEndpoint = svc.AuthorizationEndpoint,
                ClientId = svc.ClientId,
                Global = svc.TenantId == null,
                Scope = svc.Scope,
                UniqueConnectionName = svc.UniqueConnectionName,
                TokenEndpoint = svc.TokenEndpoint,
                RevocationEndpoint = svc.RevocationEndpoint,
                AuthenticationType = svc.AuthenticationType
            };
            if (!string.IsNullOrEmpty(retVal.ClientSecret) && decryptSecret)
            {
                retVal.ClientSecret = retVal.Global
                    ? svc.ClientSecret.Decrypt()
                    : Decrypt(svc.ClientSecret, securityContext.CurrentTenantName);
            }

            return retVal;
        }

        private TimeZoneInfo GetTimeZone(string permissionScopeName)
        {
            using (new FullSecurityAccessHelper<TTrustConfig>(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = true})))
            {
                var t = securityContext.Tenants.First(n => n.TenantName == permissionScopeName);
                if (!string.IsNullOrEmpty(t.TimeZone))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(t.TimeZone);
                }
            }

            return TimeZoneInfo.Local;
        }

        /// <summary>
        /// raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Estimats a claimData item from a given mapping and an original claim
        /// </summary>
        /// <param name="map">the mapping instruction for estimating a new claim</param>
        /// <param name="original">the original claim-value</param>
        /// <returns>a new claim that must be added to the currently logged on user</returns>
        private ClaimData[] TryGetClaim(AuthenticationClaimMapping map, ClaimData original)
        {
            try
            {
                if (!map.OutgoingClaimValue.StartsWith("^^#"))
                {
                    return new[]
                    {
                        MakeClaim(map,original,!string.IsNullOrEmpty(map.OutgoingClaimValue)
                            ? original.FormatText(map.OutgoingClaimValue)
                            : "")
                    };
                }

                var tmp = ExpressionParser.Parse(map.OutgoingClaimValue.Substring(3), original,
                    d => DefaultCallbacks.PrepareDefaultCallbacks(d.Scope, d.ReplSession));
                if (tmp is IEnumerable<string> tenu)
                {
                    return (from s in tenu
                        select MakeClaim(map, original, s)).ToArray();
                }
            }
            catch
            {
            }

            return null;
        }

        private ClaimData MakeClaim(AuthenticationClaimMapping map, ClaimData original, string value)
        {
            return new ClaimData
            {
                Type = original.FormatText(map.OutgoingClaimName),
                ValueType = !string.IsNullOrEmpty(map.OutgoingValueType)
                    ? original.FormatText(map.OutgoingValueType)
                    : "",
                Issuer = !string.IsNullOrEmpty(map.OutgoingIssuer)
                    ? original.FormatText(map.OutgoingIssuer)
                    : "",
                OriginalIssuer = !string.IsNullOrEmpty(map.OutgoingOriginalIssuer)
                    ? original.FormatText(map.OutgoingOriginalIssuer)
                    : "",
                Value = value
            };
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
