using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Plugins;
using ITVComponents.Security;
using ITVComponents.Settings.Native;
using ITVComponents.TypeConversion;
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
            var hu = options.Users.First(n => n.UserName == user.UserName && (string.IsNullOrEmpty(n.AuthenticationType) || string.IsNullOrEmpty(user.AuthenticationType) || n.AuthenticationType == user.AuthenticationType));
            return from r in Roles join gr in hu.Roles on r.RoleName equals gr select r;
        }

        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> permissions, string permissionScope)
        {
            return from u in (from a in (from t in options.Roles
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
            var hu = options.Users.First(n => n.UserName == user.UserName && (string.IsNullOrEmpty(n.AuthenticationType) || string.IsNullOrEmpty(user.AuthenticationType) || n.AuthenticationType == user.AuthenticationType));
            return hu.CustomInfo.Where(n => n.PropertyType == propertyType).Select(i => new CustomUserProperty
            {
                PropertyName = i.PropertyName, Value = i.Value,
                PropertyType = i.PropertyType

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
            return options.Users.Any(n =>
                n.AuthenticationType == userAuthenticationType && userLabels.Contains(n.UserName));
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
                join u in options.Users on new {UserName=t, AuthenticationType=userAuthenticationType} equals new {u.UserName, u.AuthenticationType}
                select u.CustomInfo).SelectMany(i => i).Where(n => n.PropertyType == propertyType).Select(p => new CustomUserProperty
            {
                PropertyName = p.PropertyName,
                Value = p.Value,
                PropertyType = p.PropertyType
            });
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
            var hu = options.Users.First(n => n.UserName == user.UserName && (string.IsNullOrEmpty(n.AuthenticationType) || string.IsNullOrEmpty(user.AuthenticationType) || n.AuthenticationType == user.AuthenticationType));
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
        /// Gets a list of activated features for a specific permission-Scope
        /// </summary>
        /// <param name="permissionScopeName">the name of the current permission-prefix selected by the current user</param>
        /// <returns>returns a list of activated features</returns>
        public IEnumerable<Feature> GetFeatures(string permissionScopeName)
        {
            return options.Features??Array.Empty<Feature>();
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
