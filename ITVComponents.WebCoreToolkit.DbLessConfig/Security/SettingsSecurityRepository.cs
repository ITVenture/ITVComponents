using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.DbLessConfig.Configurations;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.DbLessConfig.Security
{
    public class SettingsSecurityRepository : ISecurityRepository, IPlugin
    {
        private readonly IdentitySettings options;
        private IList<User> bufferedUsers;
        private IList<Role> bufferedRoles;
        private IList<Permission> bufferedPermissions;
        public SettingsSecurityRepository(IOptions<IdentitySettings> options)
        {
            this.options = options.Value;
        }

        public SettingsSecurityRepository()
        {
            this.options = NativeSettings.GetSection<IdentitySettings>(IdentitySettings.SettingsKey);
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets a list of users in the current application
        /// </summary>
        public ICollection<User> Users =>
            bufferedUsers ??= (from t in options.Users
                select new User
                {
                    UserName = t.UserName,
                    AuthenticationType = t.AuthenticationType
                }).ToList();

        /// <summary>
        /// Gets a list of Roles that can be granted to users in the current application
        /// </summary>
        public ICollection<Role> Roles =>
            bufferedRoles ??= (from t in options.Roles
                select new Role
                {
                    RoleName = t.RoleName
                }).ToList();

        /// <summary>
        /// Gets a collection of defined Permissions in the current application
        /// </summary>
        public ICollection<Permission> Permissions =>
            bufferedPermissions ??= (from t in options.ExplicitPermissions??new string[0]
                select new Permission
                {
                    PermissionName = t
                }).ToList();

        /// <summary>
        /// Gets an enumeration of Roles that are assigned to the given user
        /// </summary>
        /// <param name="user">the user for which to get the roles</param>
        /// <returns>an enumerable of all the user-roles</returns>
        public IEnumerable<Role> GetRoles(User user)
        {
            var hu = options.Users.First(n => n.UserName == user.UserName && n.AuthenticationType == user.AuthenticationType);
            return from r in Roles join gr in hu.Roles on r.RoleName equals gr select r;
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for the given user
        /// </summary>
        /// <param name="user">the user for which to get the custom properties</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public IEnumerable<CustomUserProperty> GetCustomProperties(User user)
        {
            var hu = options.Users.First(n => n.UserName == user.UserName && n.AuthenticationType == user.AuthenticationType);
            return hu.CustomInfo.Select(i => new CustomUserProperty {PropertyName = i.Key, Value = i.Value});
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType)
        {
            return (from t in userLabels
                join u in options.Users on new {UserName=t, AuthenticationType=userAuthenticationType} equals new {u.UserName, u.AuthenticationType}
                select u.CustomInfo).SelectMany(i => i).Select(p => new CustomUserProperty
            {
                PropertyName = p.Key,
                Value = p.Value
            });
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given user through its roles
        /// </summary>
        /// <param name="user">the user for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given user</returns>
        public IEnumerable<Permission> GetPermissions(User user)
        {
            var hu = options.Users.First(n => n.UserName == user.UserName && n.AuthenticationType == user.AuthenticationType);
            return (from t in hu.Roles
                    join r in options.Roles on t equals r.RoleName
                    select r.Permissions).SelectMany(n => n).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(p => new Permission {PermissionName = p});
        }

        /// <summary>
        /// Gets an enumeration of Permissions for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of permissions for the given user-labels</returns>
        public IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType)
        {
            return (from ur in (from t in userLabels
                        join u in options.Users on new {UserName=t, AuthenticationType=userAuthenticationType} equals new {u.UserName, u.AuthenticationType}
                        select u.Roles).SelectMany(r => r).Distinct()
                    join hr in options.Roles on ur equals hr.RoleName
                    select hr.Permissions).SelectMany(n => n).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(p => new Permission {PermissionName = p});
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given Role
        /// </summary>
        /// <param name="role">the role for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given role</returns>
        public IEnumerable<Permission> GetPermissions(Role role)
        {
            var ro = options.Roles.First(n => n.RoleName == role.RoleName);
            return ro.Permissions.Select(p => new Permission {PermissionName = p});
        }

        /// <summary>
        /// Gets a value indicating whether the specified Permission-Scope exists
        /// </summary>
        /// <param name="permissionScopeName">the permissionScope to check for existence</param>
        /// <returns>a value indicating whether the specified permissionScope is valid</returns>
        public bool PermissionScopeExists(string permissionScopeName)
        {
            return options.ExplicitPermissionScopes == null || options.ExplicitPermissionScopes.Length == 0 || options.ExplicitPermissionScopes.Contains(permissionScopeName, StringComparer.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Gets a list of eligible scopes for the extracted user-labels of the current user
        /// </summary>
        /// <param name="userLabels">the extracted user-labels for the user that is logged on the system</param>
        /// <returns>an enumerable containing all eligible Permission-Scopes that this user has access to</returns>
        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels)
        {
            return new ScopeInfo[0];
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();

        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
