using System;
using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.EFRepo.Options;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

        /// <summary>
        /// Name des Tenants, dem die Instanz gehoert, oder null fuer eine tenant-freie Instanz. Eine
        /// laufende Instanz ist strikt an ihren Tenant gebunden (anders als eine oeffentliche Definition).
        /// </summary>
        public string TenantId { get; set; }

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

        /// <summary>
        /// Name des Tenants, dem die Definition gehoert, oder null fuer eine oeffentliche Definition.
        /// Oeffentliche Definitionen sind fuer alle Tenants sichtbar und koennen im Kontext eines
        /// beliebigen Tenants gestartet werden.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>Die vollstaendige Definition als JSON.</summary>
        public string DefinitionJson { get; set; }
    }

    /// <summary>
    /// Der EF-Core-Kontext des Workflow-Stores. Provider-agnostisch: SqlServer, PostgreSql oder
    /// SQLite werden erst beim Bauen der <see cref="DbContextOptions"/> gewaehlt.
    /// </summary>
    /// <remarks>
    /// Plugin- und tenant-faehig nach dem Vorbild des Toolkit-<c>TaskSchedulerContext</c>: als
    /// <see cref="ScopedDependencyAttribute">ScopedDependency</see> und <see cref="IPlugin"/> laesst
    /// er sich per WebPluginHelper (Web) ODER in einem Service laden. Den aktiven Tenant liest er aus
    /// einem injizierten <see cref="IUserAwareContext"/> (typischerweise der Security-Context) - nicht
    /// aus dem Web-Stack direkt. Der options-only-Ctor (Migrationen/Tests) laesst die Tenant-Filter aus.
    /// </remarks>
    [ScopedDependency(FriendlyName = "WorkflowContext")]
    public class WorkflowContext : DbContext, IPlugin
    {
        private readonly DbContextModelBuilderOptions<WorkflowContext> modelOptions;
        private readonly IUserAwareContext userContext;

        /// <summary>
        /// Options-only-Ctor fuer Migrationen/Design-Time/Tests. Es wird KEIN Tenant-Filter gesetzt -
        /// alle Zeilen sind sichtbar.
        /// </summary>
        public WorkflowContext(DbContextOptions<WorkflowContext> options) : base(options)
        {
        }

        /// <summary>
        /// Basis-Ctor mit Model-Optionen und Tenant-Quelle. Wird vom Plugin-Ctor genutzt.
        /// </summary>
        public WorkflowContext(DbContextOptions options, DbContextModelBuilderOptions<WorkflowContext> modelOptions,
            IUserAwareContext userContext) : base(options)
        {
            this.modelOptions = modelOptions;
            this.userContext = userContext;
        }

        /// <summary>
        /// Plugin-/Laufzeit-Ctor: die Options kommen ueber einen <see cref="ContextOptionsLoader{TContext}"/>
        /// (Provider-Wahl im Host), der Tenant ueber den injizierten <see cref="IUserAwareContext"/>.
        /// <paramref name="useTenantFilter"/> schaltet die Tenant-Schicht.
        /// </summary>
        public WorkflowContext(ContextOptionsLoader<WorkflowContext> dbOptions, IUserAwareContext userContext,
            bool useTenantFilter, IOptions<DbContextModelBuilderOptions<WorkflowContext>> modelOptions)
            : this(dbOptions.Options, modelOptions.Value, userContext)
        {
            UseTenantFilter = useTenantFilter;
            this.modelOptions.ConfigureExpressionProperty(() => CurrentTenant);
        }

        /// <summary>Schaltet die tenant-abhaengige Filterung.</summary>
        public bool UseTenantFilter { get; set; }

        /// <summary>
        /// Der aktuell aktive Tenant. Speist die globalen Query-Filter und das Stempeln neuer Zeilen.
        /// </summary>
        /// <remarks>
        /// Vorrang hat der ambiente <see cref="WorkflowExecutionScope"/>: setzt der tenant-uebergreifende
        /// Runner beim Vortrieb einer Instanz deren Tenant, gilt dieser (auch „bewusst tenant-frei" =
        /// null) - unabhaengig vom injizierten Benutzer-Kontext, den es im Dienst gar nicht gibt. Ist
        /// kein Scope aktiv (Web-Betrieb), gilt wie bisher der injizierte <see cref="IUserAwareContext"/>,
        /// und nur wenn der Filter eingeschaltet ist.
        /// </remarks>
        public string CurrentTenant => WorkflowExecutionScope.HasTenant
            ? WorkflowExecutionScope.CurrentTenant
            : (UseTenantFilter ? userContext?.CurrentTenant : null);

        /// <inheritdoc/>
        public string UniqueName { get; set; }

        /// <inheritdoc/>
        public event EventHandler Disposed;

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
                e.HasIndex(n => n.TenantId);
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
                e.HasIndex(n => n.TenantId);
            });

            // Nur wenn der Plugin-Ctor Model-Optionen gesetzt hat, werden die tenant-abhaengigen
            // globalen Query-Filter angewendet. Der options-only-Pfad bleibt filterfrei.
            modelOptions?.ConfigureModelBuilder(modelBuilder);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            base.Dispose();
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
