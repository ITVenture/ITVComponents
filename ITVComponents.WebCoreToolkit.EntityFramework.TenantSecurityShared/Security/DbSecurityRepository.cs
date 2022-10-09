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
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Logging;
using CustomUserProperty = ITVComponents.WebCoreToolkit.Models.CustomUserProperty;
using Feature = ITVComponents.WebCoreToolkit.Models.Feature;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Permission = ITVComponents.WebCoreToolkit.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.Models.Role;
using User = ITVComponents.WebCoreToolkit.Models.User;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Security
{
    public abstract class DbSecurityRepository<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser> : ISecurityRepository
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
        where TUserProperty : CustomUserProperty<TUserId, TUser>, new()
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
        where TUser : class
    {
        private readonly ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser> securityContext;
        private readonly ILogger logger;

        protected DbSecurityRepository(ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser> securityContext,
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
                using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
                return (from u in securityContext.Users
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
                using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
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
                using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
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
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            return (from r in AllRoles(securityContext.Users.First(UserFilter(user))) select r.Role).ToArray();
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for the given user
        /// </summary>
        /// <param name="user">the user for which to get the custom properties</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public virtual IEnumerable<CustomUserProperty> GetCustomProperties(User user, CustomUserPropertyType propertyType)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
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
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
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
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
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

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="originalClaims">the claims that were originally attached to the current identity</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public virtual IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims,
            string userAuthenticationType)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            var typeClaims = securityContext.AuthenticationClaimMappings.Where(n =>
                n.AuthenticationType.AuthenticationTypeName == userAuthenticationType).ToArray();
            var preMapped = from t in originalClaims 
                join i in typeClaims on t.Type equals i.IncomingClaimName
                select new { Original = t, Map = i };
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
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
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
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
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
                join ur in securityContext.TenantUserRoles on tr.TenantUserId equals ur.TenantUserId
                    join r in securityContext.SecurityRoles on new {ur.RoleId, tr.TenantId} equals new { r.RoleId, r.TenantId }
                    join rp in securityContext.RolePermissions on new {r.RoleId, r.TenantId} equals new {rp.RoleId, rp.TenantId}
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
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
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
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            return securityContext.Tenants.Any(n => n.TenantName == permissionScopeName);
        }

        public virtual IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string authType)
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, true, false);
            
            if(userLabels.Any(n => Regex.IsMatch(n, Global.AppUserKeyPattern)))
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
        /// Gets a list of activated features for a specific permission-Scope
        /// </summary>
        /// <param name="permissionScopeName">the name of the current permission-prefix selected by the current user</param>
        /// <returns>returns a list of activated features</returns>
        public virtual IEnumerable<Feature> GetFeatures(string permissionScopeName)
        {
            IDisposable tmp = null;
            try
            {
                if (!securityContext.Tenants.Any(n => n.TenantName == permissionScopeName))
                {
                    tmp = new FullSecurityAccessHelper(securityContext, true, true);
                }

                var dt = DateTime.SpecifyKind(DateTime.UtcNow,DateTimeKind.Local);
                var raw = (from t in securityContext.Features
                    join a in securityContext.TenantFeatureActivations.Where(ta =>
                            ta.Tenant.TenantName == permissionScopeName
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
            using (var h = new FullSecurityAccessHelper(securityContext, true, true))
            {
                var t = securityContext.Tenants.First(n => n.TenantName == permissionScopeName);
                if (!string.IsNullOrEmpty(t.TenantPassword))
                {
                    return Convert.FromBase64String(t.TenantPassword);
                }
            }

            return null;
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
