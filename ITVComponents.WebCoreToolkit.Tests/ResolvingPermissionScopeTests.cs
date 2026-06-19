using System;
using System.Linq;
using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Models;
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
        [TestMethod]
        public void RouteOverride_To_Eligible_Scope_Is_Applied()
        {
            var repo = new FakeSecurityRepository("TenantA", "TenantB");
            var ctx = TestSecurity.AuthenticatedContext(repo, route: ("tenant", "TenantB"));
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
            var ctx = TestSecurity.AuthenticatedContext(repo, route: ("tenant", "EvilCorp"));
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
            var tabA = NewScope(TestSecurity.AuthenticatedContext(repoA, route: ("tenant", "TenantA")), defaultScope: "TenantA");
            var tabB = NewScope(TestSecurity.AuthenticatedContext(repoB, route: ("tenant", "TenantB")), defaultScope: "TenantA");

            Assert.AreEqual("TenantA", tabA.PermissionPrefix);
            Assert.AreEqual("TenantB", tabB.PermissionPrefix);
            Assert.AreEqual("TenantA", tabA.PermissionPrefix, "re-reading must be stable and not affected by the other instance");
        }

        [TestMethod]
        public void FixedUserScope_Claim_Overrides_Route()
        {
            var repo = new FakeSecurityRepository("TenantA", "TenantB");
            var ctx = TestSecurity.AuthenticatedContext(repo, route: ("tenant", "TenantB"),
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
            var ctx = TestSecurity.AuthenticatedContext(repo); // no route override
            var scope = NewScope(ctx, defaultScope: "TenantB");

            Assert.AreEqual("TenantB", scope.PermissionPrefix);
        }

        [TestMethod]
        public void TenantLess_User_Resolves_To_No_Scope_Without_Throwing()
        {
            // A signed-in user who is a member of no tenant has zero eligible scopes. Resolution must NOT throw
            // (it used to crash in UpdateScopePermissions' EligibleScopes.First on a null/absent scope); instead
            // the user resolves to no scope and an empty permission set, leaving only the tenant-neutral identity
            // pages reachable.
            var repo = new FakeSecurityRepository(); // no eligible scopes
            var ctx = TestSecurity.AuthenticatedContext(repo);
            var scope = NewScope(ctx, defaultScope: "TenantA");

            Assert.IsNull(scope.PermissionPrefix);
            Assert.AreEqual(0, repo.PermissionScopesQueried.Count, "no scope must ever be resolved for a tenant-less user");
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

        private static InMemoryScope NewScope(IContextUserProvider ctx, string defaultScope)
            => new InMemoryScope(ctx, "tenant", (_, eligibles) =>
                eligibles.Any(e => e.ScopeName == defaultScope) ? defaultScope : eligibles.FirstOrDefault()?.ScopeName);

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
    }
}
