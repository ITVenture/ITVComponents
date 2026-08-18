using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Prueft die technische Kennung der Definition: dass zwei Mandanten denselben fachlichen Namen
    /// benutzen duerfen, und dass eine laufende Instanz an GENAU ihrer Definition bleibt.
    /// </summary>
    /// <remarks>
    /// Vorher war (Id, Version) der Primaerschluessel - der Mandant passte dort nicht hinein, weil er
    /// null sein darf (= oeffentlich). Zwei Mandanten konnten deshalb nicht denselben Namen benutzen,
    /// und der Verweis der Instanz war ueber Name und Version mehrdeutig.
    /// </remarks>
    [TestClass]
    public class WorkflowDefinitionKeyTest
    {
        private SqliteConnection connection;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            using var ctx = new WorkflowContext(new SqliteTestOptionsLoader(connection),
                TestServices.ForTenant(null), useTenantFilter: true,
                new WorkflowFilterInitializer<WorkflowContext>());
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        private EfWorkflowStore StoreFor(string tenant)
            => new EfWorkflowStore(() => new WorkflowContext(new SqliteTestOptionsLoader(connection),
                TestServices.ForTenant(tenant), useTenantFilter: true,
                new WorkflowFilterInitializer<WorkflowContext>()));

        private static WorkflowDefinition Linear(string id, bool isPublic = false)
        {
            return new WorkflowDefinition
            {
                Id = id,
                Version = 1,
                Name = id,
                IsPublic = isPublic,
                Nodes = new List<WorkflowNode>
                {
                    new StartNode { Id = "s" },
                    new AutomatedActivityNode { Id = "a", ActivityRef = "noop" },
                    new EndNode { Id = "e" }
                },
                Flows = new List<SequenceFlow>
                {
                    new SequenceFlow { Id = "f1", SourceId = "s", TargetId = "a" },
                    new SequenceFlow { Id = "f2", SourceId = "a", TargetId = "e" }
                }
            };
        }

        [TestMethod]
        public void TwoTenantsMayUseTheSameName()
        {
            var acme = StoreFor("acme");
            var beta = StoreFor("beta");

            WorkflowDefinition a = Linear("onboarding");
            WorkflowDefinition b = Linear("onboarding");
            acme.SaveDefinition(a);
            beta.SaveDefinition(b);

            Assert.AreNotEqual(0, a.Key);
            Assert.AreNotEqual(a.Key, b.Key,
                "with (Id, Version) as the primary key the second tenant collided - that was the defect.");
            Assert.AreEqual("acme", a.TenantId);
            Assert.AreEqual("beta", b.TenantId,
                "without an explicit public flag the definition belongs to the tenant that saved it.");
        }

        [TestMethod]
        public void WithoutSayingPublic_ItBelongsToTheCurrentTenant()
        {
            var acme = StoreFor("acme");
            var beta = StoreFor("beta");
            WorkflowDefinition definition = Linear("private");

            acme.SaveDefinition(definition);

            Assert.AreEqual("acme", definition.TenantId);
            Assert.IsNull(beta.GetDefinition("private"),
                "silently creating a definition every other tenant can start is the failure mode this " +
                "guards against.");
        }

        [TestMethod]
        public void PublicIsAnExplicitDecision()
        {
            var acme = StoreFor("acme");
            var beta = StoreFor("beta");
            WorkflowDefinition definition = Linear("shared", isPublic: true);

            acme.SaveDefinition(definition);

            Assert.IsNull(definition.TenantId, "public means no tenant.");
            Assert.IsNotNull(beta.GetDefinition("shared"), "every tenant sees a public definition.");
            Assert.IsTrue(beta.GetDefinition("shared").IsPublic);
        }

        [TestMethod]
        public void PublicAndATenantAtOnce_IsRejected()
        {
            var acme = StoreFor("acme");
            WorkflowDefinition definition = Linear("shared", isPublic: true);
            definition.TenantId = "acme";

            Assert.ThrowsException<System.InvalidOperationException>(() => acme.SaveDefinition(definition),
                "that is a contradiction, not something to interpret.");
        }

        [TestMethod]
        public void TheOwnDefinitionBeatsThePublicOneOfTheSameName()
        {
            var acme = StoreFor("acme");
            WorkflowDefinition shared = Linear("report", isPublic: true);
            acme.SaveDefinition(shared);
            WorkflowDefinition own = Linear("report");
            acme.SaveDefinition(own);

            WorkflowDefinition resolved = acme.GetDefinition("report");

            Assert.AreEqual(own.Key, resolved.Key,
                "a tenant's own version refines the public one - without a rule the database order would " +
                "decide, which is to say chance.");
            Assert.AreEqual(shared.Key, StoreFor("beta").GetDefinition("report").Key,
                "another tenant still gets the public one.");
        }

        [TestMethod]
        public void ARunningInstanceStaysOnItsOwnDefinition()
        {
            var acme = StoreFor("acme");
            WorkflowDefinition shared = Linear("claim", isPublic: true);
            acme.SaveDefinition(shared);

            var ran = new List<string>();
            var engine = new WorkflowEngine(acme,
                new ActivityRegistry().Register("noop", ctx => ran.Add("public")));
            WorkflowInstance instance = engine.CreateInstance("claim");

            Assert.AreEqual(shared.Key, instance.DefinitionKey);

            // Jetzt legt der Mandant eine eigene Fassung DESSELBEN Namens und derselben Version an.
            WorkflowDefinition own = Linear("claim");
            acme.SaveDefinition(own);
            Assert.AreNotEqual(shared.Key, own.Key);

            engine.Advance(acme.GetInstance(instance.Id));

            Assert.AreEqual(WorkflowStatus.Completed, acme.GetInstance(instance.Id).Status);
            Assert.AreEqual(shared.Key, acme.GetInstance(instance.Id).DefinitionKey,
                "the instance keeps the definition it started on - resolving by name would have moved it " +
                "onto a different graph mid-flight.");
        }

        [TestMethod]
        public void UpdatingADefinitionKeepsItsKey()
        {
            var acme = StoreFor("acme");
            WorkflowDefinition definition = Linear("stable");
            acme.SaveDefinition(definition);
            int first = definition.Key;

            definition.Name = "renamed";
            acme.SaveDefinition(definition);

            Assert.AreEqual(first, definition.Key,
                "running instances hang on that key - it must not move when the definition is edited.");
            Assert.AreEqual("renamed", acme.GetDefinition(first).Name);
        }

        [TestMethod]
        public void TheKeyIsNotPartOfTheExchangeFormat()
        {
            var acme = StoreFor("acme");
            WorkflowDefinition definition = Linear("portable");
            acme.SaveDefinition(definition);

            string json = ITVComponents.Workflow.Serialization.WorkflowJson.ExportDefinition(definition);

            StringAssert.DoesNotMatch(json, new System.Text.RegularExpressions.Regex("\"[Kk]ey\""),
                "the technical key holds in ONE store - in an exported file it would point at something " +
                "else entirely.");
        }

        [TestMethod]
        public void AnInstanceWithoutItsDefinition_IsRefusedByTheDatabase()
        {
            var acme = StoreFor("acme");
            var instance = new WorkflowInstance
            {
                DefinitionKey = 4711, DefinitionId = "ghost", DefinitionVersion = 1
            };

            Assert.ThrowsException<DbUpdateException>(() => acme.SaveInstance(instance),
                "the reference is a real foreign key - an instance without a definition cannot run, so it " +
                "must not exist.");
        }
    }
}
