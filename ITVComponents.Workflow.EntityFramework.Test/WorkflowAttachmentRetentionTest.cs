using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Workflow.EntityFramework.Abstractions;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Retention;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Workflow.EntityFramework.Test
{
    /// <summary>
    /// Die <b>eigene Frist der Anhang-Inhalte</b>: sie duerfen frueher wegfallen als der Vorgang - und
    /// die Beschreibung bleibt in jedem Fall stehen.
    /// </summary>
    /// <remarks>
    /// Nur gegen die Datenbank-Fassung: Anhaenge gibt es allein dort. Der Lauf liegt aus demselben Grund
    /// nicht im Kern - die Inhalte stecken hinter einer austauschbaren, asynchronen Ablage.
    /// </remarks>
    [TestClass]
    public class WorkflowAttachmentRetentionTest
    {
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

        private EfWorkflowStore NewStore() => new EfWorkflowStore(() => new WorkflowContext(options));

        private IWorkflowAttachmentStore Attachments()
            => new EfWorkflowAttachmentStore(() => new WorkflowContext(options));

        private WorkflowDefinition Definition(EfWorkflowStore store, int? attachmentDays,
            int? retentionDays = null, bool allowOverride = false)
        {
            var definition = new WorkflowDefinition
            {
                Id = "wf",
                Version = 1,
                Name = "Der Ablauf wf",
                RetentionDays = retentionDays,
                AttachmentRetentionDays = attachmentDays,
                AllowTenantRetentionOverride = allowOverride
            };
            store.SaveDefinition(definition);
            return definition;
        }

        /// <summary>Ein beendeter Vorgang mit einem Anhang, dessen Bytes in der Datenbank liegen.</summary>
        private string EndedWithAttachment(EfWorkflowStore store, WorkflowDefinition definition,
            DateTime endedUtc, string fileIdentifier = "blob-1")
        {
            var instance = new WorkflowInstance
            {
                DefinitionKey = definition.Key,
                DefinitionId = definition.Id,
                DefinitionVersion = definition.Version,
                TenantId = "t1",
                Status = WorkflowStatus.Completed,
                CreatedUtc = endedUtc.AddDays(-1),
                EndedUtc = endedUtc
            };
            store.SaveInstance(instance);

            using var ctx = new WorkflowContext(options);
            ctx.WorkflowAttachments.Add(new WorkflowAttachmentRow
            {
                InstanceId = instance.Id, TenantId = "t1", Author = "anna", CreatedUtc = endedUtc,
                FileName = "rechnung.pdf", ContentType = "application/pdf", SizeBytes = 3,
                FileIdentifier = fileIdentifier
            });
            ctx.WorkflowAttachmentBlobs.Add(new WorkflowAttachmentBlobRow
            {
                FileIdentifier = fileIdentifier, ContentType = "application/pdf",
                DownloadName = "rechnung.pdf", Content = new byte[] { 1, 2, 3 }
            });
            ctx.SaveChanges();
            return instance.Id;
        }

        private Task<WorkflowAttachmentRetentionResult> Run(EfWorkflowStore store,
            IWorkflowAttachmentStore attachments = null, WorkflowRetentionDefaults defaults = null)
            => new WorkflowAttachmentRetentionRunner(store, attachments ?? Attachments(), defaults)
                .RunAsync(Now);

        private int BlobCount(string fileIdentifier)
        {
            using var ctx = new WorkflowContext(options);
            return ctx.WorkflowAttachmentBlobs.Count(b => b.FileIdentifier == fileIdentifier);
        }

        private WorkflowAttachmentRow Description(string instanceId)
        {
            using var ctx = new WorkflowContext(options);
            return ctx.WorkflowAttachments.IgnoreQueryFilters()
                .First(a => a.InstanceId == instanceId);
        }

        // --- Der Kern der Sache -------------------------------------------------------------------------

        [TestMethod]
        public async Task TheBytesGo_WhileTheProcessAndItsDescriptionStay()
        {
            // Genau dafuer gibt es zwei Fristen: den Vorgang zehn Jahre, die PDFs eines.
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = Definition(store, attachmentDays: 30, retentionDays: 3650);
            string id = EndedWithAttachment(store, definition, Now.AddDays(-40));

            WorkflowAttachmentRetentionResult result = await Run(store);

            Assert.AreEqual(1, result.ProcessesPurged);
            Assert.AreEqual(1, result.FilesDeleted);
            Assert.AreEqual(0, BlobCount("blob-1"), "the bytes are gone.");
            Assert.IsNotNull(store.GetInstance(id),
                "the process itself stays - its own deadline is ten years away.");

            WorkflowAttachmentRow description = Description(id);
            Assert.AreEqual("rechnung.pdf", description.FileName,
                "the description stays: a process that cannot say 'there was a file here' would be "
                + "kept incompletely.");
            Assert.AreEqual("blob-1", description.FileIdentifier,
                "and so does the identifier - it says WHAT lay there.");
            Assert.AreEqual(Now, description.BytesPurgedUtc, "with the moment the bytes went.");
        }

        [TestMethod]
        public async Task NotYetDue_MeansNothingHappens()
        {
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = Definition(store, attachmentDays: 90);
            EndedWithAttachment(store, definition, Now.AddDays(-40));

            WorkflowAttachmentRetentionResult result = await Run(store);

            Assert.AreEqual(1, result.GroupsWithoutWork,
                "the oldest of the group is younger than the deadline - the group is settled at once.");
            Assert.AreEqual(1, BlobCount("blob-1"));
        }

        [TestMethod]
        public async Task WithoutAnyDeadline_NothingIsDeleted()
        {
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = Definition(store, attachmentDays: null);
            EndedWithAttachment(store, definition, Now.AddYears(-5));

            await Run(store);

            Assert.AreEqual(1, BlobCount("blob-1"),
                "whoever configures nothing loses nothing - not even after five years.");
        }

        [TestMethod]
        public async Task ARunningProcess_KeepsItsFiles()
        {
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = Definition(store, attachmentDays: 0);
            string id = EndedWithAttachment(store, definition, Now.AddDays(-40));

            // Nachtraeglich wieder in Gang gesetzt: damit ist auch das Ende weg, und ohne Ende gibt es
            // keine Frist, die laufen koennte.
            WorkflowInstance instance = store.GetInstance(id);
            instance.Status = WorkflowStatus.Running;
            store.SaveInstance(instance);

            WorkflowAttachmentRetentionResult result = await Run(store);

            Assert.AreEqual(0, result.GroupsSeen, "a process without an end forms no group.");
            Assert.AreEqual(1, BlobCount("blob-1"));
        }

        [TestMethod]
        public async Task AnArchivedProcess_LosesItsBytesToo_AndSaysSo()
        {
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = Definition(store, attachmentDays: 30, retentionDays: 30);
            string id = EndedWithAttachment(store, definition, Now.AddDays(-40));
            Assert.AreEqual(1, store.ArchiveInstanceTree(id, Now), "precondition: it is archived.");

            WorkflowAttachmentRetentionResult result = await Run(store);

            Assert.AreEqual(1, result.ProcessesPurged, "the archive is the second source, not the only one.");
            Assert.AreEqual(0, BlobCount("blob-1"));
            Assert.AreEqual(Now, store.GetArchivedInstance(id).AttachmentsPurgedUtc,
                "the archive row carries the mark - the description rows are long gone.");
        }

        [TestMethod]
        public async Task ARunAfterTheBytesAreGone_FindsNothingToDo()
        {
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = Definition(store, attachmentDays: 30, retentionDays: 3650);
            EndedWithAttachment(store, definition, Now.AddDays(-40));
            await Run(store);

            WorkflowAttachmentRetentionResult second = await Run(store);

            Assert.AreEqual(0, second.GroupsSeen,
                "what is marked as purged must not turn up again - otherwise every run walks the same "
                + "files forever.");
        }

        [TestMethod]
        public async Task ATenantsObjection_KeepsTheFilesLonger()
        {
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = Definition(store, attachmentDays: 30, retentionDays: 3650,
                allowOverride: true);
            EndedWithAttachment(store, definition, Now.AddDays(-40));
            store.SaveRetentionOverride(new WorkflowRetentionOverride
            {
                OwnerTenantId = null, DefinitionId = "wf", TenantId = "t1",
                AttachmentRetentionDays = 3650
            });

            await Run(store);

            Assert.AreEqual(1, BlobCount("blob-1"),
                "the attachment deadline runs through the same chain as the process deadline.");
        }

        // --- Wenn das Loeschen scheitert ---------------------------------------------------------------

        [TestMethod]
        public async Task AFailedDelete_LeavesTheProcessUnmarkedForTheNextRun()
        {
            EfWorkflowStore store = NewStore();
            WorkflowDefinition definition = Definition(store, attachmentDays: 30, retentionDays: 3650);
            string id = EndedWithAttachment(store, definition, Now.AddDays(-40));

            WorkflowAttachmentRetentionResult result = await Run(store, new FailingAttachmentStore());

            Assert.AreEqual(1, result.ProcessesFailed, "counted apart from 'nothing to do'.");
            Assert.AreEqual(0, result.ProcessesPurged);
            Assert.IsNull(Description(id).BytesPurgedUtc,
                "unmarked - marking a file as gone that is still there would hide it forever.");
            Assert.AreEqual(1, BlobCount("blob-1"));

            // Und der naechste Lauf nimmt ihn wieder auf.
            Assert.AreEqual(1, (await Run(store)).ProcessesPurged, "the retry does the work.");
            Assert.AreEqual(0, BlobCount("blob-1"));
        }

        /// <summary>Eine Ablage, die beim Loeschen scheitert - der Fall, den der Lauf ueberleben muss.</summary>
        private sealed class FailingAttachmentStore : IWorkflowAttachmentStore
        {
            public Task<string> SaveAsync(byte[] content, string contentType, string downloadName,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<WorkflowAttachmentContent> OpenAsync(string fileIdentifier,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task DeleteAsync(string fileIdentifier, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("the object store is not reachable.");
        }
    }
}
