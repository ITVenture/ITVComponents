using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

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
                (from t in taggedUser.Claims where t.Type == Global.FixedAssetPermission select t.Value).Distinct()
                .ToArray();
            assignedUserScope = taggedUser.Claims.First(n => n.Type == Global.FixedAssetUserScope).Value;
            assignedFeatures = (from t in taggedUser.Claims where t.Type == Global.FixedAssetFeature select t.Value)
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
            if (user.UserName == decoratedUser.Identity.Name)
            {
                yield return new Role { RoleName = "Me" };
            }
        }

        public IEnumerable<CustomUserProperty> GetCustomProperties(User user)
        {
            return Array.Empty<CustomUserProperty>();
        }

        public bool IsAuthenticated(string[] userLabels, string userAuthenticationType)
        {
            if (userLabels.Length == 1 && userLabels[0] == decoratedUser.Identity.Name && userAuthenticationType ==
                ((ClaimsIdentity)decoratedUser.Identity).AuthenticationType)
            {
                return true;
            }

            return false;
        }

        public IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType)
        {
            return Array.Empty<CustomUserProperty>();
        }

        public IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims, string userAuthenticationType)
        {
            return Array.Empty<ClaimData>();
        }

        public IEnumerable<Permission> GetPermissions(User user)
        {
            if (user.UserName == decoratedUser.Identity.Name)
            {
                return from t in assignedPermissions
                    select new Permission { PermissionName = t };
            }

            return Array.Empty<Permission>();
        }

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType)
        {
            if (userLabels.Length == 1 && userLabels[0] == decoratedUser.Identity.Name && userAuthenticationType ==
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
            return decoratedUser.HasClaim(Global.FixedAssetUserScope, permissionScopeName);
        }

        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string userAuthenticationType)
        {
            if (userLabels.Length == 1 && userLabels[0] == decoratedUser.Identity.Name && userAuthenticationType ==
                ((ClaimsIdentity)decoratedUser.Identity).AuthenticationType)
            {
                yield return new ScopeInfo
                    { ScopeDisplayName = assignedUserScope, ScopeName = "Limited Asset Scope" };
            }
        }

        public IEnumerable<Feature> GetFeatures(string permissionScopeName)
        {
            if (permissionScopeName == assignedUserScope)
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
    }
}
