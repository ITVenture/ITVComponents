using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Json;
using ITVComponents.WebCoreToolkit.Helpers;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security.UserScopes.Helpers;

namespace ITVComponents.WebCoreToolkit.Security.UserScopes
{
    internal class CookiePermissionRepo:ISecurityRepository
    {
        private readonly AuthTypeUserLabels[] userLabels;
        private readonly ScopeInfo[] eligibleScopes;
        private readonly string currentScope;
        private readonly string[] permissions;
        private readonly Permission[] knownPermissions;
        private readonly Feature[] features;
        private readonly ISecurityRepository parentRepo;

        public CookiePermissionRepo(AuthTypeUserLabels[] userLabels, ScopeInfo[] eligibleScopes, string currentScope, string[] permissions, Permission[] knownPermissions, Feature[] features, ISecurityRepository parentRepo)
        {
            this.userLabels = userLabels;
            this.eligibleScopes = eligibleScopes;
            this.currentScope = currentScope;
            this.permissions = permissions;
            this.knownPermissions = knownPermissions;
            this.features = features;
            this.parentRepo = parentRepo;
        }

        public void Dispose()
        {
        }

        public event EventHandler Disposed;
        public string UniqueName { get; set; }
        public ICollection<User> Users => parentRepo.Users;
        public ICollection<Role> Roles => parentRepo.Roles;
        public ICollection<Permission> Permissions => knownPermissions;

        // The auto-permission-registration bootstrap must reach the writable backing store. This snapshot sits on
        // top of the repo stack once a scope is resolved, so without this passthrough the call would dead-end at
        // the ISecurityRepository default no-op and no permission would ever be created/granted.
        public void EnsureRequestedPermissions(string[] permissionNames, AutoPermissionsOptions options) =>
            parentRepo.EnsureRequestedPermissions(permissionNames, options);

        // Only consulted on an authorization miss (auto-reg fast-path), i.e. rarely — forward to the backing store
        // rather than snapshotting the roles into the cookie.
        public string[] GetGlobalRoles(string[] userLabels, string userAuthenticationType) =>
            parentRepo.GetGlobalRoles(userLabels, userAuthenticationType);

        public IEnumerable<Role> GetRoles(User user) => parentRepo.GetRoles(user);

        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> requiredPermissions,
            string permissionScope) => parentRepo.GetRolesWithPermissions(requiredPermissions, permissionScope);

        public IEnumerable<CustomUserProperty> GetCustomProperties(User user, CustomUserPropertyType propertyType) =>
            parentRepo.GetCustomProperties(user, propertyType);

