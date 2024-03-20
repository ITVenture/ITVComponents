using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.AspNetCore.Authentication;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    internal class AssetSecurityRepository:ISecurityRepository
    {
        private readonly ClaimsPrincipal decoratedUser;
        private readonly ISecurityRepository decoratedRepo;
        private readonly string[] assignedPermissions;
        private readonly string[] assignedFeatures;
        private readonly string assignedUserScope;

        public AssetSecurityRepository(ClaimsPrincipal taggedUser, ISecurityRepository decoratedRepo, AssetInfo info)
        {
            decoratedUser = taggedUser;
            this.decoratedRepo = decoratedRepo;
            assignedPermissions = info.Permissions;
            assignedFeatures = info.Features;
            assignedUserScope = info.UserScopeName;
        }
        public AssetSecurityRepository(ClaimsPrincipal taggedUser, ISecurityRepository decoratedRepo)
        {
            decoratedUser = taggedUser;
            this.decoratedRepo = decoratedRepo;
            assignedPermissions =
                (from t in taggedUser.Claims where t.Type == ClaimTypes.FixedAssetPermission select t.Value).Distinct()
                .ToArray();
            assignedUserScope = taggedUser.Claims.First(n => n.Type == ClaimTypes.FixedUserScope).Value;
            assignedFeatures = (from t in taggedUser.Claims where t.Type == ClaimTypes.FixedAssetFeature select t.Value)
                .Distinct().ToArray();
        }

        public void Dispose()
        {
        }

        public event EventHandler Disposed;
        public string UniqueName { get; set; }

        public ICollection<User> Users => new[]  { new User { AuthenticationType = ((ClaimsIdentity)decoratedUser.Identity).AuthenticationType, UserName = decoratedUser.Identity.Name } };

        public ICollection<Role> Roles => new[] { new Role { RoleName = "Me" } };
        public ICollection<Permission> Permissions => decoratedRepo.Permissions;
        public IEnumerable<Role> GetRoles(User user)
        {
            if (user.UserName.Equals(decoratedUser.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                yield return new Role { RoleName = "Me" };
            }
        }

        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> permissions, string permissionScope)
        {
            return decoratedRepo.GetRolesWithPermissions(permissions, permissionScope);
        }

        public IEnumerable<CustomUserProperty> GetCustomProperties(User user, CustomUserPropertyType propertyType)
        {
            return Array.Empty<CustomUserProperty>();
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
            return null;
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
            return default(T);
        }

        public bool SetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType, string value)
        {
            return false;
        }

        public bool SetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType, T value)
        {
            return false;
        }

        public bool IsAuthenticated(string[] userLabels, string userAuthenticationType)
        {
            if (userLabels.Length == 1 && userLabels[0].Equals(decoratedUser.Identity.Name,StringComparison.OrdinalIgnoreCase) && userAuthenticationType ==
                ((ClaimsIdentity)decoratedUser.Identity).AuthenticationType)
            {
                return true;
            }

            return false;
        }

        public IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType, CustomUserPropertyType propertyType)
        {
            return Array.Empty<CustomUserProperty>();
        }

        public IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims, string userAuthenticationType)
        {
            return Array.Empty<ClaimData>();
        }

        public IEnumerable<Permission> GetPermissions(User user)
        {
            if (user.UserName.Equals(decoratedUser.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return from t in assignedPermissions
                    select new Permission { PermissionName = t };
            }

            return Array.Empty<Permission>();
        }

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType)
        {
            if (userLabels.Length == 1 && userLabels[0].Equals(decoratedUser.Identity.Name, StringComparison.OrdinalIgnoreCase) && userAuthenticationType ==
                ((ClaimsIdentity)decoratedUser.Identity).AuthenticationType)
            {
                return from t in assignedPermissions
                    select new Permission { PermissionName = t };
            }

            return Array.Empty<Permission>();
        }

        public IEnumerable<Permission> GetPermissions(Role role)
        {
            if (role.RoleName == "Me")
            {
                return from t in assignedPermissions
                    select new Permission { PermissionName = t };
            }

            return Array.Empty<Permission>();
        }

        public bool PermissionScopeExists(string permissionScopeName)
        {
            return decoratedUser.HasClaim(ClaimTypes.FixedUserScope, permissionScopeName);
        }

        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string userAuthenticationType)
        {
            if (userLabels.Length == 1 && userLabels[0].Equals(decoratedUser.Identity.Name, StringComparison.OrdinalIgnoreCase) && userAuthenticationType ==
                ((ClaimsIdentity)decoratedUser.Identity).AuthenticationType)
            {
                yield return new ScopeInfo
                    { ScopeDisplayName = assignedUserScope, ScopeName = "Limited Asset Scope" };
            }
        }

        public IEnumerable<Feature> GetFeatures(string permissionScopeName)
        {
            if (string.IsNullOrEmpty(permissionScopeName) || permissionScopeName.Equals(assignedUserScope,StringComparison.OrdinalIgnoreCase))
            {
                return from t in assignedFeatures
                    select new Feature
                    {
                        Enabled = true,
                        FeatureDescription = null,
                        FeatureName = t
                    };
            }

            return Array.Empty<Feature>();
        }

        public string Decrypt(string encryptedValue, string permissionScopeName)
        {
            return decoratedRepo.Decrypt(encryptedValue, permissionScopeName);
        }

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName)
        {
            return decoratedRepo.Decrypt(encryptedValue, permissionScopeName);
        }

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            return decoratedRepo.Decrypt(encryptedValue, permissionScopeName, initializationVector, salt);
        }

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            return decoratedRepo.GetDecryptStream(baseStream, permissionScopeName, initializationVector, salt);
        }

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName)
        {
            return decoratedRepo.GetDecryptStream(baseStream, permissionScopeName);
        }

        public string Encrypt(string value, string permissionScopeName)
        {
            return decoratedRepo.Encrypt(value, permissionScopeName);
        }

        public byte[] Encrypt(byte[] value, string permissionScopeName)
        {
            return decoratedRepo.Encrypt(value, permissionScopeName);
        }

        public byte[] Encrypt(byte[] value, string permissionScopeName, out byte[] initializationVector, out byte[] salt)
        {
            return decoratedRepo.Encrypt(value, permissionScopeName, out initializationVector, out salt);
        }

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName, out byte[] initializationVector,
            out byte[] salt)
        {
            return decoratedRepo.GetEncryptStream(baseStream, permissionScopeName, out initializationVector, out salt);
        }

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName)
        {
            return decoratedRepo.GetEncryptStream(baseStream, permissionScopeName);
        }

        public string EncryptJsonObject(object value, string permissionScopeName)
        {
            return decoratedRepo.EncryptJsonObject(value, permissionScopeName);
        }
    }
}
