﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.WebToolkitOverrides
{
    internal class JsonSettingsSecurityRepository:ISecurityRepository
    {
        private ICollection<User> bufferedUsers;
        private DateTime lastUserRefresh;
        private ICollection<Role> bufferedRoles;
        private DateTime lastRoleRefresh;
        private ICollection<Permission> bufferedPermissions;
        private DateTime lastPermissionRefresh;

        private object writeSync = new object();

        /// <summary>
        /// Gets a list of users in the current application
        /// </summary>
        public ICollection<User> Users => bufferedUsers = CheckRefresh(bufferedUsers, () => (from t in HubConfiguration.Helper.HubUsers select new User {UserName = t.UserName, AuthenticationType = t.AuthenticationType}).ToList(), ref lastUserRefresh);

        /// <summary>
        /// Gets a list of Roles that can be granted to users in the current application
        /// </summary>
        public ICollection<Role> Roles => bufferedRoles = CheckRefresh(bufferedRoles, ()=>(from t in HubConfiguration.Helper.HubRoles select new Role{RoleName = t.RoleName}).ToList(), ref lastRoleRefresh);

        /// <summary>
        /// Gets a collection of defined Permissions in the current application
        /// </summary>
        public ICollection<Permission> Permissions => bufferedPermissions = CheckRefresh(bufferedPermissions, ()=>(from t in HubConfiguration.Helper.KnownHubPermissions select new Permission{PermissionName = t}).ToList(), ref lastPermissionRefresh);

        /// <summary>
        /// Gets an enumeration of Roles that are assigned to the given user
        /// </summary>
        /// <param name="user">the user for which to get the roles</param>
        /// <returns>an enumerable of all the user-roles</returns>
        public IEnumerable<Role> GetRoles(User user)
        {
            var hu = HubConfiguration.Helper.HubUsers.First(n => n.UserName == user.UserName && n.AuthenticationType== user.AuthenticationType);
            return from r in Roles join gr in hu.Roles on r.RoleName equals gr select r;
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for the given user
        /// </summary>
        /// <param name="user">the user for which to get the custom properties</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public IEnumerable<CustomUserProperty> GetCustomProperties(User user)
        {
            var hu = HubConfiguration.Helper.HubUsers.First(n => n.UserName == user.UserName && n.AuthenticationType == user.AuthenticationType);
            return hu.CustomInfo;
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
                join u in HubConfiguration.Helper.HubUsers on new {UserName=t, AuthenticationType=userAuthenticationType} equals new {u.UserName, u.AuthenticationType}
                select u.CustomInfo).SelectMany(i => i);
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given user through its roles
        /// </summary>
        /// <param name="user">the user for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given user</returns>
        public IEnumerable<Permission> GetPermissions(User user)
        {
            var hu = HubConfiguration.Helper.HubUsers.First(n => n.UserName == user.UserName && n.AuthenticationType == user.AuthenticationType);
            return (from t in hu.Roles
                    join r in HubConfiguration.Helper.HubRoles on t equals r.RoleName
                    select r.Permissions).SelectMany(n => n)
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
                        join u in HubConfiguration.Helper.HubUsers on new {UserName=t, AuthenticationType=userAuthenticationType} equals new {u.UserName, u.AuthenticationType}
                        select u.Roles).SelectMany(r => r).Distinct()
                    join hr in HubConfiguration.Helper.HubRoles on ur equals hr.RoleName
                    select hr.Permissions).SelectMany(n => n).Distinct(StringComparer.OrdinalIgnoreCase)
                .Union(TemporaryGrants.GetTemporaryPermissions(userLabels)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(p => new Permission {PermissionName = p});
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given Role
        /// </summary>
        /// <param name="role">the role for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given role</returns>
        public IEnumerable<Permission> GetPermissions(Role role)
        {
            var ro = HubConfiguration.Helper.HubRoles.First(n => n.RoleName == role.RoleName);
            return ro.Permissions.Select(p => new Permission {PermissionName = p});
        }

        /// <summary>
        /// this is not used!
        /// </summary>
        /// <param name="permissionScopeName">the permissionscope to check</param>
        /// <returns>always true</returns>
        public bool PermissionScopeExists(string permissionScopeName)
        {
            return true;
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

        /// <summary>
        /// Checks whether the buffered data of this repository is outdated and requires a refresh
        /// </summary>
        /// <typeparam name="T">the current collection-type</typeparam>
        /// <param name="bufferedInstance">the buffered value</param>
        /// <param name="factory">a factory function that creates the new or initial value</param>
        /// <param name="lastRefresh">a timestamp that represents the last refresh of the given collection</param>
        /// <returns>the current to-use value of the requested collection</returns>
        private T CheckRefresh<T>(T bufferedInstance, Func<T> factory, ref DateTime lastRefresh) where T:class
        {
            T retVal = bufferedInstance;
            if (retVal == null || DateTime.Now.Subtract(lastRefresh).TotalMinutes > 2)
            {
                lock (writeSync)
                {
                    retVal = factory();
                }
            }

            return retVal;
        }
    }
}
