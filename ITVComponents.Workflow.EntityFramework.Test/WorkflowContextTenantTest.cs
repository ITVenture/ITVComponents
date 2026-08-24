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
        public void TenantFromTheWeb_IsNormalized_AndFoundInAnyWriting()
        {
            // "Acme" aus der Route und "acme" aus der Monitor-Ansicht sind DERSELBE Mandant. Frueher
            // schrieben die beiden Wege verschieden in dieselbe Spalte: der Start ueber die Monitor-
            // Ansicht klein (die Handler riefen ToLower()), der gewoehnliche Web-Weg in der
            // Schreibweise der Route. Unter SQL Server deckte die Collation das zu - unter PostgreSQL
            // saehe der Mandant seine eigenen Definitionen nicht mehr.
            //
            // Ohne TenantId am Objekt: dann stempelt der Store mit dem Mandanten des Kontexts, und
            // genau der ist der Weg, um den es hier geht.
            StoreFor("Acme").SaveDefinition(new WorkflowDefinition
            {
                Id = "wf", Version = 1, Name = "wf"
            });

            using (WorkflowContext ctx = MakeContext("Acme"))
            {
                Assert.AreEqual("acme",
                    ctx.WorkflowDefinitions.IgnoreQueryFilters().Single().TenantId,
                    "what comes in from the web must be normalized on the way in - otherwise the same "
                    + "tenant ends up in the column twice, in two writings.");
            }

            foreach (string writing in new[] { "Acme", "acme", "ACME" })
            {
                Assert.IsNotNull(StoreFor(writing).GetDefinition("wf"),
                    $"'{writing}' is the same tenant and must see it.");
            }

            Assert.IsNull(StoreFor("beta").GetDefinition("wf"),
                "and normalizing must not turn the filter into a sieve - beta still sees nothing.");
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
        /// <b>Die Form, in der es in Produktion laeuft:</b> EIN Options-Provider fuer alle Kontexte.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Der Provider ist ein Plugin und lebt einmal im Prozess; sein <c>DbContextModelBuilderOptions</c>
        /// samt <c>ExpressionFixVisitor</c> also auch. Der Visitor nimmt je Namen die <b>erste</b>
        /// Registrierung - und die ist eine <c>MemberExpression</c> ueber eine <b>Konstante</b>: den
        /// Kontext, der als erster durch <c>OnModelCreating</c> gelaufen ist. Dazu kommt, dass EF das
        /// Modell einmal baut und danach wiederverwendet. Auf dem Papier haengt der Filter damit an
        /// genau einer Kontext-Instanz.
        /// </para>
        /// <para>
        /// Dass es trotzdem stimmt, liegt an EF Core: in einem Query-Filter ersetzt es eine
        /// <c>DbContext</c>-Konstante durch einen Zugriff auf den <b>gerade laufenden</b> Kontext. Genau
        /// diese Zusage prueft dieser Test - und haelt sie fest, damit ein EF-Wechsel sie nicht
        /// stillschweigend zuruecknehmen kann. Die uebrigen Tests hier wuerden es nicht merken: sie geben
        /// jedem Kontext seinen eigenen Provider und damit seine eigene Registrierung.
        /// </para>
        /// </remarks>
        [TestMethod]
        public void SharedOptionsProvider_FilterFollowsTheRunningContext_NotTheFirstOne()
        {
            var shared = new WorkflowFilterInitializer<WorkflowContext>();

            WorkflowContext ContextFor(string tenant) => new WorkflowContext(
                new SqliteTestOptionsLoader(connection), TestServices.ForTenant(tenant),
                useTenantFilter: true, shared);

            // acme ist als erster da und praegt damit die Registrierung im gemeinsamen Visitor.
            var acme = new EfWorkflowStore(() => ContextFor("acme"));
            acme.SaveDefinition(Definition("acmeflow", "acme"));
            Assert.IsNotNull(acme.GetDefinition("acmeflow"), "the first context must see its own definition");

            // beta kommt danach - mit demselben Provider, demselben Options-Objekt, demselben Modell.
            var beta = new EfWorkflowStore(() => ContextFor("beta"));
            beta.SaveDefinition(Definition("betaflow", "beta"));

            Assert.IsNull(beta.GetDefinition("acmeflow"),
                "the filter must evaluate on the RUNNING context - not on the one that registered first");
            Assert.IsNotNull(beta.GetDefinition("betaflow"), "and beta must see its own");
            Assert.IsNull(acme.GetDefinition("betaflow"), "the same in the other direction");

            using WorkflowContext ctx = ContextFor("beta");
            Assert.AreEqual("beta", ctx.CurrentTenant);
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
