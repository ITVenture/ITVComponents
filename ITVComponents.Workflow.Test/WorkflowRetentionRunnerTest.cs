using System;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Retention;
using ITVComponents.Workflow.Stores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.Test
{
    /// <summary>
    /// Der Aufbewahrungslauf: welche Gruppe er anfasst, welche er in Ruhe laesst, und was er meldet.
    /// </summary>
    /// <remarks>
    /// Ohne Datenbank und ohne Uhr - der Lauf bekommt seinen Zeitpunkt uebergeben. Damit laesst sich
    /// „in zwei Jahren" pruefen, statt zwei Jahre zu warten, und die Frage „warum wurde das
    /// weggeraeumt?" beantwortet sich an einem Tisch.
    /// </remarks>
    [TestClass]
    public class WorkflowRetentionRunnerTest
    {
        private static readonly DateTime Now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

        private InMemoryWorkflowStore store;

        [TestInitialize]
        public void Setup() => store = new InMemoryWorkflowStore();

        private WorkflowDefinition Definition(string id, int? retentionDays = null,
            bool allowOverride = false, string tenantId = null)
        {
            var definition = new WorkflowDefinition
            {
                Id = id,
                Version = 1,
                Name = $"Der Ablauf {id}",
                TenantId = tenantId,
                RetentionDays = retentionDays,
                AllowTenantRetentionOverride = allowOverride
            };
            store.SaveDefinition(definition);
            return definition;
        }

        /// <summary>
        /// Ein beendeter Vorgang mit einem <b>gesetzten</b> Endzeitpunkt. Der ueberlebt das Speichern:
        /// ein bestehendes Ende wird nie ueberschrieben - sonst liesse sich hier gar nichts stellen.
        /// </summary>
        private string Ended(WorkflowDefinition definition, string tenantId, DateTime endedUtc,
            WorkflowStatus status = WorkflowStatus.Completed, string parentInstanceId = null)
        {
            var instance = new WorkflowInstance
            {
                DefinitionKey = definition.Key,
                DefinitionId = definition.Id,
                DefinitionVersion = definition.Version,
                TenantId = tenantId,
                Status = status,
                CreatedUtc = endedUtc.AddDays(-1),
                EndedUtc = status.IsEnded() ? endedUtc : (DateTime?)null,
                ParentInstanceId = parentInstanceId
            };
            store.SaveInstance(instance);
            return instance.Id;
        }

        private WorkflowRetentionResult Run(WorkflowRetentionDefaults defaults = null, int maxPerGroup = 200)
            => new WorkflowRetentionRunner(store, defaults).Run(Now, maxPerGroup);

        // --- Wer nichts einstellt, verliert nichts -----------------------------------------------------

        [TestMethod]
        public void NothingConfiguredAnywhere_ArchivesNothing()
        {
            WorkflowDefinition definition = Definition("wf");
            string id = Ended(definition, "t1", Now.AddYears(-5));

            WorkflowRetentionResult result = Run();

            Assert.AreEqual(0, result.InstancesArchived,
                "without any deadline nothing is cleaned up - not even after five years.");
            Assert.AreEqual(1, result.GroupsWithoutWork, "the group was seen and left alone.");
            Assert.IsNotNull(store.GetInstance(id), "and the process is still there.");
        }

        [TestMethod]
        public void TheGlobalDefault_AppliesWhenTheDefinitionSaysNothing()
        {
            WorkflowDefinition definition = Definition("wf");
            string id = Ended(definition, "t1", Now.AddDays(-40));

            WorkflowRetentionResult result = Run(new WorkflowRetentionDefaults { RetentionDays = 30 });

            Assert.AreEqual(1, result.InstancesArchived, "the global default is the last stage of the chain.");
            Assert.IsNull(store.GetInstance(id), "the process left the active tables.");
            Assert.IsNotNull(store.GetArchivedInstance(id), "and arrived in the archive.");
        }

        // --- Die Kette ---------------------------------------------------------------------------------

        [TestMethod]
        public void TheDefinitionsDeadline_BeatsTheGlobalOne()
        {
            WorkflowDefinition definition = Definition("wf", retentionDays: 90);
            string id = Ended(definition, "t1", Now.AddDays(-40));

            Run(new WorkflowRetentionDefaults { RetentionDays = 30 });

            Assert.IsNotNull(store.GetInstance(id),
                "the definition says 90 days - the global 30 must not reach through.");
        }

        [TestMethod]
        public void TheTenantsObjection_HoldsItBack()
        {
            WorkflowDefinition definition = Definition("wf", retentionDays: 30, allowOverride: true);
            string id = Ended(definition, "t1", Now.AddDays(-40));
            store.SaveRetentionOverride(new WorkflowRetentionOverride
            {
                OwnerTenantId = null, DefinitionId = "wf", TenantId = "t1", RetentionDays = 3650
            });

            Run();

            Assert.IsNotNull(store.GetInstance(id), "the tenant wants to keep it - and may.");
        }

        [TestMethod]
        public void AnObjectionTheDefinitionDoesNotAllow_ChangesNothing()
        {
            WorkflowDefinition definition = Definition("wf", retentionDays: 30);
            string id = Ended(definition, "t1", Now.AddDays(-40));
            store.SaveRetentionOverride(new WorkflowRetentionOverride
            {
                OwnerTenantId = null, DefinitionId = "wf", TenantId = "t1", RetentionDays = 3650
            });

            Run();

            Assert.IsNull(store.GetInstance(id),
                "without AllowTenantRetentionOverride the objection stands but does not act.");
        }

        [TestMethod]
        public void TheObjectionOfAnotherTenant_IsNoneOfThisOnesBusiness()
        {
            WorkflowDefinition definition = Definition("wf", retentionDays: 30, allowOverride: true);
            string mine = Ended(definition, "t1", Now.AddDays(-40));
            string theirs = Ended(definition, "t2", Now.AddDays(-40));
            store.SaveRetentionOverride(new WorkflowRetentionOverride
            {
                OwnerTenantId = null, DefinitionId = "wf", TenantId = "t2", RetentionDays = 3650
            });

            Run();

            Assert.IsNull(store.GetInstance(mine), "t1 said nothing - the definition's 30 days apply.");
            Assert.IsNotNull(store.GetInstance(theirs), "t2 objected, and only for itself.");
        }

        // --- Was der Lauf gar nicht erst anfasst -------------------------------------------------------

        [TestMethod]
        public void AGroupWhoseOldestIsNotDue_IsDroppedWithoutReadingAnInstance()
        {
            WorkflowDefinition definition = Definition("wf", retentionDays: 90);
            Ended(definition, "t1", Now.AddDays(-10));
            Ended(definition, "t1", Now.AddDays(-20));

            WorkflowRetentionResult result = Run();

            Assert.AreEqual(1, result.GroupsSeen);
            Assert.AreEqual(1, result.GroupsWithoutWork,
                "the oldest of the group is younger than the deadline - that settles the whole group.");
            Assert.AreEqual(0, result.InstancesArchived);
        }

        [TestMethod]
        public void ARunningProcess_IsNoCandidate()
        {
            WorkflowDefinition definition = Definition("wf", retentionDays: 0);
            string running = Ended(definition, "t1", Now.AddYears(-1), WorkflowStatus.Waiting);

            WorkflowRetentionResult result = Run();

            Assert.AreEqual(0, result.GroupsSeen, "a process that has not ended is not even a group.");
            Assert.IsNotNull(store.GetInstance(running));
        }

        [TestMethod]
        public void ACancelledProcess_CountsAsEnded()
        {
            // Abgebrochen ist ein Ende wie jedes andere - fuer die Aufbewahrung ist er ein fertiger
            // Datensatz. Stuende das nur an einer der drei Stellen, waere es irgendwann uneinheitlich.
            WorkflowDefinition definition = Definition("wf", retentionDays: 30);
            string id = Ended(definition, "t1", Now.AddDays(-40), WorkflowStatus.Cancelled);

            Run();

            Assert.IsNull(store.GetInstance(id));
            Assert.AreEqual(WorkflowStatus.Cancelled, store.GetArchivedInstance(id).Status,
                "and the archive remembers HOW it ended.");
        }

        // --- Der Prozessbaum ---------------------------------------------------------------------------

        [TestMethod]
        public void TheWholeTree_GoesAtOnce_AndCountsAsOne()
        {
            WorkflowDefinition parent = Definition("parent", retentionDays: 30);
            WorkflowDefinition child = Definition("child", retentionDays: 3650);
            string rootId = Ended(parent, "t1", Now.AddDays(-40));
            string childId = Ended(child, "t1", Now.AddDays(-41), parentInstanceId: rootId);

            WorkflowRetentionResult result = Run();

            Assert.AreEqual(1, result.TreesArchived, "one tree...");
            Assert.AreEqual(2, result.InstancesArchived, "...but two instances.");
            Assert.IsNotNull(store.GetArchivedInstance(childId),
                "the child goes with it - even though its own definition would have kept it ten years. "
                + "The tree is one process for the reader, and its deadline is the root's.");
            Assert.AreEqual(rootId, store.GetArchivedInstance(childId).ParentInstanceId,
                "and the link survives.");
        }

        [TestMethod]
        public void AChildIsNeverAnEntryPoint()
        {
            WorkflowDefinition parent = Definition("parent");
            WorkflowDefinition child = Definition("child", retentionDays: 0);
            string rootId = Ended(parent, "t1", Now.AddDays(-40));
            string childId = Ended(child, "t1", Now.AddDays(-41), parentInstanceId: rootId);

            WorkflowRetentionResult result = Run();

            Assert.AreEqual(1, result.GroupsSeen,
                "only the root forms a group - otherwise a parent could lose its children.");
            Assert.IsNotNull(store.GetInstance(childId),
                "and the child's own zero-day deadline does not reach it.");
        }

        [TestMethod]
        public void AStillRunningSubProcess_StopsTheWholeTree()
        {
            WorkflowDefinition parent = Definition("parent", retentionDays: 30);
            WorkflowDefinition child = Definition("child");
            string rootId = Ended(parent, "t1", Now.AddDays(-40));
            string childId = Ended(child, "t1", Now, WorkflowStatus.Waiting, parentInstanceId: rootId);

            WorkflowRetentionResult result = Run();

            Assert.AreEqual(1, result.TreesRefused,
                "refused - and counted separately, so 'nothing to do' and 'not done' stay apart.");
            Assert.AreEqual(0, result.InstancesArchived);
            Assert.IsNotNull(store.GetInstance(rootId), "no half tree: the root stays too.");
            Assert.IsNotNull(store.GetInstance(childId));
        }

        // --- Was der Lauf ueber sich selbst sagt -------------------------------------------------------

        [TestMethod]
        public void TheLimitPerRun_IsReportedAndNotHidden()
        {
            WorkflowDefinition definition = Definition("wf", retentionDays: 30);
            Ended(definition, "t1", Now.AddDays(-40));
            Ended(definition, "t1", Now.AddDays(-41));

            WorkflowRetentionResult result = Run(maxPerGroup: 1);

            Assert.AreEqual(1, result.InstancesArchived, "one per run, as asked.");
            Assert.AreEqual(1, result.GroupsTruncated,
                "and it says so - a truncated run must not look like a complete one.");
        }

        [TestMethod]
        public void ARunWithoutALimit_IsRefused()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Run(maxPerGroup: 0),
                "a run that may archive nothing is not a run.");
        }

        [TestMethod]
        public void TheArchive_KeepsWhatTheProcessWasAbout()
        {
            WorkflowDefinition definition = Definition("wf", retentionDays: 30);
            var instance = new WorkflowInstance
            {
                DefinitionKey = definition.Key,
                DefinitionId = "wf",
                DefinitionVersion = 1,
                TenantId = "t1",
                Status = WorkflowStatus.Faulted,
                FaultCode = "PAYMENT_DECLINED",
                FaultMessage = "the card was refused",
                CreatedUtc = Now.AddDays(-41),
                EndedUtc = Now.AddDays(-40)
            };
            instance.Variables["amount"] = 42;
            store.SaveInstance(instance);

            Run();

            WorkflowArchivedInstance archived = store.GetArchivedInstance(instance.Id);
            Assert.AreEqual("PAYMENT_DECLINED", archived.FaultCode,
                "a failed process is the most interesting record there is - the archive keeps why.");
            Assert.AreEqual("Der Ablauf wf", archived.DefinitionName,
                "the definition's name travels along as TEXT - it must stay readable when the "
                + "definition itself is cleaned up.");
            Assert.AreEqual(Now, archived.ArchivedUtc);
            Assert.AreEqual(Now.AddDays(-40), archived.EndedUtc);
        }
    }
}
