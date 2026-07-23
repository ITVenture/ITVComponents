using System;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.Workflow.EntityFramework
{
    /// <summary>
    /// Persistierte Zeile einer Workflow-Instanz. Die JSON-Spalten sind die Wahrheit; die
    /// ausgegliederten Spalten (Status, CorrelationKey und die Warte-Token-Tabelle) sind der
    /// Abfrage-Index, aus dem ein JSON-Blob allein nicht filterbar waere.
    /// </summary>
    public class WorkflowInstanceRow
    {
        /// <summary>Instanz-Id (Primaerschluessel).</summary>
        public string Id { get; set; }

        /// <summary>Fachliche Id der Definition.</summary>
        public string DefinitionId { get; set; }

        /// <summary>Version der Definition.</summary>
        public int DefinitionVersion { get; set; }

        /// <summary>Status als Zahl (indizierbar).</summary>
        public int Status { get; set; }

        /// <summary>Korrelationsschluessel, oder null.</summary>
        public string CorrelationKey { get; set; }

        /// <summary>Die Variablen als JSON (typerhaltend).</summary>
        public string VariablesJson { get; set; }

        /// <summary>Die Tokens als JSON.</summary>
        public string TokensJson { get; set; }

        /// <summary>Das Protokoll als JSON.</summary>
        public string HistoryJson { get; set; }

        /// <summary>Fehlermeldung bei Faulted, oder null.</summary>
        public string FaultMessage { get; set; }

        /// <summary>Erstellzeitpunkt (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Zeitpunkt der letzten Aenderung (UTC).</summary>
        public DateTime UpdatedUtc { get; set; }
    }

    /// <summary>
    /// Ein wartendes Token als eigene, abfragbare Zeile - der Index fuer die Signal- und
    /// Timer-Abfragen. Wird bei jedem Speichern der Instanz neu aufgebaut.
    /// </summary>
    public class WaitingTokenRow
    {
        /// <summary>Technischer Primaerschluessel.</summary>
        public long WaitingTokenRowId { get; set; }

        /// <summary>Id der zugehoerigen Instanz.</summary>
        public string InstanceId { get; set; }

        /// <summary>Korrelationsschluessel der Instanz (denormalisiert fuer die Abfrage).</summary>
        public string CorrelationKey { get; set; }

        /// <summary>Signalname bei einem Signal-Wartepunkt, oder null.</summary>
        public string WaitingSignal { get; set; }

        /// <summary>Faelligkeitszeitpunkt bei einem Timer, oder null.</summary>
        public DateTime? DueUtc { get; set; }
    }

    /// <summary>
    /// Persistierte Zeile einer Workflow-Definition (als JSON-Blob, Schluessel Id+Version).
    /// </summary>
    public class WorkflowDefinitionRow
    {
        /// <summary>Fachliche Id der Definition.</summary>
        public string Id { get; set; }

        /// <summary>Version der Definition.</summary>
        public int Version { get; set; }

        /// <summary>Die vollstaendige Definition als JSON.</summary>
        public string DefinitionJson { get; set; }
    }

    /// <summary>
    /// Der EF-Core-Kontext des Workflow-Stores. Provider-agnostisch: SqlServer, PostgreSql oder
    /// SQLite werden erst beim Bauen der <see cref="DbContextOptions"/> gewaehlt.
    /// </summary>
    public class WorkflowContext : DbContext
    {
        /// <summary>Initialisiert den Kontext.</summary>
        public WorkflowContext(DbContextOptions<WorkflowContext> options) : base(options)
        {
        }

        /// <summary>Die Workflow-Instanzen.</summary>
        public DbSet<WorkflowInstanceRow> WorkflowInstances { get; set; }

        /// <summary>Der Warte-Token-Index.</summary>
        public DbSet<WaitingTokenRow> WaitingTokens { get; set; }

        /// <summary>Die Workflow-Definitionen.</summary>
        public DbSet<WorkflowDefinitionRow> WorkflowDefinitions { get; set; }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<WorkflowInstanceRow>(e =>
            {
                e.HasKey(n => n.Id);
                e.HasIndex(n => n.Status);
                e.HasIndex(n => n.CorrelationKey);
            });

            modelBuilder.Entity<WaitingTokenRow>(e =>
            {
                e.HasKey(n => n.WaitingTokenRowId);
                e.HasIndex(n => n.InstanceId);
                e.HasIndex(n => n.WaitingSignal);
                e.HasIndex(n => n.DueUtc);
            });

            modelBuilder.Entity<WorkflowDefinitionRow>(e =>
            {
                e.HasKey(n => new { n.Id, n.Version });
            });
        }
    }
}
