using System;
using System.Linq;
using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Prueft die tenant-abhaengige Filterung des <see cref="WorkflowContext"/> ueber den
    /// Plugin-/Laufzeit-Ctor: oeffentliche Definitionen (TenantId null) sind fuer jeden Tenant
    /// sichtbar, tenant-eigene nur im eigenen Tenant; Instanzen sind strikt tenant-gebunden und
    /// werden beim Anlegen mit dem aktiven Tenant gestempelt.
    /// </summary>
    [TestClass]
    public class WorkflowContextTenantTest
    {
        private SqliteConnection connection;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite(connection).Options;
            using var ctx = new WorkflowContext(options);
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup()
        {
            connection?.Dispose();
        }

        private WorkflowContext MakeContext(string tenant)
        {
            return new WorkflowContext(
                new SqliteTestOptionsLoader(connection),
                TestServices.ForTenant(tenant),
                useTenantFilter: true,
                new WorkflowFilterInitializer<WorkflowContext>());
        }

        /// <summary>Derselbe Kontext ueber den DI-Ctor (Tenant-Schalter als Options-Objekt).</summary>
        private WorkflowContext MakeOptionsContext(string tenant, WorkflowContextOptions options)
        {
            return new WorkflowContext(
                new SqliteTestOptionsLoader(connection),
                TestServices.ForTenant(tenant),
                options == null ? null : Options.Create(options),
                new WorkflowFilterInitializer<WorkflowContext>());
        }

        private EfWorkflowStore StoreFor(string tenant)
        {
            return new EfWorkflowStore(() => MakeContext(tenant));
        }

        /// <summary>
        /// Eine Definition. <paramref name="tenant"/> null heisst hier <b>oeffentlich</b> - und das muss
        /// seit der ausdruecklichen Entscheidung auch so gesagt werden: „kein Mandant gesetzt" allein
        /// laesst der Store auf den aktiven Mandanten fallen, damit der Editor nicht still oeffentliche
        /// Definitionen anlegt.
        /// </summary>
        private static WorkflowDefinition Definition(string id, string tenant)
        {
            return new WorkflowDefinition
            {
                Id = id, Version = 1, TenantId = tenant, IsPublic = tenant == null, Name = id
            };
        }

        [TestMethod]
        public void PublicDefinitionsVisibleToAll_TenantOwnedOnlyToOwner()
        {
            var acme = StoreFor("acme");
            var beta = StoreFor("beta");

            acme.SaveDefinition(Definition("pub", null));       // oeffentlich
            acme.SaveDefinition(Definition("acmeflow", "acme")); // tenant-eigen

            // acme sieht beides.
            Assert.IsNotNull(acme.GetDefinition("pub"), "owner should see the public definition");
            Assert.IsNotNull(acme.GetDefinition("acmeflow"), "owner should see its own definition");

            // beta sieht die oeffentliche, aber nicht die von acme.
            Assert.IsNotNull(beta.GetDefinition("pub"), "public definition must be visible to every tenant");
            Assert.IsNull(beta.GetDefinition("acmeflow"), "another tenant's definition must be hidden");
        }

        [TestMethod]
        public void InstancesAreStrictlyTenantScopedAndStamped()
        {
            var acme = StoreFor("acme");
            var beta = StoreFor("beta");

            acme.SaveDefinition(Definition("pub", null));

            var instance = new WorkflowInstance
            {
                Id = "i1",
                DefinitionKey = acme.GetDefinition("pub", 1).Key,
                DefinitionId = "pub",
                DefinitionVersion = 1
            };
            acme.SaveInstance(instance);

            // Beim Anlegen mit dem aktiven Tenant gestempelt.
            Assert.AreEqual("acme", instance.TenantId, "a new instance is stamped with the active tenant");

            // Nur acme sieht die Instanz.
            Assert.IsNotNull(acme.GetInstance("i1"), "owner sees its instance");
            Assert.IsNull(beta.GetInstance("i1"), "another tenant must not see the instance");
        }

        /// <summary>
        /// Der DI-Ctor leitet den Schalter aus <see cref="WorkflowContextOptions"/> auf den bool-Ctor um -
        /// ausdruecklich gesetzt wie ausgelassen. Ohne Options gilt der Standard <c>true</c>: der DI-Weg
        /// laeuft im Web, und dort ist "zu viel sehen" der teure Fehler.
        /// </summary>
        [TestMethod]
        public void OptionsCtorRoutesTenantFilterToBoolCtor()
        {
            var acme = StoreFor("acme");
            acme.SaveDefinition(Definition("acmeflow", "acme"));

            using (WorkflowContext ctx = MakeOptionsContext("beta",
                       new WorkflowContextOptions { UseTenantFilter = true }))
            {
                Assert.IsTrue(ctx.UseTenantFilter, "the switch must arrive at the bool ctor");
                Assert.AreEqual("beta", ctx.CurrentTenant);
                Assert.IsNull(ctx.WorkflowDefinitions.FirstOrDefault(d => d.Id == "acmeflow"),
                    "another tenant's definition must be hidden");
            }

            using (WorkflowContext ctx = MakeOptionsContext("beta", null))
            {
                Assert.IsTrue(ctx.UseTenantFilter, "missing options must default to the restrictive side");
            }

            using (WorkflowContext ctx = MakeOptionsContext("beta",
                       new WorkflowContextOptions { UseTenantFilter = false }))
            {
                // Ausgeschaltet heisst NICHT filterfrei: der Filter bleibt im Modell und wertet den
                // aktiven Mandanten als null aus - sichtbar ist dann nur noch, was niemandem gehoert.
                Assert.IsFalse(ctx.UseTenantFilter);
                Assert.IsNull(ctx.CurrentTenant);
            }
        }
    }
}
