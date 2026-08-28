using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using ITVComponents.WebCoreToolkit.Helpers;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Shared lightweight test doubles for the permission-scope tests. Kept minimal: only the members the
    /// shared resolution engine actually touches are implemented; everything else throws.
    /// </summary>
    internal static class TestSecurity
    {
        public const string AuthType = "TestAuth";

        public static FakeContextUserProvider AuthenticatedContext(ISecurityRepository repo,
            (string key, string value)? route = null, params Claim[] extraClaims)
        {
            var claims = new List<Claim> { new Claim(System.Security.Claims.ClaimTypes.Name, "tester") };
            claims.AddRange(extraClaims);
            var identity = new ClaimsIdentity(claims, AuthType);
            var ctx = new FakeContextUserProvider
            {
                User = new ClaimsPrincipal(identity),
                Services = new FakeServiceProvider(repo, new FakeUserNameMapper())
            };
            if (route.HasValue)
            {
                ctx.RouteData[route.Value.key] = route.Value.value;
            }
            return ctx;
        }
    }

    internal sealed class FakeContextUserProvider : IContextUserProvider
    {
        public ClaimsPrincipal User { get; set; }
        public IServiceProvider Services { get; set; }
        public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public string RequestPath { get; set; }
    }

    internal sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly ISecurityRepository repo;
        private readonly IUserNameMapper mapper;

        public FakeServiceProvider(ISecurityRepository repo, IUserNameMapper mapper)
        {
            this.repo = repo;
            this.mapper = mapper;
        }

        /// <summary>Der Benutzer der laufenden Anfrage, wo ein Test ihn braucht; sonst null.</summary>
        public IContextUserProvider ContextUser { get; set; }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(ISecurityRepository)) return repo;
            if (serviceType == typeof(IUserNameMapper)) return mapper;
            if (serviceType == typeof(IContextUserProvider)) return ContextUser;
            return null;
        }
    }

    internal sealed class FakeUserNameMapper : IUserNameMapper
    {
        public string[] GetUserLabels(IIdentity user) => new[] { "tester" };
        public string UniqueName { get; set; }
        public void Dispose() { }
        public event EventHandler Disposed;
    }

    /// <summary>
    /// Minimal <see cref="ISecurityRepository"/> double that records every permission-scope it is asked to
    /// resolve, so tests can prove resolution happens only against eligible scopes.
    /// </summary>
    internal sealed class FakeSecurityRepository : ISecurityRepository
    {
        private readonly string[] eligible;

        public FakeSecurityRepository(params string[] eligibleScopes)
        {
            eligible = eligibleScopes;
        }

        public List<string> PermissionScopesQueried { get; } = new List<string>();

        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string userAuthenticationType)
            => eligible.Select(s => new ScopeInfo { ScopeName = s, ScopeDisplayName = s });

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string forScope, string userAuthenticationType)
        {
            PermissionScopesQueried.Add(forScope);
            return new[] { new Permission { PermissionName = forScope + ".Read" } };
        }

        public Permission[] GetKnownPermissions(string permissionScope)
            => new[] { new Permission { PermissionName = permissionScope + ".Read" } };

        public IEnumerable<Feature> GetFeatures(string permissionScopeName) => Array.Empty<Feature>();

        // ----- unused members ------------------------------------------------
        public string UniqueName { get; set; }
        public void Dispose() { }
        public event EventHandler Disposed;
        public ICollection<User> Users => throw new NotImplementedException();
        public ICollection<Role> Roles => throw new NotImplementedException();
        public ICollection<Permission> Permissions => throw new NotImplementedException();
        public IEnumerable<Role> GetRoles(User user) => throw new NotImplementedException();
        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> requiredPermissions, string permissionScope) => throw new NotImplementedException();
        public IEnumerable<CustomUserProperty> GetCustomProperties(User user, CustomUserPropertyType propertyType) => throw new NotImplementedException();
        public string GetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType) => throw new NotImplementedException();
        public T GetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType) => throw new NotImplementedException();
        public bool SetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType, string value) => throw new NotImplementedException();
        public bool SetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType, T value) => throw new NotImplementedException();
        public bool IsAuthenticated(string[] userLabels, string userAuthenticationType) => throw new NotImplementedException();
        public bool IsAuthenticated(string[] userLabels, string forScope, string userAuthenticationType) => throw new NotImplementedException();
        public IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType, CustomUserPropertyType propertyType) => throw new NotImplementedException();
        public IEnumerable<T> GetUserIds<T>(string[] userLabels, string userAuthenticationType) => throw new NotImplementedException();
        public T GetUserId<T>(string[] userLabels, string userAuthenticationType) => throw new NotImplementedException();
        public IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims, string userAuthenticationType) => throw new NotImplementedException();
        public IEnumerable<Permission> GetPermissions(User user) => throw new NotImplementedException();
        public IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType) => throw new NotImplementedException();
        public IEnumerable<Permission> GetPermissions(Role role) => throw new NotImplementedException();
        public bool PermissionScopeExists(string permissionScopeName) => throw new NotImplementedException();
        public TimeZoneHelper GetTimeZoneHelper(string permissionScopeName) => throw new NotImplementedException();
        public string Decrypt(string encryptedValue, string permissionScopeName) => throw new NotImplementedException();
        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName) => throw new NotImplementedException();
        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName, byte[] initializationVector, byte[] salt) => throw new NotImplementedException();
        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName, byte[] initializationVector, byte[] salt) => throw new NotImplementedException();
        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName) => throw new NotImplementedException();
        public string Encrypt(string value, string permissionScopeName) => throw new NotImplementedException();
        public byte[] Encrypt(byte[] value, string permissionScopeName) => throw new NotImplementedException();
        public byte[] Encrypt(byte[] value, string permissionScopeName, out byte[] initializationVector, out byte[] salt) => throw new NotImplementedException();
        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName, out byte[] initializationVector, out byte[] salt) => throw new NotImplementedException();
        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName) => throw new NotImplementedException();
        public string EncryptJsonObject(object value, string permissionScopeName) => throw new NotImplementedException();
        public ExternalServiceConnection GetExternalService(string name, bool decryptSecret = false) => throw new NotImplementedException();
        public void PrepareExternalServiceConnect(OAuthState oAuthState) => throw new NotImplementedException();
        public OAuthState GetOAuthRequest(string connectionName, string state) => throw new NotImplementedException();
        public void StoreExternalServiceToken(string connectionName, TranslatedTokenResponse token) => throw new NotImplementedException();
        public TranslatedTokenResponse GetBufferedToken(string connectionName, bool forRevoke, bool throwIfNull, out ExternalServiceConnection connectionInfo, out Action<TranslatedTokenResponse> updateToken) => throw new NotImplementedException();
    }
}
