using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security;
using System.Text.RegularExpressions;
using Castle.Core.Logging;
using ITVComponents.Formatting;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Security;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Helpers;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using CustomUserProperty = ITVComponents.WebCoreToolkit.Models.CustomUserProperty;
using Feature = ITVComponents.WebCoreToolkit.Models.Feature;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Permission = ITVComponents.WebCoreToolkit.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.Models.Role;
using User = ITVComponents.WebCoreToolkit.Models.User;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Security
{
    public abstract class DbSecurityRepository<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> : ISecurityRepository
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
        where TUserProperty : CustomUserProperty<TUserId, TUser>, new()
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
        where TUser : class
        where TTenant : Tenant
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    {
        private readonly ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> securityContext;
        private readonly ILogger logger;

        protected DbSecurityRepository(ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> securityContext,
            ILogger logger)
        {
            this.securityContext = securityContext;
            this.logger = logger;
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
                using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext,
                    new() { ShowAllTenants = false, HideGlobals = false });
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
                using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
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
                using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
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
            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
            return (from r in AllRoles(securityContext.Users.First(UserFilter(user))) select r.Role).ToArray();
        }

        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> requiredPermissions,
            string permissionScope)
        {
            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = true, HideGlobals = false });

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
            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
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
                    retVal = JsonHelper.FromJsonString<T>(tmpVal);
                }
            }

            return retVal;
        }

        public bool SetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType, string value)
        {
            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
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
                stringVal = JsonHelper.ToJson(value);
            }

            return SetCustomProperty(user, propertyName, propertyType, stringVal);
        }

        public virtual bool IsAuthenticated(string[] userLabels, string userAuthenticationType)
        {
            var t = securityContext.CurrentTenantId;
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

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public virtual IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType, CustomUserPropertyType propertyType)
        {
            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
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

            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
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
            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
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
            return (from t in preMapped where string.IsNullOrEmpty(t.Map.Condition) || (ExpressionParser.Parse(t.Map.Condition, t.Original) is bool b && b)
                   select TryGetClaim(t.Map,t.Original)).Where(n => n != null);
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given user through its roles
        /// </summary>
        /// <param name="user">the user for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given user</returns>
        public virtual IEnumerable<Permission> GetPermissions(User user)
        {
            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
            return (from p in (from r in AllRoles(securityContext.Users.First(UserFilter(user))) select r.Role.RolePermissions).SelectMany(rp => rp) select new Permission
            {
                //PermissionName = $"{(!p.Permission.IsGlobal?p.Tenant.TenantName:"")}{p.Permission.PermissionName}"
                PermissionName = p.Permission.PermissionName
            }).Distinct().ToArray();
        }

        /// <summary>
        /// Gets an enumeration of Permissions for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of permissions for the given user-labels</returns>
        public virtual IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType)
        {
            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
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

            var permRaw = (from tr in tenantUsers.Where(UserFilter(userLabels,userAuthenticationType))
                    .Join(securityContext.TenantUsers, UserId, tr => tr.UserId, (tu,tt) => tt)
                join ur in securityContext.TenantUserRoles/*.Where(n => n.TenantUserId != null && n.RoleId != null)*/ on tr.TenantUserId equals ur.TenantUserId.Value
                    join r in securityContext.SecurityRoles on new {RoleId=ur.RoleId.Value, tr.TenantId} equals new { r.RoleId, r.TenantId }
                    join rp in securityContext.RolePermissions/*.Where(n => n.RoleId != null)*/ on new {r.RoleId, r.TenantId} equals new {RoleId=rp.RoleId.Value, rp.TenantId}
                    join rt in securityContext.Tenants on rp.TenantId equals rt.TenantId
                    join p in securityContext.Permissions on rp.PermissionId equals p.PermissionId
                    select new Permission
                    {
                        //PermissionName = p.PermissionName != rt.TenantName?$"{(!p.IsGlobal?rt.TenantName:"")}{p.PermissionName}":p.PermissionName
                        PermissionName = p.PermissionName
                    }).Distinct().ToArray();
            if (preFilteredPerms != null)
            {
                permRaw = (from t in permRaw join p in preFilteredPerms on t.PermissionName equals p select t)
                    .ToArray();
            }

            return permRaw;
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given Role
        /// </summary>
        /// <param name="role">the role for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given role</returns>
        public virtual IEnumerable<Permission> GetPermissions(Role role)
        {
            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
            if (role is TRole dbRole)
            {
                return from p in dbRole.RolePermissions select p.Permission;
            }

            return (from p in securityContext.SecurityRoles.First(r => r.RoleName == role.RoleName).RolePermissions select new Permission
            {
                //PermissionName = $"{(!p.Permission.IsGlobal ? p.Tenant.TenantName : "")}{p.Permission.PermissionName}"
                PermissionName = p.Permission.PermissionName
            }).ToArray();
        }

        /// <summary>
        /// Gets a value indicating whether the specified Permission-Scope exists
        /// </summary>
        /// <param name="permissionScopeName">the permissionScope to check for existence</param>
        /// <returns>a value indicating whether the specified permissionScope is valid</returns>
        public virtual bool PermissionScopeExists(string permissionScopeName)
        {
            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = false, HideGlobals = false });
            return securityContext.Tenants.Any(n => n.TenantName == permissionScopeName);
        }

        public virtual IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string authType)
        {
            using var tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = true, HideGlobals = false });

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
                if (!useCurrentTenant && !securityContext.Tenants.Any(n => n.TenantName == permissionScopeName))
                {
                    tmp = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = true, HideGlobals = true});
                }

                var dt = DateTime.UtcNow;//DateTime.SpecifyKind(DateTime.UtcNow,DateTimeKind.Local);
                var raw = (from t in securityContext.Features
                    join a in securityContext.TenantFeatureActivations.Where(ta =>
                                ((!useCurrentTenant && ta.Tenant.TenantName == permissionScopeName) || (useCurrentTenant && ta.TenantId == securityContext.CurrentTenantId))
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
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = GetEncryptionKey(permissionScopeName);
            }

            if (passwd != null)
            {
                return AesEncryptor.Decrypt(encryptedValue, passwd);
            }

            return encryptedValue.Decrypt();
        }

        public virtual byte[] Decrypt(byte[] encryptedValue, string permissionScopeName)
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = GetEncryptionKey(permissionScopeName);
            }

            if (passwd != null)
            {
                return AesEncryptor.Decrypt(encryptedValue, passwd);
            }

            return encryptedValue.Decrypt();
        }

        public virtual byte[] Decrypt(byte[] encryptedValue, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = GetEncryptionKey(permissionScopeName);
            }

            if (passwd != null)
            {
                return AesEncryptor.Decrypt(encryptedValue, passwd, initializationVector, salt);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        public virtual Stream GetDecryptStream(Stream baseStream, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = GetEncryptionKey(permissionScopeName);
            }

            if (passwd != null)
            {
                return AesEncryptor.GetDecryptStream(baseStream, passwd, initializationVector, salt);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        public virtual Stream GetDecryptStream(Stream baseStream, string permissionScopeName)
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = GetEncryptionKey(permissionScopeName);
            }

            if (passwd != null)
            {
                return AesEncryptor.GetDecryptStream(baseStream, passwd);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        public virtual string Encrypt(string value, string permissionScopeName)
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = GetEncryptionKey(permissionScopeName);
            }

            if (passwd != null)
            {
                return AesEncryptor.Encrypt(value, passwd);
            }

            return value.Encrypt();
        }

        public virtual byte[] Encrypt(byte[] value, string permissionScopeName)
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = GetEncryptionKey(permissionScopeName);
            }

            if (passwd != null)
            {
                return AesEncryptor.Encrypt(value, passwd);
            }

            return value.Encrypt();
        }

        public virtual byte[] Encrypt(byte[] value, string permissionScopeName, out byte[] initializationVector, out byte[] salt)
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = GetEncryptionKey(permissionScopeName);
            }

            if (passwd != null)
            {
                return AesEncryptor.Encrypt(value, passwd, out initializationVector, out salt);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        public virtual Stream GetEncryptStream(Stream baseStream, string permissionScopeName, out byte[] initializationVector,
            out byte[] salt)
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = GetEncryptionKey(permissionScopeName);
            }

            if (passwd != null)
            {
                return AesEncryptor.GetEncryptStream(baseStream, passwd, out initializationVector, out salt);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        public virtual Stream GetEncryptStream(Stream baseStream, string permissionScopeName)
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = GetEncryptionKey(permissionScopeName);
            }

            if (passwd != null)
            {
                return AesEncryptor.GetEncryptStream(baseStream, passwd);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        public string EncryptJsonObject(object value, string permissionScopeName)
        {
            byte[] passwd = null;
            if (!string.IsNullOrEmpty(permissionScopeName))
            {
                passwd = GetEncryptionKey(permissionScopeName);
            }

            if (passwd != null)
            {
                return value.EncryptJsonValues(passwd);
            }

            throw new InvalidOperationException("This is only supported for explicit tenant-encryption");
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        protected abstract User SelectUser(TUser src);

        protected abstract System.Linq.Expressions.Expression<Func<TUser, bool>> UserFilter(User user);

        protected abstract System.Linq.Expressions.Expression<Func<TUser, bool>> UserFilter(string[] userLabels, string authType);

        protected abstract IEnumerable<TUserRole> AllRoles(TUser user);

        protected abstract IEnumerable<CustomUserProperty<TUserId, TUser>> UserProps(TUser user);

        protected abstract Expression<Func<TUser, TUserId>> UserId { get;}

        private byte[] GetEncryptionKey(string permissionScopeName)
        {
            using (var h = new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = true, HideGlobals = true}))
            {
                var t = securityContext.Tenants.First(n => n.TenantName == permissionScopeName);
                if (!string.IsNullOrEmpty(t.TenantPassword))
                {
                    return Convert.FromBase64String(t.TenantPassword);
                }
            }

            return null;
        }

        private TimeZoneInfo GetTimeZone(string permissionScopeName)
        {
            using (new FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>(securityContext, new() { ShowAllTenants = true, HideGlobals = true}))
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
        private ClaimData TryGetClaim(AuthenticationClaimMapping map, ClaimData original)
        {
            try
            {
                return new ClaimData
                {
                    Type = original.FormatText(map.OutgoingClaimName),
                    ValueType = !string.IsNullOrEmpty(map.OutgoingValueType) ? original.FormatText(map.OutgoingValueType) : "",
                    Issuer = !string.IsNullOrEmpty(map.OutgoingIssuer) ? original.FormatText(map.OutgoingIssuer) : "",
                    OriginalIssuer = !string.IsNullOrEmpty(map.OutgoingOriginalIssuer) ? original.FormatText(map.OutgoingOriginalIssuer) : "",
                    Value = !string.IsNullOrEmpty(map.OutgoingClaimValue) ? original.FormatText(map.OutgoingClaimValue) : ""
                };
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
