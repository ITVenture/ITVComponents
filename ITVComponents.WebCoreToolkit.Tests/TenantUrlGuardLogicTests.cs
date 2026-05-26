using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Blazor.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms <see cref="TenantUrlGuardLogic.PlanPathSegmentRewrite"/> differentiates an escape-catch
    /// (rewrite under the current tenant) from a legitimate cross-tenant switch (let through, the
    /// forceLoad/fresh circuit picks up the new base href). Same shape as the MLM-found regression:
    /// before the fix, every non-prefixed URL was rewritten — even a tenant-picker's own target.
    /// </summary>
    [TestClass]
    public class TenantUrlGuardLogicTests
    {
        private static readonly IList<string> AuthExclusions = new List<string>
        {
            "/Account/",
            "/Identity/Account/",
            "/Logout",
            "/Login"
        };

        [TestMethod]
        public void Same_Prefix_Passes_Through()
        {
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/T001/",
                target: new Uri("https://app/T001/users"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001", "ADM"));

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Escape_Without_Tenant_Prefix_Is_Rewritten_Under_Current()
        {
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/T001/",
                target: new Uri("https://app/users"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001", "ADM"));

            Assert.AreEqual("/T001/users", result);
        }

        [TestMethod]
        public void Cross_Tenant_Switch_To_Eligible_Scope_Passes_Through()
        {
            // The MLM-symptom case: tenant-picker navigates /T001/ → /ADM/counter; without the
            // eligibility check, the guard would (wrongly) rewrite to /T001/ADM/counter.
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/T001/",
                target: new Uri("https://app/ADM/counter"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001", "ADM"));

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Ineligible_Foreign_Prefix_Stays_Escape_Catch()
        {
            // Same shape as the cross-tenant switch — but the segment is NOT in the user's eligible
            // scopes. The receiving middleware would 404 it anyway, but the guard keeps the URL inside
            // the current tenant so the user stays in a valid context.
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/T001/",
                target: new Uri("https://app/EvilCorp/foo"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001", "ADM"));

            Assert.AreEqual("/T001/EvilCorp/foo", result);
        }

        [TestMethod]
        public void Auth_Excluded_Path_Passes_Through()
        {
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/T001/",
                target: new Uri("https://app/Identity/Account/Login"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001"));

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Blazor_Internal_Path_Passes_Through()
        {
            // Defensive: /_blazor and /_framework normally don't come through NavigationManager, but
            // if a host wires a custom forwarder we still want them untouched.
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/T001/",
                target: new Uri("https://app/_blazor/initializers"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001"));

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Flat_BasePath_Is_NoOp()
        {
            // Query-mode hosts (or pre-switch state) have basePath "/" — nothing to enforce.
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/",
                target: new Uri("https://app/users"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001"));

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Rewrite_Preserves_Query_And_Fragment()
        {
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/T001/",
                target: new Uri("https://app/users?id=42#top"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001"));

            Assert.AreEqual("/T001/users?id=42#top", result);
        }

        private static ISet<string> Eligible(params string[] scopes)
            => new HashSet<string>(scopes, StringComparer.Ordinal);
    }
}
