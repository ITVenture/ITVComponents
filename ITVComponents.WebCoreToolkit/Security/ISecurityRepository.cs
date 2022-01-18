using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.Security
{
    public interface ISecurityRepository:IPlugin
    {
        /// <summary>
        /// Gets a list of users in the current application
        /// </summary>
        ICollection<User> Users { get; }

        /// <summary>
        /// Gets a list of Roles that can be granted to users in the current application
        /// </summary>
        ICollection<Role> Roles { get; }

        /// <summary>
        /// Gets a collection of defined Permissions in the current application
        /// </summary>
        ICollection<Permission> Permissions { get; }

        /// <summary>
        /// Gets an enumeration of Roles that are assigned to the given user
        /// </summary>
        /// <param name="user">the user for which to get the roles</param>
        /// <returns>an enumerable of all the user-roles</returns>
        IEnumerable<Role> GetRoles(User user);

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for the given user
        /// </summary>
        /// <param name="user">the user for which to get the custom properties</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        IEnumerable<CustomUserProperty> GetCustomProperties(User user);

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType);

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="originalClaims">the claims that were originally attached to the current identity</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims, string userAuthenticationType);

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given user through its roles
        /// </summary>
        /// <param name="user">the user for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given user</returns>
        IEnumerable<Permission> GetPermissions(User user);

        /// <summary>
        /// Gets an enumeration of Permissions for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of permissions for the given user-labels</returns>
        IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType);

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given Role
        /// </summary>
        /// <param name="role">the role for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given role</returns>
        IEnumerable<Permission> GetPermissions(Role role);

        /// <summary>
        /// Gets a value indicating whether the specified Permission-Scope exists
        /// </summary>
        /// <param name="permissionScopeName">the permissionScope to check for existence</param>
        /// <returns>a value indicating whether the specified permissionScope is valid</returns>
        bool PermissionScopeExists(string permissionScopeName);

        /// <summary>
        /// Gets a list of eligible scopes for the extracted user-labels of the current user
        /// </summary>
        /// <param name="userLabels">the extracted user-labels for the user that is logged on the system</param>
        /// <returns>an enumerable containing all eligible Permission-Scopes that this user has access to</returns>
        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels);

    }
}
