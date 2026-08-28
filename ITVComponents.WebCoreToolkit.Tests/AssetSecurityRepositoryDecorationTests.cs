using System.Linq;
using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ToolkitClaimTypes = ITVComponents.WebCoreToolkit.ClaimTypes;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Deckt ab, wann eine Anfrage die Rechte ihrer Freigabe bekommt - genauer: wann
    /// <see cref="ServiceProviderExtensions.GetAssetSecurityRepository"/> den Mandanten der Freigabe als
    /// zulaessigen Bereich liefert. Daran haengt mehr als die Rechte: ohne diesen Bereich strippt
    /// <c>UseTenantPathPrefix()</c> das Mandantensegment nicht, das Routing findet keinen Endpunkt, und der
    /// Freigabe-Link endet in einem 404, dem man nichts von einer Freigabe ansieht (BUG-PRE197).
    /// </summary>
    [TestClass]
    public class AssetSecurityRepositoryDecorationTests
    {
        [TestMethod]
        public void Asset_Scope_Applies_When_The_Template_Grants_Nothing()
        {
            // Der Fall, der lange nicht ging: eine Vorlage, die weder Features noch Rechte gewaehrt - also
            // die schlichteste Freigabe ueberhaupt, auf eine Seite, die der Empfaenger ohnehin sehen darf.
            // Der Mandant der Freigabe gilt trotzdem, denn er ist das, was die Freigabe ausmacht.
            var scopes = ResolveScopes(new Claim(ToolkitClaimTypes.FixedUserScope, "xyz"));

            CollectionAssert.AreEqual(new[] { "xyz" }, scopes);
        }

        [TestMethod]
        public void Asset_Scope_Applies_With_Permissions_But_Without_Features()
        {
            // Die haeufigste Vorlage: sie gewaehrt ein Recht und kein Feature.
            var scopes = ResolveScopes(
                new Claim(ToolkitClaimTypes.FixedUserScope, "xyz"),
                new Claim(ToolkitClaimTypes.FixedAssetPermission, "Customers.Read"));

            CollectionAssert.AreEqual(new[] { "xyz" }, scopes);
        }

        [TestMethod]
        public void Without_An_Asset_The_Users_Own_Scopes_Remain()
        {
            // Ohne Freigabe darf der Dekorator nicht dazwischenfunken: es gelten die Mandanten des Benutzers.
            var scopes = ResolveScopes();

            CollectionAssert.AreEqual(new[] { "abc" }, scopes);
        }

        /// <summary>
        /// Baut den Dekorator ueber einem Repository, das dem Benutzer den Mandanten "abc" zugesteht, und
        /// fragt ihn nach den zulaessigen Bereichen.
        /// </summary>
        /// <param name="assetClaims">die Claims, die eine geltende Freigabe hinterlaesst</param>
        /// <returns>die Namen der zulaessigen Bereiche</returns>
        private static string[] ResolveScopes(params Claim[] assetClaims)
        {
            var inner = new FakeSecurityRepository("abc");
            var context = TestSecurity.AuthenticatedContext(inner, null, assetClaims);
            var services = new FakeServiceProvider(inner, new FakeUserNameMapper()) { ContextUser = context };

            var repo = services.GetAssetSecurityRepository(inner);
            return repo.GetEligibleScopes(new[] { "tester" }, TestSecurity.AuthType)
                .Select(n => n.ScopeName)
                .ToArray();
        }
    }
}
