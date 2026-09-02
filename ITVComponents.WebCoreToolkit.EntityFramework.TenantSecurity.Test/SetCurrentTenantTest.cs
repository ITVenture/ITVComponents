using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DataAnnotation;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DIIntegration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Interceptors;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FlatBinderTenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.BinderModels.BinderTenant;
using TreeBinderTenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.BinderModels.BinderTenant;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// Prueft das Eintragen des aktuellen Mandanten beim Speichern - und vor allem, dass es fuer die
    /// <b>flache wie die hierarchische</b> Mandanten-Auspraegung gleichermassen funktioniert.
    /// </summary>
    /// <remarks>
    /// Die Kontexte hier sind absichtlich winzig und von Hand gebaut: geprueft wird der Mechanismus
    /// (Nachschlagen ueber eine generische Entitaet, Eintragen in ausgezeichnete Eigenschaften), nicht
    /// das Sicherheitsmodell. Ein echter Sicherheits-Kontext braeuchte den halben Baukasten und wuerde
    /// verdecken, worum es geht.
    /// </remarks>
    [TestClass]
    public class SetCurrentTenantTest
    {
        private SqliteConnection connection;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        private DbContextOptions<T> Options<T>(SetTenantType tenantType, IServiceProvider services)
            where T : DbContext
        {
            var builder = new DbContextOptionsBuilder<T>().UseSqlite(connection);
            builder.AddInterceptors(new SetCurrentTenantInterceptor(services, tenantType));
            return builder.Options;
        }

        private static IServiceProvider Scope(string scopeName)
            => new ServiceCollection()
                .AddSingleton<IPermissionScope>(new FixedScope(scopeName))
                .BuildServiceProvider();

        // --- Die Nachschlage-Funktion ---------------------------------------------------------

        [TestMethod]
        public void ItFindsTheTenantInTheFlatModel()
        {
            using var ctx = new FlatContext(new DbContextOptionsBuilder<FlatContext>()
                .UseSqlite(connection).Options);
            ctx.Database.EnsureCreated();
            ctx.Tenants.Add(new FlatBinderTenant { TenantName = "acme", DisplayName = "Acme" });
            ctx.SaveChanges();

            int id = TenantIdLookup.For<FlatBinderTenant>()(ctx, "acme");

            Assert.AreEqual(ctx.Tenants.Single().TenantId, id);
        }

        [TestMethod]
        public void ItFindsTheTenantInTheHierarchicalModel()
        {
            using var ctx = new TreeContext(new DbContextOptionsBuilder<TreeContext>()
                .UseSqlite(connection).Options);
            ctx.Database.EnsureCreated();
            var parent = new TreeBinderTenant { TenantName = "group", DisplayName = "Group" };
            ctx.Tenants.Add(parent);
            ctx.SaveChanges();
            ctx.Tenants.Add(new TreeBinderTenant
            {
                TenantName = "acme", DisplayName = "Acme", ParentTenantId = parent.TenantId
            });
            ctx.SaveChanges();

            int id = TenantIdLookup.For<TreeBinderTenant>()(ctx, "acme");

            Assert.AreEqual(ctx.Tenants.Single(t => t.TenantName == "acme").TenantId, id,
                "the hierarchical tenant differs only by its parent link - the lookup must not care.");
        }

        [TestMethod]
        public void AnUnknownTenantName_SaysSo()
        {
            using var ctx = new FlatContext(new DbContextOptionsBuilder<FlatContext>()
                .UseSqlite(connection).Options);
            ctx.Database.EnsureCreated();

            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => TenantIdLookup.For<FlatBinderTenant>()(ctx, "ghost"));

            StringAssert.Contains(ex.Message, "ghost",
                "writing an invented id into the foreign keys would put the row at a tenant that does " +
                "not exist - and nobody would see why.");
        }

        [TestMethod]
        public void TheWrongFlavourForThisContext_SaysWhich()
        {
            using var ctx = new FlatContext(new DbContextOptionsBuilder<FlatContext>()
                .UseSqlite(connection).Options);
            ctx.Database.EnsureCreated();

            // Die haeufigste Fehlbedienung: hierarchische Auspraegung an einem flachen Kontext.
            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => TenantIdLookup.For<TreeBinderTenant>()(ctx, "acme"));

            StringAssert.Contains(ex.Message, "not part of the model");
            StringAssert.Contains(ex.Message, "flat or hierarchical",
                "the EF message names the type but not the cause - that is what costs the time.");
        }

        // --- Der Interceptor ------------------------------------------------------------------

        [TestMethod]
        public void TheCurrentTenantIsStampedOnNewRows_Flat()
        {
            IServiceProvider services = Scope("acme");
            using var ctx = new FlatContext(Options<FlatContext>(SetTenantType.BinderTenant, services));
            ctx.Database.EnsureCreated();
            ctx.Tenants.Add(new FlatBinderTenant { TenantName = "acme", DisplayName = "Acme" });
            ctx.SaveChanges();

            ctx.Documents.Add(new Document { Subject = "Invoice" });
            ctx.SaveChanges();

            Assert.AreEqual(ctx.Tenants.Single().TenantId, ctx.Documents.Single().TenantId);
        }

        [TestMethod]
        public void TheCurrentTenantIsStampedOnNewRows_Hierarchical()
        {
            IServiceProvider services = Scope("acme");
            using var ctx = new TreeContext(Options<TreeContext>(SetTenantType.HierarchyBinderTenant,
                services));
            ctx.Database.EnsureCreated();
            ctx.Tenants.Add(new TreeBinderTenant { TenantName = "acme", DisplayName = "Acme" });
            ctx.SaveChanges();

            ctx.Documents.Add(new Document { Subject = "Invoice" });
            ctx.SaveChanges();

            Assert.AreEqual(ctx.Tenants.Single().TenantId, ctx.Documents.Single().TenantId,
                "that is the whole point of this issue: the same mechanism on the hierarchical model.");
        }

        [TestMethod]
        public void AnExplicitTenantIsLeftAlone()
        {
            IServiceProvider services = Scope("acme");
            using var ctx = new FlatContext(Options<FlatContext>(SetTenantType.BinderTenant, services));
            ctx.Database.EnsureCreated();
            ctx.Tenants.Add(new FlatBinderTenant { TenantName = "acme" });
            ctx.Tenants.Add(new FlatBinderTenant { TenantName = "other" });
            ctx.SaveChanges();
            int otherId = ctx.Tenants.Single(t => t.TenantName == "other").TenantId;

            ctx.Documents.Add(new Document { Subject = "Invoice", TenantId = otherId });
            ctx.SaveChanges();

            Assert.AreEqual(otherId, ctx.Documents.Single().TenantId,
                "the interceptor fills in what is empty - it does not overrule a deliberate assignment.");
        }

        [TestMethod]
        public void WithoutAnythingToStamp_TheTenantIsNotEvenLookedUp()
        {
            // Scope-Name, den es NICHT gibt: waere das Nachschlagen nicht bedarfsgetrieben, floege hier
            // eine Ausnahme - und jeder Speichervorgang kostete eine Abfrage, die niemand braucht.
            IServiceProvider services = Scope("ghost");
            using var ctx = new FlatContext(Options<FlatContext>(SetTenantType.BinderTenant, services));
            ctx.Database.EnsureCreated();

            ctx.Notes.Add(new Note { Text = "no tenant column here" });
            ctx.SaveChanges();

            Assert.AreEqual(1, ctx.Notes.Count());
        }

        // --- Testgeruest ----------------------------------------------------------------------

        /// <summary>Ein Datensatz, der seinen Mandanten vom Interceptor bekommt.</summary>
        public class Document
        {
            public int Id { get; set; }

            public string Subject { get; set; }

            [AssignTenant]
            public int TenantId { get; set; }
        }

        /// <summary>Ein Datensatz ganz ohne Mandanten-Bezug.</summary>
        public class Note
        {
            public int Id { get; set; }

            public string Text { get; set; }
        }

        public class FlatContext : DbContext
        {
            public FlatContext(DbContextOptions<FlatContext> options) : base(options) { }

            public DbSet<FlatBinderTenant> Tenants { get; set; }

            public DbSet<Document> Documents { get; set; }

            public DbSet<Note> Notes { get; set; }
        }

        public class TreeContext : DbContext
        {
            public TreeContext(DbContextOptions<TreeContext> options) : base(options) { }

            public DbSet<TreeBinderTenant> Tenants { get; set; }

            public DbSet<Document> Documents { get; set; }

            public DbSet<Note> Notes { get; set; }
        }

        /// <summary>Ein Berechtigungs-Scope mit festem Namen - mehr braucht der Interceptor nicht.</summary>
        private sealed class FixedScope : PermissionScopeBase
        {
            private readonly string scopeName;

            public FixedScope(string scopeName) => this.scopeName = scopeName;

            protected override string GetPermissionScopePrefix() => scopeName;

            protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
                => throw new NotSupportedException("The test scope is fixed.");
        }
    }
}
