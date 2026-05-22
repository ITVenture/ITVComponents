using System.Linq;
using ITVComponents.WebCoreToolkit.Blazor.Security;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Tests for the real Blazor <see cref="ScopedPermissionScope"/> type. They confirm that the in-memory,
    /// route-driven strategy wires the shared resolution engine through its options correctly — i.e. the
    /// tenant-isolation gate proven in <see cref="ResolvingPermissionScopeTests"/> is actually reached by the
    /// concrete Blazor type, and that independent instances (== browser tabs) resolve independently.
    /// </summary>
    [TestClass]
    public class ScopedPermissionScopeTests
    {
        [TestMethod]
        public void RouteOverride_To_Eligible_Scope_Is_Applied()
        {
            var repo = new FakeSecurityRepository("TenantA", "TenantB");
            var scope = NewScope(TestSecurity.AuthenticatedContext(repo, route: ("tenant", "TenantB")), defaultScope: "TenantA");

            Assert.AreEqual("TenantB", scope.PermissionPrefix);
            CollectionAssert.Contains(repo.PermissionScopesQueried, "TenantB");
        }

        [TestMethod]
        public void RouteOverride_To_Ineligible_Scope_Is_Rejected()
        {
            var repo = new FakeSecurityRepository("TenantA", "TenantB");
            var scope = NewScope(TestSecurity.AuthenticatedContext(repo, route: ("tenant", "EvilCorp")), defaultScope: "TenantA");

            Assert.AreEqual("TenantA", scope.PermissionPrefix);
            CollectionAssert.DoesNotContain(repo.PermissionScopesQueried, "EvilCorp");
        }

        [TestMethod]
        public void PerInstance_Scopes_Do_Not_Bleed()
        {
            var repoA = new FakeSecurityRepository("TenantA", "TenantB");
            var repoB = new FakeSecurityRepository("TenantA", "TenantB");
            var tabA = NewScope(TestSecurity.AuthenticatedContext(repoA, route: ("tenant", "TenantA")), defaultScope: "TenantA");
            var tabB = NewScope(TestSecurity.AuthenticatedContext(repoB, route: ("tenant", "TenantB")), defaultScope: "TenantA");

            Assert.AreEqual("TenantA", tabA.PermissionPrefix);
            Assert.AreEqual("TenantB", tabB.PermissionPrefix);
        }

        [TestMethod]
        public void No_Route_Uses_Configured_Default_Expression()
        {
            var repo = new FakeSecurityRepository("TenantA", "TenantB");
            var scope = NewScope(TestSecurity.AuthenticatedContext(repo), defaultScope: "TenantB");

            Assert.AreEqual("TenantB", scope.PermissionPrefix);
        }

        private static ScopedPermissionScope NewScope(IContextUserProvider ctx, string defaultScope)
        {
            var options = Microsoft.Extensions.Options.Options.Create(new ScopedPermissionScopeOptions
            {
                RouteOverrideParam = "tenant",
                DefaultScopeExpression = (_, eligibles) =>
                    eligibles.Any(e => e.ScopeName == defaultScope) ? defaultScope : eligibles.FirstOrDefault()?.ScopeName!
            });
            return new ScopedPermissionScope(ctx, options, NullLogger<ScopedPermissionScope>.Instance);
        }
    }
}
