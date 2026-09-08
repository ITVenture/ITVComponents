using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelFeature = ITVComponents.WebCoreToolkit.Models.Feature;
using ModelPermission = ITVComponents.WebCoreToolkit.Models.Permission;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Covers what happens when the scope handed to a <see cref="UserScope"/> is not one of its eligible
    /// scopes. Every lookup here used to be a bare <c>First(...)</c>, so the answer was always the same
    /// exception - <em>"Sequence contains no matching element"</em> - which names neither the scope that was
    /// asked for nor the ones on offer, and carries a stack pointing at whichever component happened to ask
    /// first rather than at the resolution that went wrong.
    /// </summary>
    [TestClass]
    public class UserScopeLookupTests
    {
        [TestMethod]
        public void Storing_Permissions_For_An_Ineligible_Scope_Names_The_Scope_And_The_Alternatives()
        {
            var token = TokenFor("TenantA", "TenantB");

            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => token.UpdateScopePermissions("ADM", KnownPermissions("ADM.Read"), new[] { "ADM.Read" }));

            StringAssert.Contains(ex.Message, "ADM");
            StringAssert.Contains(ex.Message, "TenantA");
            StringAssert.Contains(ex.Message, "TenantB");
        }

        [TestMethod]
        public void Marking_An_Ineligible_Scope_As_Refreshed_Says_So()
        {
            var token = TokenFor("TenantA");

            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => token.SetScopeRefreshed("ADM"));

            StringAssert.Contains(ex.Message, "ADM");
            StringAssert.Contains(ex.Message, "TenantA");
        }

        [TestMethod]
        public void An_Empty_Eligible_Set_Is_Spelled_Out_Rather_Than_Left_Blank()
        {
            // "(none)" beats an empty pair of brackets: it says the set was empty, not that the message
            // lost its arguments somewhere.
            var token = TokenFor();

            var ex = Assert.ThrowsExactly<InvalidOperationException>(() => token.SetScopeRefreshed("ADM"));

            StringAssert.Contains(ex.Message, "none");
        }

        [TestMethod]
        public void Reading_Permissions_Of_An_Ineligible_Scope_Yields_Nothing_Instead_Of_Throwing()
        {
            // The read paths are asked speculatively by components that only want to know whether anything
            // is there. They answer "nothing" for every other miss, so they answer "nothing" here too - the
            // log entry carries the reason.
            var token = TokenFor("TenantA");
            token.UpdateScopePermissions("TenantA", KnownPermissions("TenantA.Read"), new[] { "TenantA.Read" });
            token.UpdateScopeFeatures("TenantA", new[] { new ModelFeature { FeatureName = "SomeFeature", Enabled = true } });

            Assert.IsNull(token.GetPermissionsOf("ADM"));
            Assert.IsNull(token.GetFeaturesOf("ADM"));
        }

        [TestMethod]
        public void A_Scope_That_Differs_Only_In_Case_Is_The_Same_Scope()
        {
            var token = TokenFor("TenantA");
            token.UpdateScopePermissions("tenanta", KnownPermissions("TenantA.Read"), new[] { "TenantA.Read" });

            CollectionAssert.AreEqual(new[] { "TenantA.Read" }, token.GetPermissionsOf("TENANTA"));
        }

        private static UserScope TokenFor(params string[] eligibleScopes)
            => new UserScope
            {
                EligibleScopes = eligibleScopes
                    .Select(n => new ScopeInfo { ScopeName = n, ScopeDisplayName = n })
                    .ToArray()
            };

        private static ModelPermission[] KnownPermissions(params string[] names)
            => names.Select(n => new ModelPermission { PermissionName = n }).ToArray();
    }
}
