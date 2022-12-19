using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
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
        /// Gets all Roles that have the requested permissions
        /// </summary>
        /// <param name="requiredPermissions">a list of permissions for which to get the appropriate roles</param>
        /// <param name="permissionScope">the permissionscope for which to fetch the requested roles</param>
        /// <returns>a list of roles that have the requested permissions</returns>
        IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> requiredPermissions, string permissionScope);

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for the given user
        /// </summary>
        /// <param name="user">the user for which to get the custom properties</param>
        /// <param name="propertyType">the expected property type</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        IEnumerable<CustomUserProperty> GetCustomProperties(User user, CustomUserPropertyType propertyType);

        /// <summary>
        /// Gets the string representation of the given property. This is only supported in 1:1 user environments
        /// </summary>
        /// <param name="user">the user for which go get the property</param>
        /// <param name="propertyName">the name of the desired property</param>
        /// <param name="propertyType">the expected property-type</param>
        /// <returns>the string representation of the requested property</returns>
        string GetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType);

        /// <summary>
        /// Gets the string representation of the given property. This is only supported in 1:1 user environments
        /// </summary>
        /// <param name="user">the user for which go get the property</param>
        /// <param name="propertyName">the name of the desired property</param>
        /// <param name="propertyType">the expected property-type</param>
        /// <returns>the string representation of the requested property</returns>
        T GetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType);

        /// <summary>
        /// Sets the specified property for the given user
        /// </summary>
        /// <param name="user">the user for which to set a property</param>
        /// <param name="propertyName">the property-name</param>
        /// <param name="propertyType">the property type</param>
        /// <param name="value">the value of the property</param>
        /// <returns>a value indicating whether the property could be saved</returns>
        bool SetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType, string value);

        /// <summary>
        /// Sets the specified property for the given user
        /// </summary>
        /// <param name="user">the user for which to set a property</param>
        /// <param name="propertyName">the property-name</param>
        /// <param name="propertyType">the property type</param>
        /// <param name="value">the value of the property</param>
        /// <returns>a value indicating whether the property could be saved</returns>
        bool SetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType, T value);

        /// <summary>
        /// Get a value indicating, if the resulting userlables result to a user that is authenticated for the current user-scope
        /// </summary>
        /// <param name="userLabels">the user-labels that represent the currently logged on user</param>
        /// <param name="userAuthenticationType">the authentication-type of the current user</param>
        /// <returns>a value indicating whether this user is valid in the current scope</returns>
        bool IsAuthenticated(string[] userLabels, string userAuthenticationType);

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="userLabels">the labels that describe the current user</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <param name="propertyType">the requested property type</param>
        /// <returns>an enumerable of all the custom user-properties for this user</returns>
        IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType, CustomUserPropertyType propertyType);

        /// <summary>
        /// Gets an enumeration of CustomUserProperties for a set of user-labels that is appropriate for the given user
        /// </summary>
        /// <param name="originalClaims">the claims that were originally attached to the current identity</param>
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <param name="propertyType">the requested propertyType</param>
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
        /// <param name="userAuthenticationType">the authentication-type that was used to authenticate current user</param>
        /// <returns>an enumerable containing all eligible Permission-Scopes that this user has access to</returns>
        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string userAuthenticationType);

        /// <summary>
        /// Gets a list of activated features for a specific permission-Scope
        /// </summary>
        /// <param name="permissionScopeName">the name of the current permission-prefix selected by the current user</param>
        /// <returns>returns a list of activated features</returns>
        IEnumerable<Feature> GetFeatures(string permissionScopeName);

        /// <summary>
        /// Decrypts a value with the appropriate settings
        /// </summary>
        /// <param name="encryptedValue">the encrypted value</param>
        /// <param name="permissionScopeName">the name of the eliged permission scope</param>
        /// <returns>the decrypted value</returns>
        string Decrypt(string encryptedValue, string permissionScopeName);

        /// <summary>
        /// Decrypts a value with the appropriate settings
        /// </summary>
        /// <param name="encryptedValue">the encrypted value</param>
        /// <param name="permissionScopeName">the name of the eliged permission scope</param>
        /// <returns>the decrypted value</returns>
        byte[] Decrypt(byte[] encryptedValue, string permissionScopeName);

        /// <summary>
        /// Decrypts a value with the appropriate settings
        /// </summary>
        /// <param name="encryptedValue">the encrypted value</param>
        /// <param name="permissionScopeName">the name of the eliged permission scope</param>
        /// <param name="initializationVector">the initializationVector that was used for encryption</param>
        /// <param name="salt">the salt that was used for encryption</param>
        /// <returns>the decrypted value</returns>
        byte[] Decrypt(byte[] encryptedValue, string permissionScopeName, byte[] initializationVector, byte[] salt);

        /// <summary>
        /// Wraps the given stream with a Decrypt-Stream for the current permission-scope
        /// </summary>
        /// <param name="baseStream">the base-stream that contains encrypted data</param>
        /// <param name="permissionScopeName">the permission-scope that is permitted to read the given file</param>
        /// <param name="initializationVector">the initialization vector that is used for decrypting the data</param>
        /// <param name="salt">the salt that was used for the encryption</param>
        /// <returns>a decrypt-stream that can be used to read the cleartext-data</returns>
        Stream GetDecryptStream(Stream baseStream, string permissionScopeName, byte[] initializationVector, byte[] salt);

        /// <summary>
        /// Wraps the given stream with a Decrypt-Stream for the current permission-scope
        /// </summary>
        /// <param name="baseStream">the base-stream that contains encrypted data</param>
        /// <param name="permissionScopeName">the permission-scope that is permitted to read the given file</param>
        /// <returns>a decrypt-stream that can be used to read the cleartext-data</returns>
        Stream GetDecryptStream(Stream baseStream, string permissionScopeName);

        /// <summary>
        /// Decrypts a value with the appropriate settings
        /// </summary>
        /// <param name="value">the value to encrypt</param>
        /// <param name="permissionScopeName">the name of the eliged permission scope</param>
        /// <returns>the encrypted value</returns>
        string Encrypt(string value, string permissionScopeName);

        /// <summary>
        /// Decrypts a value with the appropriate settings
        /// </summary>
        /// <param name="value">the value to encrypt</param>
        /// <param name="permissionScopeName">the name of the eliged permission scope</param>
        /// <returns>the encrypted value</returns>
        byte[] Encrypt(byte[] value, string permissionScopeName);

        /// <summary>
        /// Decrypts a value with the appropriate settings
        /// </summary>
        /// <param name="value">the value to encrypt</param>
        /// <param name="permissionScopeName">the name of the eliged permission scope</param>
        /// <param name="initializationVector">the initializationVector that was used for encryption</param>
        /// <param name="salt">the salt that was used for encryption</param>
        /// <returns>the encrypted value</returns>
        byte[] Encrypt(byte[] value, string permissionScopeName, out byte[] initializationVector, out byte[] salt);

        /// <summary>
        /// Creates a decrypt-stream that is capable to encrypt data for the given permission-scope
        /// </summary>
        /// <param name="baseStream">the stream that points to a resource that will save the encrypted data</param>
        /// <param name="permissionScopeName">the permission-scope that is permitted to access the data</param>
        /// <param name="initializationVector">the initialization vector that was used to initialize the encryption</param>
        /// <param name="salt">the salt that was used for encryption</param>
        /// <returns>a stream that will encrypt any data written and forwards it to the base-stream</returns>
        Stream GetEncryptStream(Stream baseStream, string permissionScopeName, out byte[] initializationVector, out byte[] salt);

        /// <summary>
        /// Creates a decrypt-stream that is capable to encrypt data for the given permission-scope
        /// </summary>
        /// <param name="baseStream">the stream that points to a resource that will save the encrypted data</param>
        /// <param name="permissionScopeName">the permission-scope that is permitted to access the data</param>
        /// <returns>a stream that will encrypt any data written and forwards it to the base-stream</returns>
        Stream GetEncryptStream(Stream baseStream, string permissionScopeName);

        /// <summary>
        /// Serializes an object to Json and encrypts string values when prefixed with "encrypt:"
        /// </summary>
        /// <param name="value">the object to serialize</param>
        /// <param name="permissionScopeName">the permission-scope that is permitted to access the data</param>
        /// <returns>the serialized and when demanded encrypted representation of the provided object</returns>
        string EncryptJsonObject(object value, string permissionScopeName);
    }
}
