using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TreeContext = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test.ClientAppMachineAccessTreeTest.TreeTestContext;
using TreeModels = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// Nagelt fest, dass <c>CurrentTenantTree</c> je Kontext einmal gelesen wird und trotzdem stimmt.
    /// </summary>
    /// <remarks>
    /// Die globalen Filter reichen den Baum als Parameter weiter, und EF zaehlt eine Parameter-Sammlung bei jeder
    /// Ausfuehrung neu auf. Solange die Eigenschaft eine Datenbank-Abfrage war, kostete das je gefilterter Abfrage
    /// eine zusaetzliche Rundreise (im MiniStore-Log 115 je Seitenaufruf). Jetzt merkt sich der Kontext die Ids; die
    /// Tests pruefen, dass der Merker greift, nach SaveChanges neu liest und die Reihenfolge (naechster zuerst) haelt.
    /// Laeuft auf dem Baum-Testkontext aus <see cref="ClientAppMachineAccessTreeTest"/>, dessen Sicht "den Baum
    /// hinauf" im Arbeitsspeicher aus der Mandantentabelle nachgebaut wird.
    /// </remarks>
    [TestClass]
    public class CurrentTenantTreeBufferTest
    {
        private string dbName;

        [TestInitialize]
        public void Setup() => dbName = $"currentTenantTree_{Guid.NewGuid():N}";

        [TestMethod]
        public void ReturnsTheTenantAndItsParents_NearestFirst()
        {
            var (root, mid, leaf) = SeedChain();
            using var ctx = Context("leaf", includeParentTree: true);

            CollectionAssert.AreEqual(new[] { leaf, mid, root }, ctx.CurrentTenantTree.ToArray());
        }

        [TestMethod]
        public void WithoutParentTree_IsEmpty()
        {
            SeedChain();
            using var ctx = Context("leaf", includeParentTree: false);

            Assert.AreEqual(0, ctx.CurrentTenantTree.Count());
        }

        [TestMethod]
        public void IsReadOncePerContext_AndAgainAfterSaveChanges()
        {
            var (root, mid, leaf) = SeedChain();
            using var ctx = Context("leaf", includeParentTree: true);
            CollectionAssert.AreEqual(new[] { leaf, mid, root }, ctx.CurrentTenantTree.ToArray());

            // Ein anderer Kontext haengt "leaf" direkt unter "root".
            using (var other = Context("leaf", includeParentTree: true))
            {
                other.Tenants.Single(n => n.TenantId == leaf).ParentTenantId = root;
                other.SaveChanges();
            }

            CollectionAssert.AreEqual(new[] { leaf, mid, root }, ctx.CurrentTenantTree.ToArray(),
                "innerhalb desselben Kontexts gilt der gemerkte Baum - sonst waere es wieder eine Rundreise je Abfrage.");

            ctx.SaveChanges();
            CollectionAssert.AreEqual(new[] { leaf, root }, ctx.CurrentTenantTree.ToArray(),
                "nach SaveChanges muss der Baum neu gelesen werden; ein Schreibvorgang kann ihn veraendert haben.");
        }

        [TestMethod]
        public async Task IsReadAgainAfterSaveChangesAsync()
        {
            var (root, _, leaf) = SeedChain();
            using var ctx = Context("leaf", includeParentTree: true);
            _ = ctx.CurrentTenantTree.ToArray();

            using (var other = Context("leaf", includeParentTree: true))
            {
                other.Tenants.Single(n => n.TenantId == leaf).ParentTenantId = root;
                await other.SaveChangesAsync();
            }

            await ctx.SaveChangesAsync();
            CollectionAssert.AreEqual(new[] { leaf, root }, ctx.CurrentTenantTree.ToArray());
        }

        [TestMethod]
        public void AnotherTenant_ReadsItsOwnTree()
        {
            var (root, mid, _) = SeedChain();
            using var ctx = Context("mid", includeParentTree: true);

            CollectionAssert.AreEqual(new[] { mid, root }, ctx.CurrentTenantTree.ToArray());
        }

        // --- Testgeruest ----------------------------------------------------------------------

        private (int Root, int Mid, int Leaf) SeedChain()
        {
            using var ctx = Context("root", includeParentTree: false);
            var root = Tenant(ctx, "root", null);
            var mid = Tenant(ctx, "mid", root);
            var leaf = Tenant(ctx, "leaf", mid);
            return (root, mid, leaf);
        }

        private static int Tenant(TreeContext ctx, string name, int? parent)
        {
            var tenant = new TreeModels.HierarchyTenant
            {
                TenantName = name, DisplayName = name, TenantNameLower = name.ToLowerInvariant(), ParentTenantId = parent
            };
            ctx.Tenants.Add(tenant);
            ctx.SaveChanges();
            return tenant.TenantId;
        }

        private TreeContext Context(string tenant, bool includeParentTree)
        {
            var options = new DbContextOptionsBuilder<TreeContext>().UseInMemoryDatabase(dbName).Options;
            var ctx = new TreeContext(new FixedScope(tenant), new AnonymousUser(), null,
                NullLogger<TreeContext>.Instance,
                Microsoft.Extensions.Options.Options.Create(new DbContextModelBuilderOptions<TreeContext>()),
                options);

            // Der Setter von IncludeParentTree prueft die Rechte des Benutzers ueber dessen Services; die hat dieser
            // Kontext nicht. Fuer den Merker zaehlt nur, dass der Schalter an ist.
            typeof(AspNetTreeSecurityContext<TreeContext>)
                .GetField("includeParentTree", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(ctx, includeParentTree);
            return ctx;
        }

        private sealed class FixedScope : PermissionScopeBase
        {
            private readonly string scope;

            public FixedScope(string scope) => this.scope = scope;

            protected override string GetPermissionScopePrefix() => scope;

            protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
                => throw new NotSupportedException();
        }

        private sealed class AnonymousUser : IContextUserProvider
        {
            public ClaimsPrincipal User { get; } = new ClaimsPrincipal(new ClaimsIdentity());

            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();

            public string RequestPath => "/";

            public IServiceProvider Services => null;
        }
    }
}