        public string GetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType) =>
            parentRepo.GetCustomProperty(user, propertyName, propertyType);

        public T GetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType) =>
            parentRepo.GetCustomProperty<T>(user, propertyName, propertyType);

        public bool SetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType,
            string value) => parentRepo.SetCustomProperty(user, propertyName, propertyType, value);


        public bool
            SetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType, T value) =>
            parentRepo.SetCustomProperty(user, propertyName, propertyType, value);

        public bool IsAuthenticated(string[] userLabels, string userAuthenticationType)
        {
            if (UserValidateHelper.IsUserOk(userLabels, userAuthenticationType, this.userLabels, currentScope:currentScope))
            {
                return true;
            }

            return parentRepo.IsAuthenticated(userLabels, userAuthenticationType);
        }

        public bool IsAuthenticated(string[] userLabels, string forScope, string userAuthenticationType)
        {
            if (UserValidateHelper.IsUserOk(userLabels, userAuthenticationType, this.userLabels, forScope, currentScope: currentScope))
            {
                return true;
            }

            Console.WriteLine(JsonHelper.ToJson(userLabels, SerializationTypingMode.StaticTyping));
            return parentRepo.IsAuthenticated(userLabels, forScope, userAuthenticationType);
        }

        public IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType,
            CustomUserPropertyType propertyType) =>
            parentRepo.GetCustomProperties(userLabels, userAuthenticationType, propertyType);

        public IEnumerable<T> GetUserIds<T>(string[] userLabels, string userAuthenticationType) =>
            parentRepo.GetUserIds<T>(userLabels, userAuthenticationType);

        public T GetUserId<T>(string[] userLabels, string userAuthenticationType) =>
            parentRepo.GetUserId<T>(userLabels, userAuthenticationType);

        public IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims, string userAuthenticationType) =>
            parentRepo.GetCustomProperties(originalClaims, userAuthenticationType);

        public IEnumerable<Permission> GetPermissions(User user) => parentRepo.GetPermissions(user);

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType)
        {
            if (UserValidateHelper.IsUserOk(userLabels, userAuthenticationType, this.userLabels, currentScope: currentScope))
            {
                return permissions.Select(n => new Permission { PermissionName = n });
            }

            return parentRepo.GetPermissions(userLabels, userAuthenticationType);
        }

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string forScope, string userAuthenticationType)
        {
            if (UserValidateHelper.IsUserOk(userLabels, userAuthenticationType, this.userLabels, forScope, currentScope: currentScope))
            {
                return permissions.Select(n => new Permission { PermissionName = n });
            }

            return parentRepo.GetPermissions(userLabels, forScope, userAuthenticationType);
        }

        public IEnumerable<Permission> GetPermissions(Role role) => parentRepo.GetPermissions(role);

        public bool PermissionScopeExists(string permissionScopeName)
        {
            if (permissionScopeName == currentScope)
            {
                return true;
            }

            return parentRepo.PermissionScopeExists(permissionScopeName);
        }

        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string userAuthenticationType)
        {
            if (UserValidateHelper.IsUserOk(userLabels, userAuthenticationType, this.userLabels, currentScope: currentScope))
            {
                return eligibleScopes;
            }

            return parentRepo.GetEligibleScopes(userLabels, userAuthenticationType);
        }

        // Lazy tenant-tree primitives are not snapshot-cached (they resolve per expand); forward to the parent
        // repository. Without this explicit passthrough the interface default (empty list) would swallow the call
        // once this decorator sits on top of the repo stack.
        public IReadOnlyList<TenantTreeNode> GetRootTenants(string[] userLabels, string userAuthenticationType)
            => parentRepo.GetRootTenants(userLabels, userAuthenticationType);

        public IReadOnlyList<TenantTreeNode> GetChildTenants(string[] userLabels, string userAuthenticationType, int parentTenantId, int[] carriedRoleIds)
            => parentRepo.GetChildTenants(userLabels, userAuthenticationType, parentTenantId, carriedRoleIds);

        public IEnumerable<Feature> GetFeatures(string permissionScopeName)
        {
            if (permissionScopeName == currentScope)
            {
                return features;
            }

            return parentRepo.GetFeatures(permissionScopeName);
        }

        public TimeZoneHelper GetTimeZoneHelper(string permissionScopeName) =>
            parentRepo.GetTimeZoneHelper(permissionScopeName);

        public string Decrypt(string encryptedValue, string permissionScopeName) =>
            parentRepo.Decrypt(encryptedValue, permissionScopeName);

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName) =>
            parentRepo.Decrypt(encryptedValue, permissionScopeName);

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName, byte[] initializationVector,
            byte[] salt) => parentRepo.Decrypt(encryptedValue, permissionScopeName, initializationVector, salt);

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName, byte[] initializationVector,
            byte[] salt) => parentRepo.GetDecryptStream(baseStream, permissionScopeName, initializationVector, salt);

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName) =>
            parentRepo.GetDecryptStream(baseStream, permissionScopeName);

        public string Encrypt(string value, string permissionScopeName) =>
            parentRepo.Encrypt(value, permissionScopeName);

        public byte[] Encrypt(byte[] value, string permissionScopeName) =>
            parentRepo.Encrypt(value, permissionScopeName);

        public byte[] Encrypt(byte[] value, string permissionScopeName, out byte[] initializationVector,
            out byte[] salt) => parentRepo.Encrypt(value, permissionScopeName, out initializationVector, out salt);

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName, out byte[] initializationVector,
            out byte[] salt) =>
            parentRepo.GetEncryptStream(baseStream, permissionScopeName, out initializationVector, out salt);

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName) =>
            parentRepo.GetEncryptStream(baseStream, permissionScopeName);

        public string EncryptJsonObject(object value, string permissionScopeName) =>
            parentRepo.EncryptJsonObject(value, permissionScopeName);

        public Permission[] GetKnownPermissions(string permissionScope)
        {
            if (permissionScope == currentScope)
            {
                return knownPermissions;
            }

            return parentRepo.GetKnownPermissions(permissionScope);
        }

        public ExternalServiceConnection GetExternalService(string name, bool decryptSecret = false) =>parentRepo.GetExternalService(name, decryptSecret);

        public void PrepareExternalServiceConnect(OAuthState oAuthState) =>
            parentRepo.PrepareExternalServiceConnect(oAuthState);

        public OAuthState GetOAuthRequest(string connectionName, string state)=> parentRepo.GetOAuthRequest(connectionName, state);

        public void StoreExternalServiceToken(string connectionName, TranslatedTokenResponse token) =>
            parentRepo.StoreExternalServiceToken(connectionName, token);

        public TranslatedTokenResponse GetBufferedToken(string connectionName, bool forRevoke, bool throwIfNull, out ExternalServiceConnection connectionInfo, out Action<TranslatedTokenResponse> updateToken)=>parentRepo.GetBufferedToken(connectionName, forRevoke, throwIfNull, out connectionInfo, out updateToken);
    }
}
