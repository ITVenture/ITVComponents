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
using ITVComponents.WebCoreToolkit.Security.UserScopes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ToolkitClaimTypes = ITVComponents.WebCoreToolkit.ClaimTypes;
using UserScope = ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels.UserScope;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Security-critical tests for the shared scope-resolution engine extracted from CookiePermissionScope
    /// (Phase 5.4 step 1). They lock in the tenant-isolation gate that every <see cref="IPermissionScope"/>
    /// strategy passes through: a route override may only select a scope the user is actually eligible for,
    /// and independent instances (== independent Blazor circuits / browser tabs) never bleed scope into one
    /// another. The CookiePermissionRepo push -> VerifyUserPermissions integration is verified at runtime in a
    /// host (it depends on the internal SecurityRepository stack); these tests cover the resolution decision.
    /// </summary>
    [TestClass]
    public class ResolvingPermissionScopeTests
    {
        private const string AuthType = "TestAuth";

        [TestMethod]
        public void RouteOverride_To_Eligible_Scope_Is_Applied()
        {
            var repo = new FakeSecurityRepository("TenantA", "TenantB");
            var ctx = AuthenticatedContext(repo, route: ("tenant", "TenantB"));
            var scope = NewScope(ctx, defaultScope: "TenantA");

            Assert.AreEqual("TenantB", scope.PermissionPrefix);
            Assert.IsTrue(scope.IsScopeExplicit);
            CollectionAssert.Contains(repo.PermissionScopesQueried, "TenantB");
        }

        [TestMethod]
        public void RouteOverride_To_Ineligible_Scope_Is_Rejected()
        {
            // The tenant-isolation gate: a forged/route-injected scope the user is NOT eligible for must be
            // ignored, and permissions must NEVER be resolved against it.
            var repo = new FakeSecurityRepository("TenantA", "TenantB");
            var ctx = AuthenticatedContext(repo, route: ("tenant", "EvilCorp"));
            var scope = NewScope(ctx, defaultScope: "TenantA");

            Assert.AreEqual("TenantA", scope.PermissionPrefix, "ineligible route override must fall back to the default scope");
            CollectionAssert.DoesNotContain(repo.PermissionScopesQueried, "EvilCorp");
        }

        [TestMethod]
        public void PerInstance_Scopes_Do_Not_Bleed()
        {
            // Two independent instances model two circuits / browser tabs. Each must resolve its own scope.
            var repoA = new FakeSecurityRepository("TenantA", "TenantB");
            var repoB = new FakeSecurityRepository("TenantA", "TenantB");
            var tabA = NewScope(AuthenticatedContext(repoA, route: ("tenant", "TenantA")), defaultScope: "TenantA");
            var tabB = NewScope(AuthenticatedContext(repoB, route: ("tenant", "TenantB")), defaultScope: "TenantA");

            Assert.AreEqual("TenantA", tabA.PermissionPrefix);
            Assert.AreEqual("TenantB", tabB.PermissionPrefix);
            Assert.AreEqual("TenantA", tabA.PermissionPrefix, "re-reading must be stable and not affected by the other instance");
        }

        [TestMethod]
        public void FixedUserScope_Claim_Overrides_Route()
        {
            var repo = new FakeSecurityRepository("TenantA", "TenantB");
            var ctx = AuthenticatedContext(repo, route: ("tenant", "TenantB"),
                extraClaims: new Claim(ToolkitClaimTypes.FixedUserScope, "LockedTenant"));
            var scope = NewScope(ctx, defaultScope: "TenantA");

            Assert.AreEqual("LockedTenant", scope.PermissionPrefix);
            Assert.IsTrue(scope.IsScopeExplicit);
            Assert.AreEqual(0, repo.PermissionScopesQueried.Count, "a fixed user scope short-circuits eligibility resolution entirely");
        }

        [TestMethod]
        public void No_Route_Falls_Back_To_Default()
        {
            var repo = new FakeSecurityRepository("TenantA", "TenantB");
            var ctx = AuthenticatedContext(repo); // no route override
            var scope = NewScope(ctx, defaultScope: "TenantB");

            Assert.AreEqual("TenantB", scope.PermissionPrefix);
        }

        [TestMethod]
        public void Anonymous_User_Resolves_To_No_Scope()
        {
            var repo = new FakeSecurityRepository("TenantA", "TenantB");
            var ctx = new FakeContextUserProvider
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()), // not authenticated
                Services = new FakeServiceProvider(repo, new FakeUserNameMapper())
            };
            ctx.RouteData["tenant"] = "TenantB";
            var scope = NewScope(ctx, defaultScope: "TenantA");

            Assert.IsNull(scope.PermissionPrefix);
            Assert.AreEqual(0, repo.PermissionScopesQueried.Count);
        }

        // ----- helpers -------------------------------------------------------

        private static InMemoryScope NewScope(IContextUserProvider ctx, string defaultScope)
            => new InMemoryScope(ctx, "tenant", (_, eligibles) =>
                eligibles.Any(e => e.ScopeName == defaultScope) ? defaultScope : eligibles.FirstOrDefault()?.ScopeName);

        private static FakeContextUserProvider AuthenticatedContext(ISecurityRepository repo,
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

        /// <summary>In-memory resolving scope — a minimal preview of the Blazor ScopedPermissionScope (step 2).</summary>
        private sealed class InMemoryScope : ResolvingPermissionScope
        {
            private readonly string routeParam;
            private readonly Func<IContextUserProvider, ScopeInfo[], string> defaultExpr;
            private UserScope token;

            public InMemoryScope(IContextUserProvider ctx, string routeParam, Func<IContextUserProvider, ScopeInfo[], string> defaultExpr)
                : base(ctx, NullLogger<InMemoryScope>.Instance)
            {
                this.routeParam = routeParam;
                this.defaultExpr = defaultExpr;
            }

            protected override string RouteOverrideParam => routeParam;
            protected override Func<IContextUserProvider, ScopeInfo[], string> DefaultScopeExpression => defaultExpr;
            protected override UserScope LoadStoredToken() => token;
            protected override bool IsTokenStale(UserScope t) => false;
            protected override void PersistToken(UserScope t) => token = t;
        }

        private sealed class FakeContextUserProvider : IContextUserProvider
        {
            public ClaimsPrincipal User { get; set; }
            public IServiceProvider Services { get; set; }
            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            public string RequestPath { get; set; }
        }

        private sealed class FakeServiceProvider : IServiceProvider
        {
            private readonly ISecurityRepository repo;
            private readonly IUserNameMapper mapper;

            public FakeServiceProvider(ISecurityRepository repo, IUserNameMapper mapper)
            {
                this.repo = repo;
                this.mapper = mapper;
            }

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(ISecurityRepository)) return repo;
                if (serviceType == typeof(IUserNameMapper)) return mapper;
                return null;
            }
        }

        private sealed class FakeUserNameMapper : IUserNameMapper
        {
            public string[] GetUserLabels(IIdentity user) => new[] { "tester" };
            public string UniqueName { get; set; }
            public void Dispose() { }
            public event EventHandler Disposed;
        }

        /// <summary>
        /// Minimal <see cref="ISecurityRepository"/> double. Implements only what the resolution engine touches
        /// and records every permission-scope it is asked to resolve, so tests can prove resolution happens only
        /// against eligible scopes.
        /// </summary>
        private sealed class FakeSecurityRepository : ISecurityRepository
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
}
