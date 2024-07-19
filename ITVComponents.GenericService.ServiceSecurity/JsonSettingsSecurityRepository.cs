using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ITVComponents.Helpers;
//using ITVComponents.InterProcessCommunication.MessagingShared.Hub.HubSecurity;
using ITVComponents.Security;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.Helpers;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.GenericService.ServiceSecurity
{
    public class JsonSettingsSecurityRepository : ISecurityRepository
    {
        private ICollection<User> bufferedUsers;
        private DateTime lastUserRefresh;
        private ICollection<Role> bufferedRoles;
        private DateTime lastRoleRefresh;
        private ICollection<Permission> bufferedPermissions;
        private DateTime lastPermissionRefresh;

        private object writeSync = new object();

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets a list of users in the current application
        /// </summary>
        public ICollection<User> Users => bufferedUsers = CheckRefresh(bufferedUsers, () => (from t in HostConfiguration.Helper.HostUsers select new User { UserName = t.UserName, AuthenticationType = t.AuthenticationType }).ToList(), ref lastUserRefresh);

        /// <summary>
        /// Gets a list of Roles that can be granted to users in the current application
        /// </summary>
        public ICollection<Role> Roles => bufferedRoles = CheckRefresh(bufferedRoles, () => (from t in HostConfiguration.Helper.HostRoles select new Role { RoleName = t.RoleName }).ToList(), ref lastRoleRefresh);

        /// <summary>
        /// Gets a collection of defined Permissions in the current application
        /// </summary>
        public ICollection<Permission> Permissions => bufferedPermissions = CheckRefresh(bufferedPermissions, () => (from t in HostConfiguration.Helper.KnownHostPermissions select new Permission { PermissionName = t }).ToList(), ref lastPermissionRefresh);

        /// <summary>
        /// Gets an enumeration of Roles that are assigned to the given user
        /// </summary>
        /// <param name="user">the user for which to get the roles</param>
        /// <returns>an enumerable of all the user-roles</returns>
        public IEnumerable<Role> GetRoles(User user)
        {
            var hu = HostConfiguration.Helper.HostUsers.First(n => n.UserName.ToLower() == user.UserName.ToLower() && (string.IsNullOrEmpty(n.AuthenticationType) || string.IsNullOrEmpty(user.AuthenticationType) || n.AuthenticationType == user.AuthenticationType));
            return from r in Roles join gr in hu.Roles on r.RoleName equals gr select r;
        }

        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> permissions, string permissionScope)
        {
            return from u in (from a in (from t in HostConfiguration.Helper.HostRoles
                                         select new
                                         {
                                             PermissionMap = t.Permissions.Select(n => new { t.RoleName, Permission = n }).ToArray()
                                         })
                        .SelectMany(i => i.PermissionMap)
                              join p in permissions on a.Permission equals p
                              select a.RoleName).Distinct()
                   join r in Roles on u equals r.RoleName
                   select r;
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for the given user
        /// </summary>
        /// <param name="user">the user for which to get the custom properties</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public IEnumerable<CustomUserProperty> GetCustomProperties(User user, CustomUserPropertyType propertyType)
        {
            var hu = HostConfiguration.Helper.HostUsers.First(n => n.UserName.ToLower() == user.UserName.ToLower() && (string.IsNullOrEmpty(n.AuthenticationType) || string.IsNullOrEmpty(user.AuthenticationType) || n.AuthenticationType == user.AuthenticationType));
            return hu.CustomInfo.Where(n => n.PropertyType == propertyType).Select(m => new CustomUserProperty
            {
                PropertyName = m.PropertyName,
                Value = m.Value,
                PropertyType = m.PropertyType
            });
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
            T retVal = default;
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
            return false;
        }

        public bool SetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType, T value)
        {
            return false;
        }

        /// <summary>
        /// Get a value indicating, if the resulting userlables result to a user that is authenticated for the current user-scope
        /// </summary>
        /// <param name="userLabels">the user-labels that represent the currently logged on user</param>
        /// <param name="userAuthenticationType">the authentication-type of the current user</param>
        /// <returns>a value indicating whether this user is valid in the current scope</returns>
        public bool IsAuthenticated(string[] userLabels, string userAuthenticationType)
        {
            return Users.Any(n => (string.IsNullOrEmpty(n.AuthenticationType) || n.AuthenticationType == userAuthenticationType) && userLabels.Contains(n.UserName));
        }

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        public IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType, CustomUserPropertyType propertyType)
        {
            return (from t in userLabels
                    join u in HostConfiguration.Helper.HostUsers on new { UserName = t, AuthenticationType = userAuthenticationType } equals new { u.UserName, u.AuthenticationType }
                    select u.CustomInfo).SelectMany(i => i.Where(n => n.PropertyType == propertyType).Select(m => new CustomUserProperty
                    {
                        PropertyName = m.PropertyName,
                        Value = m.Value,
                        PropertyType = m.PropertyType
                    }));
        }

        public IEnumerable<T> GetUserIds<T>(string[] userLabels, string userAuthenticationType)
        {
            return (from t in userLabels
                join u in HostConfiguration.Helper.HostUsers on new
                    { UserName = t, AuthenticationType = userAuthenticationType } equals new
                    { u.UserName, u.AuthenticationType }
                select u.UserName).Cast<T>();
        }

        public T GetUserId<T>(string[] userLabels, string userAuthenticationType)
        {
            var tmp = GetUserIds<T>(userLabels,userAuthenticationType).ToArray();
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
        public IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims,
            string userAuthenticationType)
        {
            return Array.Empty<ClaimData>();
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given user through its roles
        /// </summary>
        /// <param name="user">the user for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given user</returns>
        public IEnumerable<Permission> GetPermissions(User user)
        {
            var hu = HostConfiguration.Helper.HostUsers.First(n => n.UserName.ToLower() == user.UserName.ToLower() && (string.IsNullOrEmpty(n.AuthenticationType) || string.IsNullOrEmpty(user.AuthenticationType) || n.AuthenticationType == user.AuthenticationType));
            return (from t in hu.Roles
                    join r in HostConfiguration.Helper.HostRoles on t equals r.RoleName
                    select r.Permissions).SelectMany(n => n)
                .Select(p => new Permission { PermissionName = p });
        }

        /// <summary>
        /// Gets a list of activated features for a specific permission-Scope
        /// </summary>
        /// <param name="permissionScopeName">the name of the current permission-prefix selected by the current user</param>
        /// <returns>returns a list of activated features</returns>
        public IEnumerable<Feature> GetFeatures(string permissionScopeName)
        {
            return (IEnumerable<Feature>)HostConfiguration.Helper.Features ?? Array.Empty<Feature>();
        }

        public string Decrypt(string encryptedValue, string permissionScopeName)
        {
            return encryptedValue.Decrypt();
        }

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName)
        {
            return encryptedValue.Decrypt();
        }

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            throw new NotImplementedException();
        }

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            throw new NotImplementedException();
        }

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName)
        {
            throw new NotImplementedException();
        }

        public string Encrypt(string value, string permissionScopeName)
        {
            return value.Decrypt();
        }

        public byte[] Encrypt(byte[] value, string permissionScopeName)
        {
            return value.Decrypt();
        }

        public byte[] Encrypt(byte[] value, string permissionScopeName, out byte[] initializationVector, out byte[] salt)
        {
            throw new NotImplementedException();
        }

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName, out byte[] initializationVector,
            out byte[] salt)
        {
            throw new NotImplementedException();
        }

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName)
        {
            throw new NotImplementedException();
        }

        public string EncryptJsonObject(object value, string permissionScopeName)
        {
            throw new NotImplementedException();
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
                                join u in HostConfiguration.Helper.HostUsers on new { UserName = t, AuthenticationType = userAuthenticationType } equals new { u.UserName, u.AuthenticationType }
                                select u.Roles).SelectMany(r => r).Distinct()
                    join hr in HostConfiguration.Helper.HostRoles on ur equals hr.RoleName
                    select hr.Permissions).SelectMany(n => n).Distinct(StringComparer.OrdinalIgnoreCase)
                .Union(TemporaryGrants.GetTemporaryPermissions(userLabels)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(p => new Permission { PermissionName = p });
        }

        /// <summary>
        /// Gets an enumeration of Permissions that are assigned to the given Role
        /// </summary>
        /// <param name="role">the role for which to get the permissions</param>
        /// <returns>an enumerable of permissions for the given role</returns>
        public IEnumerable<Permission> GetPermissions(Role role)
        {
            var ro = HostConfiguration.Helper.HostRoles.First(n => n.RoleName.ToLower() == role.RoleName.ToLower());
            return ro.Permissions.Select(p => new Permission { PermissionName = p });
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
        /// Gets a list of eligible scopes for the extracted user-labels of the current user
        /// </summary>
        /// <param name="userLabels">the extracted user-labels for the user that is logged on the system</param>
        /// <returns>an enumerable containing all eligible Permission-Scopes that this user has access to</returns>
        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string userAuthenticationType)
        {
            return new ScopeInfo[0];
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Checks whether the buffered data of this repository is outdated and requires a refresh
        /// </summary>
        /// <typeparam name="T">the current collection-type</typeparam>
        /// <param name="bufferedInstance">the buffered value</param>
        /// <param name="factory">a factory function that creates the new or initial value</param>
        /// <param name="lastRefresh">a timestamp that represents the last refresh of the given collection</param>
        /// <returns>the current to-use value of the requested collection</returns>
        private T CheckRefresh<T>(T bufferedInstance, Func<T> factory, ref DateTime lastRefresh) where T : class
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

        /// <summary>
        /// Gets a dummy timezone for each attached tenant
        /// </summary>
        /// <param name="permissionScopeName">the permissionscopename</param>
        /// <returns>the target timezone</returns>
        private TimeZoneInfo GetTimeZone(string permissionScopeName)
        {
            return TimeZoneInfo.Local;
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
