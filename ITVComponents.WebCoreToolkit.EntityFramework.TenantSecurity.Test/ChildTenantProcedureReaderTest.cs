using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// <c>ChildTenantsWith</c> liest aus einer Prozedur, deren Spalten der Konsument mit einer Migration
    /// ausrollt. Das Ergebnis darf deshalb nicht am aktuellen Mapping von <see cref="HierarchyTenant"/>
    /// haengen (Issue ESAG ChildTenantsWith/TenantNameLower).
    /// </summary>
    /// <remarks>
    /// Die "Prozedur" ist hier ein SELECT mit genau den Spalten, die die ausgerollten Prozeduren liefern -
    /// also ohne <c>TenantNameLower</c>. Ausgefuehrt wird sie gegen SQLite, damit das echte EF-Mapping greift.
    /// </remarks>
    [TestClass]
    public class ChildTenantProcedureReaderTest
    {
        private SqliteConnection connection;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
        }

        [TestCleanup]
        public void Cleanup()
        {
            connection.Dispose();
        }

        [TestMethod]
        public void AProcedureResultWithoutTenantNameLower_StillYieldsCompleteTenants()
        {
            using var ctx = Context();
            Tenant(ctx, "Alpha");
            Tenant(ctx, "Beta");

            HierarchyTenant[] result = ChildTenantProcedureReader.ReadTenants(ctx, ctx.Tenants, ProcedureLike(ctx, "'Alpha', 'Beta'"));

            CollectionAssert.AreEquivalent(new[] { "Alpha", "Beta" }, result.Select(t => t.TenantName).ToArray());
            Assert.IsTrue(result.All(t => t.TenantNameLower == t.TenantName.ToLowerInvariant()),
                "Die Spalte, die die Prozedur nicht liefert, kommt aus dem Modell.");
        }

        /// <summary>
        /// Haelt fest, was vorher brach: dasselbe Ergebnis direkt als Mandant gemappt.
        /// </summary>
        [TestMethod]
        public void MappingTheProcedureDirectly_FailsOnTheMissingColumn()
        {
            using var ctx = Context();
            Tenant(ctx, "Alpha");

            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                // Ohne Filter: sonst packt EF das SQL in eine Unterabfrage, und SQLite scheitert schon an der
                // fehlenden Spalte. Ein "exec" laesst sich nicht einpacken - dort wirft EF beim Lesen, wie hier.
                () => ctx.Tenants.FromSqlRaw(ProcedureLike(ctx, "'Alpha'")).IgnoreQueryFilters().ToArray());
            StringAssert.Contains(ex.Message, "TenantNameLower");
        }

        [TestMethod]
        public void TheOrderOfTheProcedure_IsKept()
        {
            using var ctx = Context();
            Tenant(ctx, "Alpha");
            Tenant(ctx, "Beta");
            Tenant(ctx, "Gamma");

            HierarchyTenant[] result = ChildTenantProcedureReader.ReadTenants(ctx, ctx.Tenants,
                ProcedureLike(ctx, "'Alpha', 'Beta', 'Gamma'") + " ORDER BY TenantName DESC");

            CollectionAssert.AreEqual(new[] { "Gamma", "Beta", "Alpha" }, result.Select(t => t.TenantName).ToArray());
        }

        /// <summary>
        /// Welche Mandanten gelten, entscheidet die Prozedur - nicht ein zweites Mal der Filter des Kontexts.
        /// </summary>
        [TestMethod]
        public void TheQueryFilterOfTheContext_DoesNotHideWhatTheProcedureReturned()
        {
            using var ctx = Context();
            Tenant(ctx, "Alpha");
            ctx.HideAllTenants = true;

            Assert.AreEqual(0, ctx.Tenants.Count(), "Voraussetzung: der Filter verbirgt alles.");
            HierarchyTenant[] result = ChildTenantProcedureReader.ReadTenants(ctx, ctx.Tenants, ProcedureLike(ctx, "'Alpha'"));

            Assert.AreEqual(1, result.Length);
        }

        /// <summary>
        /// Die Sicherheits-Kontexte schalten selbst Lazy-Loading-Proxies ein. Dann muss auch der Typ, den
        /// <c>SqlQueryRaw</c> ad hoc mappt, proxyfaehig sein - sonst "Entity type 'ProcedureTenantRow' is sealed".
        /// </summary>
        [TestMethod]
        public void AContextWithLazyLoadingProxies_CanReadTheProcedure()
        {
            using (var seed = Context())
            {
                Tenant(seed, "Alpha");
            }

            using var ctx = new ReaderContext(new DbContextOptionsBuilder<ReaderContext>()
                .UseSqlite(connection).UseLazyLoadingProxies().Options);

            HierarchyTenant[] result = ChildTenantProcedureReader.ReadTenants(ctx, ctx.Tenants, ProcedureLike(ctx, "'Alpha'"));

            Assert.AreEqual("Alpha", result.Single().TenantName);
        }

        [TestMethod]
        public void AnEmptyProcedureResult_YieldsNoTenants()
        {
            using var ctx = Context();
            Tenant(ctx, "Alpha");

            HierarchyTenant[] result = ChildTenantProcedureReader.ReadTenants(ctx, ctx.Tenants, ProcedureLike(ctx, "'Nobody'"));

            Assert.AreEqual(0, result.Length);
        }

        // --- Testgeruest ----------------------------------------------------------------------

        /// <summary>Die Spalten der ausgerollten Prozeduren (ohne TenantTypeId, das der Testkontext nicht fuehrt).</summary>
        private static string ProcedureLike(DbContext ctx, string names)
        {
            var table = ctx.Model.FindEntityType(typeof(HierarchyTenant))!.GetTableName();
            return $"SELECT TenantId, ParentTenantId, TenantName, DisplayName, NULL AS TenantPassword, TimeZone, Culture, TenantDirty FROM \"{table}\" WHERE TenantName IN ({names})";
        }

        private ReaderContext Context()
        {
            var ctx = new ReaderContext(new DbContextOptionsBuilder<ReaderContext>().UseSqlite(connection).Options);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        private static void Tenant(DbContext ctx, string name)
        {
            // TenantNameLower von Hand: in der echten Datenbank BERECHNET, SQLite rechnet sie nicht aus.
            ctx.Add(new HierarchyTenant { TenantName = name, DisplayName = name, TenantNameLower = name.ToLowerInvariant() });
            ctx.SaveChanges();
        }

        public class ReaderContext : DbContext
        {
            public ReaderContext(DbContextOptions<ReaderContext> options) : base(options) { }

            public bool HideAllTenants { get; set; }

            public DbSet<HierarchyTenant> Tenants { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<HierarchyTenant>().Ignore(t => t.TenantType).Ignore(t => t.TenantTypeId);
                modelBuilder.Entity<HierarchyTenant>().Property(t => t.TenantNameLower).ValueGeneratedNever();
                modelBuilder.Entity<HierarchyTenant>().HasQueryFilter(t => !HideAllTenants);
            }
        }
    }
}
