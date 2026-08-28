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
        public void Asset_Scope_Applies_When_The_Principal_Arrives_After_The_Repository()
        {
            // BUG-PRE201: beim anonymen Zugriff wird dieses Repository zum ersten Mal INNERHALB der
            // Anmeldung aufgeloest - das Schema braucht es selbst, um das Zugangs-Token zu entschluesseln.
            // Zu diesem Zeitpunkt ist der Besucher noch anonym. Wurde die Entscheidung dort getroffen und
            // fuer die Anfrage festgehalten, blieb es fuer immer bei "keine Freigabe", obwohl der Prinzipal
            // einen Satz spaeter alles trug: kein zulaessiger Mandant, kein Strip, 404.
            var inner = new FakeSecurityRepository("abc");
            var context = new FakeContextUserProvider { User = new ClaimsPrincipal(new ClaimsIdentity()) };
            var services = new FakeServiceProvider(inner, new FakeUserNameMapper()) { ContextUser = context };

            var repo = services.GetAssetSecurityRepository(inner);

            // Erst jetzt entsteht der Prinzipal der Freigabe - so, wie es die Anmeldung mitten in der
            // Pipeline tut.
            context.User = AssetVisitor("xyz");

            var scopes = repo.GetEligibleScopes(new[] { "tester" }, TestSecurity.AuthType)
                .Select(n => n.ScopeName)
                .ToArray();

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
        /// Der Besucher, wie ihn eine geltende Freigabe hinterlaesst: derselbe Name, den der Mapper
        /// vergibt, plus der Mandant der Freigabe.
        /// </summary>
        /// <param name="scope">der Mandant der Freigabe</param>
        /// <returns>der Prinzipal</returns>
        private static ClaimsPrincipal AssetVisitor(string scope)
            => new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(System.Security.Claims.ClaimTypes.Name, "tester"),
                    new Claim(ToolkitClaimTypes.FixedUserScope, scope)
                },
                TestSecurity.AuthType));

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
