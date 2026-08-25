using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Retention;
using ITVComponents.Workflow.Serialization;
using ITVComponents.Workflow.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Das Archivieren eines Vorgangs - gefahren gegen <b>beide</b> Speicher-Fassungen, plus die Teile,
    /// die es nur in der Datenbank gibt (Kommentare, Anhaenge, Verlaufszeilen).
    /// </summary>
    /// <remarks>
    /// Archivieren ist der einzige Weg in diesem Umfeld, der Zeilen <b>loescht</b>. Ein Test, der nur
    /// gegen den Speicher ohne Datenbank gruen ist, bewiese dafuer nichts: dort ist „weg" ein Eintrag
    /// weniger im Dictionary, hier haengen Tokens, Verlauf, Kommentare, Anhaenge, Outbox und Sperren
    /// daran - teils per Kaskade, teils gar nicht.
    /// </remarks>
    [TestClass]
    public class WorkflowArchiveContractTest
    {
        private const string Memory = "memory";
        private const string Ef = "ef";

        private static readonly DateTime Now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

        private SqliteConnection connection;
        private DbContextOptions<WorkflowContext> options;

        [TestInitialize]
        public void Setup()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            options = new DbContextOptionsBuilder<WorkflowContext>().UseSqlite(connection).Options;
            using var ctx = new WorkflowContext(options);
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup() => connection?.Dispose();

        private IWorkflowStore NewStore(string kind)
            => kind == Ef
                ? new EfWorkflowStore(() => new WorkflowContext(options))
                : new InMemoryWorkflowStore();

        private EfWorkflowStore NewEfStore() => new EfWorkflowStore(() => new WorkflowContext(options));

        private static WorkflowDefinition Definition(IWorkflowStore store, string id)
        {
            var definition = new WorkflowDefinition { Id = id, Version = 1, Name = $"Der Ablauf {id}" };
            store.SaveDefinition(definition);
            return definition;
        }

        /// <summary>
        /// Ein Vorgang mit gesetztem Ende. Der gesetzte Wert ueberlebt das Speichern - ein bestehendes
        /// Ende wird nie ueberschrieben.
        /// </summary>
        private static WorkflowInstance Ended(IWorkflowStore store, WorkflowDefinition definition,
            WorkflowStatus status = WorkflowStatus.Completed, string parentInstanceId = null)
        {
            var instance = new WorkflowInstance
            {
                DefinitionKey = definition.Key,
                DefinitionId = definition.Id,
                DefinitionVersion = definition.Version,
                TenantId = "t1",
                Status = status,
                CreatedUtc = Now.AddDays(-41),
                EndedUtc = status.IsEnded() ? Now.AddDays(-40) : (DateTime?)null,
                ParentInstanceId = parentInstanceId,
                RootInstanceId = parentInstanceId
            };
            store.SaveInstance(instance);
            return instance;
        }

        // --- Der Endzeitpunkt --------------------------------------------------------------------------

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void TheEnd_IsStampedWhenTheProcessEnds(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowDefinition definition = Definition(store, "wf");
            var instance = new WorkflowInstance
            {
                DefinitionKey = definition.Key,
                DefinitionId = "wf",
                DefinitionVersion = 1,
                TenantId = "t1",
                Status = WorkflowStatus.Running
            };
            store.SaveInstance(instance);

            Assert.IsNull(store.GetInstance(instance.Id).EndedUtc,
                $"[{kind}] a running process has not ended.");

            instance.Status = WorkflowStatus.Completed;
            store.SaveInstance(instance);
            DateTime? first = store.GetInstance(instance.Id).EndedUtc;

            Assert.IsNotNull(first, $"[{kind}] the end is stamped where every change passes through.");
            Assert.AreEqual(DateTimeKind.Utc, first.Value.Kind,
                $"[{kind}] a field named Utc must say so itself.");

            // Anhalten ist bei einem gescheiterten oder beendeten Vorgang ausdruecklich erlaubt - und
            // darf die Aufbewahrungsuhr nicht neu starten. Genau daran scheiterte UpdatedUtc.
            instance.Suspended = true;
            store.SaveInstance(instance);

            Assert.AreEqual(first, store.GetInstance(instance.Id).EndedUtc,
                $"[{kind}] a later change must not move the end - that is the whole reason for the field.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void AResumedProcess_LosesItsEndAgain(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowDefinition definition = Definition(store, "wf");
            WorkflowInstance instance = Ended(store, definition, WorkflowStatus.Faulted);

            instance.Status = WorkflowStatus.Running;
            store.SaveInstance(instance);

            Assert.IsNull(store.GetInstance(instance.Id).EndedUtc,
                $"[{kind}] a process that runs again has not ended - a deadline on a superseded end "
                + "would be worse than none.");
        }

        // --- Was archiviert wird und was nicht ----------------------------------------------------------

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void OnlyEndedRoots_FormAGroup(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowDefinition definition = Definition(store, "wf");
            WorkflowInstance root = Ended(store, definition);
            Ended(store, definition, parentInstanceId: root.Id);
            Ended(store, definition, WorkflowStatus.Waiting);

            IReadOnlyList<WorkflowRetentionGroup> groups = store.ListEndedInstanceGroups();

            Assert.AreEqual(1, groups.Count, $"[{kind}] one definition, one tenant - one group.");
            Assert.AreEqual(1, groups[0].Count,
                $"[{kind}] neither the child nor the running process is an entry point.");
            Assert.AreEqual(Now.AddDays(-40), groups[0].OldestEndedUtc,
                $"[{kind}] the oldest end decides whether the group is worth reading at all.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void AnArchivedProcess_LeavesTheActiveTablesAndKeepsItsFacts(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowDefinition definition = Definition(store, "wf");
            WorkflowInstance instance = Ended(store, definition, WorkflowStatus.Faulted);
            instance.FaultCode = "PAYMENT_DECLINED";
            instance.Variables["amount"] = 42;
            instance.Log("Started", "n1");
            store.SaveInstance(instance);

            Assert.AreEqual(1, store.ArchiveInstanceTree(instance.Id, Now), $"[{kind}] one instance.");

            Assert.IsNull(store.GetInstance(instance.Id), $"[{kind}] gone from the active tables.");
            WorkflowArchivedInstance archived = store.GetArchivedInstance(instance.Id);
            Assert.IsNotNull(archived, $"[{kind}] and present in the archive.");
            Assert.AreEqual("PAYMENT_DECLINED", archived.FaultCode, $"[{kind}] the fault code stays.");
            Assert.AreEqual("Der Ablauf wf", archived.DefinitionName,
                $"[{kind}] the definition's name travels as text - the archive must stay readable "
                + "without it.");
            Assert.AreEqual(Now, archived.ArchivedUtc, $"[{kind}] with the time it was archived.");

            var payload = WorkflowJson.Deserialize<WorkflowArchivePayload>(archived.PayloadJson);
            Assert.AreEqual(42, WorkflowJson.DeserializeVariables(payload.VariablesJson)["amount"],
                $"[{kind}] the variables come back TYPED - an int must not turn into a JsonElement on "
                + "the way into the archive.");
            Assert.IsTrue(payload.History.Any(h => h.Event == "Started"),
                $"[{kind}] and the trail of what happened is part of the record.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void TheWholeTree_GoesInOneGo(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowDefinition definition = Definition(store, "wf");
            WorkflowInstance root = Ended(store, definition);
            WorkflowInstance child = Ended(store, definition, parentInstanceId: root.Id);
            WorkflowInstance grandchild = Ended(store, definition, parentInstanceId: child.Id);

            Assert.AreEqual(3, store.ArchiveInstanceTree(root.Id, Now),
                $"[{kind}] three levels deep - the tree is collected step by step, not two levels flat.");
            Assert.IsNotNull(store.GetArchivedInstance(grandchild.Id), $"[{kind}] the grandchild too.");
            Assert.IsNull(store.GetInstance(child.Id), $"[{kind}] and none of them stayed behind.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void AStillRunningChild_LeavesTheWholeTreeAlone(string kind)
        {
            IWorkflowStore store = NewStore(kind);
            WorkflowDefinition definition = Definition(store, "wf");
            WorkflowInstance root = Ended(store, definition);
            WorkflowInstance child = Ended(store, definition, WorkflowStatus.Waiting,
                parentInstanceId: root.Id);

            Assert.AreEqual(0, store.ArchiveInstanceTree(root.Id, Now),
                $"[{kind}] a process tree is archived as a whole or not at all.");
            Assert.IsNotNull(store.GetInstance(root.Id), $"[{kind}] the root stays...");
            Assert.IsNotNull(store.GetInstance(child.Id), $"[{kind}] ...and so does the child.");
            Assert.IsNull(store.GetArchivedInstance(root.Id), $"[{kind}] nothing was half-written.");
        }

        [DataTestMethod]
        [DataRow(Memory)]
        [DataRow(Ef)]
        public void ArchivingWhatIsNoLongerThere_ReportsZeroInsteadOfThrowing(string kind)
        {
            IWorkflowStore store = NewStore(kind);

            Assert.AreEqual(0, store.ArchiveInstanceTree("never-existed", Now),
                $"[{kind}] two runs may meet on the same tree - the second one just has nothing to do.");
        }

        // --- Was nur die Datenbank hat ------------------------------------------------------------------

        [TestMethod]
        public void TokensHistoryAndCommentsGo_ButTheAttachmentBytesStay()
        {
            EfWorkflowStore store = NewEfStore();
            WorkflowDefinition definition = Definition(store, "wf");
            WorkflowInstance instance = Ended(store, definition);
            instance.Tokens.Add(new Token { Id = "tok1", NodeId = "n1", Status = TokenStatus.Consumed });
            instance.Log("Done", "n1");
            store.SaveInstance(instance);

            using (var ctx = new WorkflowContext(options))
            {
                ctx.WorkflowComments.Add(new WorkflowCommentRow
                {
                    InstanceId = instance.Id, TenantId = "t1", Author = "anna",
                    CreatedUtc = Now.AddDays(-41), Text = "bitte pruefen"
                });
                ctx.WorkflowAttachments.Add(new WorkflowAttachmentRow
                {
                    InstanceId = instance.Id, TenantId = "t1", Author = "anna",
                    CreatedUtc = Now.AddDays(-41), FileName = "rechnung.pdf",
                    ContentType = "application/pdf", SizeBytes = 3, FileIdentifier = "blob-1"
                });
                ctx.WorkflowAttachmentBlobs.Add(new WorkflowAttachmentBlobRow
                {
                    FileIdentifier = "blob-1", ContentType = "application/pdf",
                    DownloadName = "rechnung.pdf", Content = new byte[] { 1, 2, 3 }
                });
                ctx.SaveChanges();
            }

            Assert.AreEqual(1, store.ArchiveInstanceTree(instance.Id, Now));

            using (var ctx = new WorkflowContext(options))
            {
                Assert.AreEqual(0, ctx.Tokens.IgnoreQueryFilters().Count(t => t.InstanceId == instance.Id),
                    "tokens hang on no foreign key - they have to be taken along explicitly.");
                Assert.AreEqual(0, ctx.HistoryEntries.Count(h => h.InstanceId == instance.Id),
                    "and so does the trail.");
                Assert.AreEqual(0,
                    ctx.WorkflowComments.IgnoreQueryFilters().Count(c => c.InstanceId == instance.Id),
                    "comments cascade with the instance.");
                Assert.AreEqual(0,
                    ctx.WorkflowAttachments.IgnoreQueryFilters().Count(a => a.InstanceId == instance.Id),
                    "so do the attachment descriptions...");
                Assert.AreEqual(1, ctx.WorkflowAttachmentBlobs.Count(b => b.FileIdentifier == "blob-1"),
                    "...but NOT their bytes: those have a deadline of their own.");
            }

            var payload = WorkflowJson.Deserialize<WorkflowArchivePayload>(
                store.GetArchivedInstance(instance.Id).PayloadJson);
            Assert.AreEqual("bitte pruefen", payload.Comments.Single().Text,
                "the comment moved into the archive - it would be unassignable rubbish otherwise.");
            Assert.AreEqual("rechnung.pdf", payload.Attachments.Single().FileName,
                "and the attachment's description stays in any case: an archive that cannot say "
                + "'there was a file here' would have kept the process incompletely.");
            Assert.AreEqual("blob-1", payload.Attachments.Single().FileIdentifier,
                "including the identifier - it says WHAT lay there, even once the bytes are gone.");
            Assert.AreEqual("tok1", payload.Tokens.Single().Id, "the tokens are kept in their end state.");
        }
    }
}
