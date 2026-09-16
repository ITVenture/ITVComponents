using System;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// Nagelt die EINE Regel fest, nach der ein Feature fuer einen Mandanten gilt:
    /// <c>Features.Enabled</c> heisst "gilt fuer ALLE", eine <c>TenantFeatureActivation</c> ist der
    /// zweite, mandantenbezogene Weg zum selben Ja - verknuepft mit ODER, nie mit UND.
    /// </summary>
    /// <remarks>
    /// Der Anlass ist ein realer Fehler: das Zahlungs-Tor verlangte beides zugleich und lehnte damit
    /// genau die Konfiguration ab, die fuer ein je Mandant VERKAUFTES Modul richtig ist
    /// (<c>Enabled = false</c> + Aktivierung je Kaeufer) - stumm, weil fail-closed die Absicht ist.
    /// <para>
    /// Die Kontexte sind absichtlich winzig und von Hand gebaut: geprueft wird die Regel, nicht das
    /// Sicherheitsmodell. Flach UND hierarchisch, weil die Regel in beiden Auspraegungen dieselbe sein
    /// muss und genau dort auseinanderlaufen wuerde.
    /// </para>
    /// </remarks>
    [TestClass]
    public class FeatureActivationTest
    {
        private static readonly DateTime Now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private SqliteConnection connection;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        // --- Die Regel, flach -----------------------------------------------------------------

        [TestMethod]
        public async Task AFeatureSoldPerTenant_IsOnForTheBuyer()
        {
            // Das Muster fuer ein verkauftes Modul: Katalog-Zeile AUS, Aktivierung je Mandant.
            using var ctx = FlatContext();
            var feature = Catalogue(ctx, "StripePayments", enabledForEveryone: false);
            int buyer = FlatTenant(ctx, "acme");
            Activate(ctx, feature, buyer);

            var state = await ctx.GetFeatureStateForTenantAsync<Tenant, FlatTenantFeatureActivation>(
                "StripePayments", buyer, Now);

            Assert.AreEqual(FeatureActivationState.ActivatedForTenant, state);
            Assert.IsTrue(state.IsEnabled(),
                "genau diese Konfiguration ist die richtige fuer ein je Mandant verkauftes Modul - " +
                "verlangt jemand Enabled UND Aktivierung, laesst sich so ein Modul nie einschalten.");
        }

        [TestMethod]
        public async Task AFeatureSoldPerTenant_IsOffForEveryoneElse()
        {
            using var ctx = FlatContext();
            var feature = Catalogue(ctx, "StripePayments", enabledForEveryone: false);
            int buyer = FlatTenant(ctx, "acme");
            int other = FlatTenant(ctx, "other");
            Activate(ctx, feature, buyer);

            var state = await ctx.GetFeatureStateForTenantAsync<Tenant, FlatTenantFeatureActivation>(
                "StripePayments", other, Now);

            Assert.AreEqual(FeatureActivationState.NotActivatedForTenant, state);
            Assert.IsFalse(state.IsEnabled(), "die Aktivierung eines fremden Mandanten zaehlt nicht.");
        }

        [TestMethod]
        public async Task AGloballyEnabledFeature_IsOnWithoutAnyActivation()
        {
            using var ctx = FlatContext();
            Catalogue(ctx, "ITVAdminViews", enabledForEveryone: true);
            int tenant = FlatTenant(ctx, "acme");

            var state = await ctx.GetFeatureStateForTenantAsync<Tenant, FlatTenantFeatureActivation>(
                "ITVAdminViews", tenant, Now);

            Assert.AreEqual(FeatureActivationState.EnabledForEveryone, state);
            Assert.IsTrue(state.IsEnabled(),
                "Enabled auf der Zeile heisst 'gilt fuer alle' - nicht 'Hauptschalter an'.");
        }

        [TestMethod]
        public async Task WithoutAnyRoadToAYes_ItIsOff()
        {
            using var ctx = FlatContext();
            Catalogue(ctx, "StripePayments", enabledForEveryone: false);
            int tenant = FlatTenant(ctx, "acme");

            var state = await ctx.GetFeatureStateForTenantAsync<Tenant, FlatTenantFeatureActivation>(
                "StripePayments", tenant, Now);

            Assert.AreEqual(FeatureActivationState.NotActivatedForTenant, state);
            Assert.IsFalse(state.IsEnabled(), "das ist 'Modul aus' - fail-closed bleibt fail-closed.");
        }

        [TestMethod]
        public async Task AFeatureThatDoesNotExist_IsToldApartFromOneNobodyBought()
        {
            using var ctx = FlatContext();
            int tenant = FlatTenant(ctx, "acme");

            var state = await ctx.GetFeatureStateForTenantAsync<Tenant, FlatTenantFeatureActivation>(
                "StripePayments", tenant, Now);

            Assert.AreEqual(FeatureActivationState.Unknown, state,
                "eine fehlende Katalog-Zeile ist ein Auslieferungsfehler, eine fehlende Aktivierung eine " +
                "Geschaefts-Tatsache - von aussen sehen beide gleich aus, und das hat schon Zeit gekostet.");
            Assert.IsFalse(state.IsEnabled());
        }

        // --- Das Zeitfenster ------------------------------------------------------------------

        [TestMethod]
        public async Task AnExpiredActivation_DoesNotCount()
        {
            using var ctx = FlatContext();
            var feature = Catalogue(ctx, "StripePayments", enabledForEveryone: false);
            int tenant = FlatTenant(ctx, "acme");
            Activate(ctx, feature, tenant, end: Now.AddDays(-1));

            var state = await ctx.GetFeatureStateForTenantAsync<Tenant, FlatTenantFeatureActivation>(
                "StripePayments", tenant, Now);

            Assert.AreEqual(FeatureActivationState.NotActivatedForTenant, state);
        }

        [TestMethod]
        public async Task AnActivationThatHasNotStartedYet_DoesNotCount()
        {
            using var ctx = FlatContext();
            var feature = Catalogue(ctx, "StripePayments", enabledForEveryone: false);
            int tenant = FlatTenant(ctx, "acme");
            Activate(ctx, feature, tenant, start: Now.AddDays(1));

            var state = await ctx.GetFeatureStateForTenantAsync<Tenant, FlatTenantFeatureActivation>(
                "StripePayments", tenant, Now);

            Assert.AreEqual(FeatureActivationState.NotActivatedForTenant, state);
        }

        [TestMethod]
        public async Task AnOpenEndedActivation_Counts()
        {
            using var ctx = FlatContext();
            var feature = Catalogue(ctx, "StripePayments", enabledForEveryone: false);
            int tenant = FlatTenant(ctx, "acme");
            Activate(ctx, feature, tenant, start: null, end: null);

            var state = await ctx.GetFeatureStateForTenantAsync<Tenant, FlatTenantFeatureActivation>(
                "StripePayments", tenant, Now);

            Assert.AreEqual(FeatureActivationState.ActivatedForTenant, state,
                "ohne Fenster gilt die Aktivierung unbegrenzt - so legt der Provisionierer sie an.");
        }

        // --- Der Name -------------------------------------------------------------------------

        [TestMethod]
        public async Task TheFeatureNameIsMatchedCaseInsensitively()
        {
            using var ctx = FlatContext();
            var feature = Catalogue(ctx, "stripepayments", enabledForEveryone: false);
            int tenant = FlatTenant(ctx, "acme");
            Activate(ctx, feature, tenant);

            var state = await ctx.GetFeatureStateForTenantAsync<Tenant, FlatTenantFeatureActivation>(
                "StripePayments", tenant, Now);

            Assert.AreEqual(FeatureActivationState.ActivatedForTenant, state,
                "ein roher Vergleich ueberlebt SQL Server mit seiner unempfindlichen Sortierung und " +
                "antwortet dann auf PostgreSQL stumm 'kein solches Feature'.");
        }

        // --- Dieselbe Regel im Baum -----------------------------------------------------------

        [TestMethod]
        public async Task TheSameRuleHolds_InTheHierarchicalModel()
        {
            using var ctx = TreeContext();
            var feature = Catalogue(ctx, "StripePayments", enabledForEveryone: false);
            int buyer = TreeTenant(ctx, "acme");
            ActivateTree(ctx, feature, buyer);

            var state = await ctx
                .GetFeatureStateForTenantAsync<HierarchyTenant, HierarchyTenantFeatureActivation>(
                    "StripePayments", buyer, Now);

            Assert.AreEqual(FeatureActivationState.ActivatedForTenant, state,
                "die Regel darf zwischen flacher und hierarchischer Auspraegung nicht auseinanderlaufen.");
        }

        [TestMethod]
        public async Task AnActivation_IsNotInheritedByChildTenants()
        {
            using var ctx = TreeContext();
            var feature = Catalogue(ctx, "StripePayments", enabledForEveryone: false);
            int parent = TreeTenant(ctx, "group");
            int child = TreeTenant(ctx, "acme", parent);
            ActivateTree(ctx, feature, parent);

            var state = await ctx
                .GetFeatureStateForTenantAsync<HierarchyTenant, HierarchyTenantFeatureActivation>(
                    "StripePayments", child, Now);

            Assert.AreEqual(FeatureActivationState.NotActivatedForTenant, state,
                "Feature-Aktivierungen sind streng mandantenlokal: ihr globaler Filter ist die einzige " +
                "mandantengebundene Regel ohne Baum-Zweig. Wer das aendert, aendert es hier zuerst.");
        }

        // --- Testgeruest ----------------------------------------------------------------------

        private FlatFeatureContext FlatContext()
        {
            var ctx = new FlatFeatureContext(new DbContextOptionsBuilder<FlatFeatureContext>()
                .UseSqlite(connection).Options);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        private TreeFeatureContext TreeContext()
        {
            var ctx = new TreeFeatureContext(new DbContextOptionsBuilder<TreeFeatureContext>()
                .UseSqlite(connection).Options);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        private static Feature Catalogue(DbContext ctx, string name, bool enabledForEveryone)
        {
            var feature = new Feature { FeatureName = name, Enabled = enabledForEveryone };
            ctx.Add(feature);
            ctx.SaveChanges();
            return feature;
        }

        private static int FlatTenant(DbContext ctx, string name)
        {
            // TenantNameLower von Hand: die Spalte ist in der Datenbank BERECHNET, und weder SQLite noch
            // der InMemory-Provider rechnet sie aus - der Insert scheitert dort sonst an NOT NULL bzw. an
            // "Required properties are missing". Dieselbe Bewegung wie bei RoleNameUniqueness weiter unten.
            var tenant = new Tenant
            {
                TenantName = name, DisplayName = name, TenantNameLower = name.ToLowerInvariant()
            };
            ctx.Add(tenant);
            ctx.SaveChanges();
            return tenant.TenantId;
        }

        private static int TreeTenant(DbContext ctx, string name, int? parentTenantId = null)
        {
            // TenantNameLower von Hand: die Spalte ist in der Datenbank BERECHNET, und weder SQLite noch
            // der InMemory-Provider rechnet sie aus - der Insert scheitert dort sonst an NOT NULL bzw. an
            // "Required properties are missing". Dieselbe Bewegung wie bei RoleNameUniqueness weiter unten.
            var tenant = new HierarchyTenant
            {
                TenantName = name, DisplayName = name, ParentTenantId = parentTenantId,
                TenantNameLower = name.ToLowerInvariant()
            };
            ctx.Add(tenant);
            ctx.SaveChanges();
            return tenant.TenantId;
        }

        private static void Activate(DbContext ctx, Feature feature, int tenantId,
            DateTime? start = null, DateTime? end = null)
        {
            ctx.Add(new FlatTenantFeatureActivation
            {
                FeatureId = feature.FeatureId, TenantId = tenantId,
                ActivationStart = start, ActivationEnd = end
            });
            ctx.SaveChanges();
        }

        private static void ActivateTree(DbContext ctx, Feature feature, int tenantId,
            DateTime? start = null, DateTime? end = null)
        {
            ctx.Add(new HierarchyTenantFeatureActivation
            {
                FeatureId = feature.FeatureId, TenantId = tenantId,
                ActivationStart = start, ActivationEnd = end
            });
            ctx.SaveChanges();
        }

        /// <summary>Der Katalog und die flachen Aktivierungen - mehr braucht die Regel nicht.</summary>
        public class FlatFeatureContext : DbContext
        {
            public FlatFeatureContext(DbContextOptions<FlatFeatureContext> options) : base(options) { }

            public DbSet<Feature> Features { get; set; }

            public DbSet<Tenant> Tenants { get; set; }

            public DbSet<FlatTenantFeatureActivation> Activations { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                // Die Mandanten-Art zoege den halben Baukasten mit herein und hat mit Features nichts zu tun.
                modelBuilder.Entity<Tenant>().Ignore(t => t.TenantType).Ignore(t => t.TenantTypeId);
                // TenantNameLower ist in der echten Datenbank BERECHNET; EF schickt eine solche Spalte
                // beim Insert gar nicht erst mit. SQLite kennt die Berechnung aber nicht - die Spalte
                // bleibt leer und die NOT-NULL-Bedingung greift. Hier also als gewoehnliche Spalte
                // fuehren, die der Testhelfer selbst fuellt.
                modelBuilder.Entity<Tenant>().Property(t => t.TenantNameLower).ValueGeneratedNever();
            }
        }

        /// <summary>Dasselbe hierarchisch - der Mandant unterscheidet sich nur durch seinen Elternverweis.</summary>
        public class TreeFeatureContext : DbContext
        {
            public TreeFeatureContext(DbContextOptions<TreeFeatureContext> options) : base(options) { }

            public DbSet<Feature> Features { get; set; }

            public DbSet<HierarchyTenant> Tenants { get; set; }

            public DbSet<HierarchyTenantFeatureActivation> Activations { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<HierarchyTenant>().Ignore(t => t.TenantType).Ignore(t => t.TenantTypeId);
                // TenantNameLower ist in der echten Datenbank BERECHNET; EF schickt eine solche Spalte
                // beim Insert gar nicht erst mit. SQLite kennt die Berechnung aber nicht - die Spalte
                // bleibt leer und die NOT-NULL-Bedingung greift. Hier also als gewoehnliche Spalte
                // fuehren, die der Testhelfer selbst fuellt.
                modelBuilder.Entity<HierarchyTenant>().Property(t => t.TenantNameLower).ValueGeneratedNever();
            }
        }
    }
}
