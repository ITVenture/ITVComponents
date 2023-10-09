using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynamitey;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.Security
{
    public sealed class SecurityRepository : ISecurityRepository
    {
        private Stack<ISecurityRepository> repos = new Stack<ISecurityRepository>();

        private ISecurityRepository rootRepo;

        private object sync = new object();

        private int rootCallCount = 0;

        internal ISecurityRepository Current
        {
            get
            {
                bool usePeek;
                lock (sync)
                {
                    usePeek = rootCallCount == 0;
                }
                if (usePeek && repos.TryPeek(out var ret))
                {
                    return ret;
                }

                return rootRepo;
            }
        }

        internal bool ExplicitSuppressed
        {
            get
            {
                lock (sync)
                {
                     return rootCallCount != 0;
                }
            }
        }

        internal void UseRoot()
        {
            lock (sync)
            {
                rootCallCount++;
            }
        }

        internal void ResetRoot()
        {
            lock (sync)
            {
                rootCallCount--;
            }
        }

        internal void PushRepo(ISecurityRepository next)
        {
            if (rootRepo == null)
            {
                rootRepo = next;
            }

            repos.Push(next);
        }

        internal ISecurityRepository PopRepo()
        {
            return repos.Pop();
        }

        public void Dispose()
        {
            while (repos.TryPop(out var r))
            {
                r.Dispose();
            }

            //OnDisposed();
        }

        //public event EventHandler Disposed;
        public ICollection<User> Users => Current.Users;
        public ICollection<Role> Roles => Current.Roles;
        public ICollection<Permission> Permissions => Current.Permissions;
        public IEnumerable<Role> GetRoles(User user) => Current.GetRoles(user);
        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> requiredPermissions, string permissionScope)
        {
            return Current.GetRolesWithPermissions(requiredPermissions, permissionScope);
        }

        public IEnumerable<CustomUserProperty> GetCustomProperties(User user, CustomUserPropertyType propertyType) => Current.GetCustomProperties(user, propertyType);

        public string GetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType) => Current.GetCustomProperty(user, propertyName, propertyType);

        public T GetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType) => Current.GetCustomProperty<T>(user, propertyName, propertyType);

        public bool SetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType, string value) => Current.SetCustomProperty(user, propertyName, propertyType, value);

        public bool SetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType, T value) => Current.SetCustomProperty(user, propertyName, propertyType, value);

        public bool IsAuthenticated(string[] userLabels, string userAuthenticationType) => Current.IsAuthenticated(userLabels,userAuthenticationType);

        public IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType, CustomUserPropertyType propertyType) => Current.GetCustomProperties(userLabels,userAuthenticationType, propertyType);

        public IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims, string userAuthenticationType) => Current.GetCustomProperties(originalClaims,userAuthenticationType);

        public IEnumerable<Permission> GetPermissions(User user) => Current.GetPermissions(user);

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType) => Current.GetPermissions(userLabels,userAuthenticationType);

        public IEnumerable<Permission> GetPermissions(Role role) => Current.GetPermissions(role);

        public bool PermissionScopeExists(string permissionScopeName) => Current.PermissionScopeExists(permissionScopeName);

        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string userAuthenticationType) => Current.GetEligibleScopes(userLabels,userAuthenticationType);

        public IEnumerable<Feature> GetFeatures(string permissionScopeName) => Current.GetFeatures(permissionScopeName);

        public string Decrypt(string encryptedValue, string permissionScopeName) => Current.Decrypt(encryptedValue,permissionScopeName);

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName) => Current.Decrypt(encryptedValue, permissionScopeName);

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName, byte[] initializationVector, byte[] salt) => Current.Decrypt(encryptedValue, permissionScopeName,initializationVector,salt);

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName, byte[] initializationVector,
            byte[] salt) => Current.GetDecryptStream(baseStream, permissionScopeName, initializationVector, salt);

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName) =>
            Current.GetDecryptStream(baseStream, permissionScopeName);

        public string Encrypt(string value, string permissionScopeName) => Current.Encrypt(value, permissionScopeName);

        public byte[] Encrypt(byte[] value, string permissionScopeName) => Current.Encrypt(value, permissionScopeName);

        public byte[] Encrypt(byte[] value, string permissionScopeName, out byte[] initializationVector, out byte[] salt) => Current.Encrypt(value, permissionScopeName, out initializationVector, out salt);

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName, out byte[] initializationVector,
            out byte[] salt) =>
            Current.GetEncryptStream(baseStream, permissionScopeName, out initializationVector, out salt);

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName) =>
            Current.GetEncryptStream(baseStream, permissionScopeName);

        public string EncryptJsonObject(object value, string permissionScopeName) => Current.EncryptJsonObject(value, permissionScopeName);

        /*private void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }*/
    }
}
