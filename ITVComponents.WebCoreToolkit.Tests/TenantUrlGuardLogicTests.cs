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

        [TestMethod]
        public void Culture_Prefixed_Base_Passes_A_Target_That_Already_Matches()
        {
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/c/de-CH/T001/",
                target: new Uri("https://app/c/de-CH/T001/users"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001", "ADM"));

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Escape_Is_Rewritten_Under_Culture_And_Tenant()
        {
            // A tenant-unaware NavigateTo("/users") has to come back under BOTH prefixes.
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/c/de-CH/T001/",
                target: new Uri("https://app/users?id=42#top"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001", "ADM"));

            Assert.AreEqual("/c/de-CH/T001/users?id=42#top", result);
        }

        [TestMethod]
        public void Cross_Tenant_Switch_Keeps_The_Language()
        {
            // Without the tenant prefix this is a legitimate switch and passes through - but passing it
            // through unchanged would drop the language, and the visitor would land in the tenant of
            // their choice speaking a different one.
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/c/de-CH/T001/",
                target: new Uri("https://app/ADM/counter"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001", "ADM"));

            Assert.AreEqual("/c/de-CH/ADM/counter", result);
        }

        [TestMethod]
        public void Deliberate_Language_Switch_Is_Left_Alone()
        {
            // The target brings its own language: that IS the navigation. Rewriting it under the current
            // one would make switching the language impossible.
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/c/de-CH/T001/",
                target: new Uri("https://app/c/fr/T001/users"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001", "ADM"));

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Language_From_The_Target_Wins_While_The_Tenant_Comes_From_The_Base()
        {
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/c/de-CH/T001/",
                target: new Uri("https://app/c/fr/users"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001", "ADM"));

            Assert.AreEqual("/c/fr/T001/users", result);
        }

        [TestMethod]
        public void Auth_Excluded_Path_Keeps_The_Language_But_Not_The_Tenant()
        {
            // The login page is deliberately tenant-neutral - it is not language-neutral.
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/c/de-CH/T001/",
                target: new Uri("https://app/Identity/Account/Login"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001"));

            Assert.AreEqual("/c/de-CH/Identity/Account/Login", result);
        }

        [TestMethod]
        public void Blazor_Internals_Do_Not_Even_Get_A_Language()
        {
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/c/de-CH/T001/",
                target: new Uri("https://app/_blazor/initializers"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible("T001"));

            Assert.IsNull(result);
        }

        [TestMethod]
        public void Tenantless_User_Still_Keeps_The_Language()
        {
            // A user who is a member of no tenant has a base path of "/c/de-CH/" - there is no tenant to
            // enforce, but there is still a language to preserve.
            var result = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath: "/c/de-CH/",
                target: new Uri("https://app/home"),
                authExclusions: AuthExclusions,
                eligibleScopes: Eligible());

            Assert.AreEqual("/c/de-CH/home", result);
        }

        private static ISet<string> Eligible(params string[] scopes)
            => new HashSet<string>(scopes, StringComparer.Ordinal);
    }
}
