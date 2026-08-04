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

        /// <summary>
        /// Der Verweis auf die Definitionszeile, mit der diese Instanz gestartet wurde
        /// (Fremdschluessel).
        /// </summary>
        /// <remarks>
        /// Bewusst der technische Schluessel und nicht Name+Version: eine Instanz eines OEFFENTLICHEN
        /// Workflows gehoert trotzdem ihrem Mandanten. Ueber den Namen waere ab dem Moment, in dem
        /// dieser Mandant eine eigene Fassung desselben Namens anlegt, nicht mehr entscheidbar, welche
        /// gemeint ist - und die laufende Instanz liefe still auf einem anderen Graphen weiter.
        /// </remarks>
        public int DefinitionKey { get; set; }

        /// <summary>Fachliche Id der Definition (Anzeige und Filter).</summary>
        public string DefinitionId { get; set; }

        /// <summary>Version der Definition (Anzeige und Filter).</summary>
        public int DefinitionVersion { get; set; }

        /// <summary>Status als Zahl (indizierbar).</summary>
        public int Status { get; set; }

        /// <summary>
        /// Die Dringlichkeit der Instanz (<b>kleinere Zahl = wichtiger</b>, siehe
        /// <c>WorkflowPriority</c>). Eigene Spalte statt Teil des JSON, weil der Aufgriff faelliger
        /// Instanzen danach sortiert - aus einem JSON-Blob liesse sich das nicht ordnen.
        /// </summary>
        public int Priority { get; set; } = Instances.WorkflowPriority.Normal;

        /// <summary>
        /// Name des Tenants, dem die Instanz gehoert, oder null fuer eine tenant-freie Instanz. Eine
        /// laufende Instanz ist strikt an ihren Tenant gebunden (anders als eine oeffentliche Definition).
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>Korrelationsschluessel, oder null.</summary>
        public string CorrelationKey { get; set; }

        /// <summary>Die Variablen als JSON (typerhaltend).</summary>
        public string VariablesJson { get; set; }

        /// <summary>
        /// Die zur Ruecknahme vorgemerkten Schritte als JSON - erledigte Schritte mit
        /// Rueckabwicklungs-Pfad, samt dem Variablen-Stand ihrer Vollendung.
        /// </summary>
        /// <remarks>
        /// Eine JSON-Spalte und keine eigene Tabelle: die Liste wird immer als GANZES gelesen (beim
        /// Rueckabwickeln) und nie einzeln abgefragt - ein Index darauf haette keinen Abnehmer.
        /// </remarks>
        public string CompensationsJson { get; set; }

        /// <summary>Fehlermeldung bei Faulted, oder null.</summary>
        public string FaultMessage { get; set; }

        /// <summary>Bei einem Subworkflow: die aufrufende (Eltern-)Instanz; sonst null (indiziert).</summary>
        public string ParentInstanceId { get; set; }

        /// <summary>Bei einem Subworkflow: das wartende Eltern-Token; sonst null.</summary>
        public string ParentTokenId { get; set; }

        /// <summary>
        /// Oberste Instanz des Prozessbaums (fuer die aggregierte Ansicht ueber Subworkflows hinweg);
        /// bei Top-Level = die eigene Id (indiziert).
        /// </summary>
        public string RootInstanceId { get; set; }

        /// <summary>Verschachtelungstiefe im Prozessbaum (0 = Top-Level).</summary>
        public int CallDepth { get; set; }

        /// <summary>
        /// Optimistischer Nebenlaeufigkeits-Zaehler (Concurrency-Token). Jeder Commit erhoeht ihn; ein
        /// nebenlaeufiger Commit gelingt nur, wenn der erwartete Wert noch passt.
        /// </summary>
        public int Version { get; set; }

        /// <summary>Erstellzeitpunkt (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Zeitpunkt der letzten Aenderung (UTC).</summary>
        public DateTime UpdatedUtc { get; set; }
    }

    /// <summary>
    /// Ein Token (Zweig) einer Instanz als eigene, unabhaengig schreibbare Zeile - die Grundlage der
    /// nebenlaeufigen Zweig-Ausfuehrung: ein Zweig patcht nur seine eigene Zeile, ohne die Geschwister
    /// zu beruehren. Ersetzt zugleich den frueheren Warte-Token-Index: eine wartende Zeile
    /// (<see cref="Status"/> = Waiting) mit <see cref="WaitingSignal"/>/<see cref="DueUtc"/> IST der
    /// Index fuer die Signal- und Timer-Abfragen.
    /// </summary>
    public class TokenRow
    {
        /// <summary>Id der zugehoerigen Instanz (Teil des Schluessels).</summary>
        public string InstanceId { get; set; }

        /// <summary>Instanz-eindeutige Token-Id (Teil des Schluessels).</summary>
        public string TokenId { get; set; }

        /// <summary>Knoten, auf dem das Token steht.</summary>
        public string NodeId { get; set; }

        /// <summary>Token-Status als Zahl (indizierbar).</summary>
        public int Status { get; set; }

        /// <summary>Signalname bei einem Signal-Wartepunkt, oder null.</summary>
        public string WaitingSignal { get; set; }

        /// <summary>Faelligkeitszeitpunkt bei einem Timer, oder null.</summary>
        public DateTime? DueUtc { get; set; }

        /// <summary>
        /// Ausfuehrungs-Ziel bei einem auf einen fremden Host wartenden Zweig (Status
        /// <see cref="Instances.TokenStatus.WaitingForTarget"/>), oder null. Indiziert - IST der
        /// Abfrage-Index fuer den verteilten Handoff (<c>FindBranchesWaitingForTarget</c>).
        /// </summary>
        public string WaitingTarget { get; set; }

        /// <summary>
        /// Bei einem an einem <c>CallWorkflowNode</c> wartenden Token: die Id der Subworkflow-Instanz, auf
        /// deren Ende gewartet wird; sonst null.
        /// </summary>
        public string WaitingForChildInstanceId { get; set; }

        /// <summary>
        /// Der <b>Zweig-Scope</b> als JSON (die eigene Variablen-Kopie eines parallelen Zweigs), oder null,
        /// wenn das Token auf dem Instanz-Scope arbeitet - dem Normalfall ausserhalb paralleler Regionen.
        /// </summary>
        public string VariablesJson { get; set; }

        /// <summary>
        /// Die Zweig-Herkunft: die Token-Id des AND-Splits, aus dem dieses Token hervorgegangen ist; sonst
        /// null. Der Join findet darueber den Scope, in den er zusammenfuehrt.
        /// </summary>
        public string SplitTokenId { get; set; }

        /// <summary>
        /// Bei einem Zweig aus einem inklusiven Gateway: wie viele Zweige dieser Split aktiviert hat;
        /// sonst null. Der zugehoerige Join zaehlt dagegen.
        /// </summary>
        public int? SplitBranchCount { get; set; }

        /// <summary>
        /// Bei einem Token, das zu einem Fristen-Timer am Schritt gehoert (wartender Timer ODER laufender
        /// Nebenpfad): die Id des Haupt-Tokens, an dessen Schritt der Timer haengt; sonst null. Verlaesst
        /// das Haupt-Token seinen Schritt, werden alle Tokens mit dieser Id verbraucht.
        /// </summary>
        public string BoundaryOwnerTokenId { get; set; }

        /// <summary>
        /// Beim wartenden Timer-Token: wie oft bereits ausgeloest wurde. Bestimmt das naechste Intervall.
        /// </summary>
        public int? BoundaryIteration { get; set; }

        /// <summary>
        /// Der Tenant der zugehoerigen Instanz - <b>denormalisiert</b>. Die Token-Zeile selbst hat keinen
        /// Query-Filter; ohne diese Spalte gaebe es kein serverseitiges Filtern/Sortieren/Paginieren einer
        /// Arbeitsliste ueber alle Instanzen hinweg (nur einen Join, den kein Index traegt).
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// Bei einer wartenden Benutzer-Aufgabe: die Aufgabenart; sonst null. Zugleich das Kennzeichen,
        /// dass diese Zeile eine offene Aufgabe IST.
        /// </summary>
        public string TaskKey { get; set; }

        /// <summary>Die Permission, die diese Aufgabe verlangt; null = nur das allgemeine Aufgaben-Recht.</summary>
        public string TaskPermission { get; set; }

        /// <summary>Der Zustaendige (Ergebnis des Zuweisungs-Ausdrucks), oder null fuer eine Pool-Aufgabe.</summary>
        public string AssignedTo { get; set; }

        /// <summary>Der Titel fuer die Arbeitsliste - unaufgeloest (Klartext oder Kultur-JSON).</summary>
        public string TaskTitle { get; set; }

        /// <summary>Wann die Aufgabe entstanden ist (UTC) - das Sortierkriterium der Arbeitsliste.</summary>
        public DateTime? TaskCreatedUtc { get; set; }

        /// <summary>Die Frist der Aufgabe (UTC), oder null. Reine Anzeige-/Sortierinformation.</summary>
        public DateTime? TaskDueUtc { get; set; }

        /// <summary>
        /// Die <b>weiche Sperre</b>: wer die Aufgabe gerade offen hat. Sie blockiert nicht - die harte
        /// Entscheidung faellt weiterhin am Versionsvergleich des Abschlusses - sondern warnt den zweiten
        /// Bearbeiter, bevor er die Arbeit doppelt macht.
        /// </summary>
        /// <remarks>
        /// Bewusst hier und nicht in <see cref="WorkflowBranchLockRow"/>: dessen
        /// <c>ReleaseLocksOfOwner</c> raeumt die Sperren eines Runners auf und wuerde einen
        /// Oberflaechen-Claim mitreissen.
        /// </remarks>
        public string ClaimedBy { get; set; }

        /// <summary>Bis wann die weiche Sperre gilt (UTC). Danach gilt die Aufgabe als frei.</summary>
        public DateTime? ClaimedUntil { get; set; }

        /// <summary>
        /// Der <b>Timer-Anspruch</b>: welcher Runner diesen faelligen Timer gerade aufgegriffen hat, in
        /// der Form <c>&lt;Owner&gt;#&lt;Aufruf-Guid&gt;</c>; sonst null.
        /// </summary>
        /// <remarks>
        /// Zwei Angaben in einer Spalte, beide gebraucht: der <b>Owner</b>-Teil, damit
        /// <c>ReleaseLocksOfOwner</c> die Ansprueche eines neu gestarteten Runners findet; die
        /// <b>Aufruf-Guid</b>, damit ein Aufgriff nach dem Stempeln exakt zurueckliest, welche Zeilen ER
        /// bekommen hat - der blosse Owner-Name genuegt dafuer nicht, derselbe Runner kann noch Zeilen
        /// aus einem frueheren, gescheiterten Durchgang halten.
        /// <para>
        /// Bewusst nicht <see cref="ClaimedBy"/> mitbenutzt: das ist die weiche Sperre der
        /// <b>Oberflaeche</b> auf einer Benutzer-Aufgabe. Beide sassen auf derselben Zeile und wuerden
        /// sich gegenseitig ueberschreiben.
        /// </para>
        /// </remarks>
        public string TimerLeaseOwner { get; set; }

        /// <summary>
        /// Bis wann der Timer-Anspruch gilt (UTC). Danach darf ihn ein anderer Runner uebernehmen - das
        /// ist die Selbstheilung fuer einen mittendrin abgestuerzten Halter.
        /// </summary>
        public DateTime? TimerLeaseUntilUtc { get; set; }

        /// <summary>
        /// Bei einem Token, das an einem ereignisbasierten Gateway um die Wette wartet: die Id des
        /// Gateway-Tokens (alle Geschwister desselben Rennens teilen sie); sonst null.
        /// </summary>
        public string RaceTokenId { get; set; }

        /// <summary>
        /// Der Korrelationsschluessel DIESES Wartepunkts (aus dem Ausdruck des Wartepunkt-Knotens), oder
        /// null - dann gilt der Schluessel der Instanz.
        /// </summary>
        public string WaitingCorrelation { get; set; }

        /// <summary>
        /// Die Art des Wartepunkts als Zahl (<c>WaitKind</c>: 0 = gerichtete Nachricht, 1 = Rundruf),
        /// oder null bei allen anderen Wartearten.
        /// </summary>
        /// <remarks>
        /// Als Spalte und nicht nur im Modell, damit ein Rundruf seine Empfaenger in der DATENBANK
        /// auswaehlen kann - sonst muesste er fuer jede wartende Instanz erst deren Definition laden, nur
        /// um die Art des Wartepunkts zu erfahren.
        /// </remarks>
        public int? WaitingKind { get; set; }

        /// <summary>
        /// Ueber welche Kante das Token an seinem aktuellen Knoten angekommen ist; null bei einem
        /// Start-Token. Der Join prueft damit je EINGEHENDER KANTE statt nur die Anzahl.
        /// </summary>
        public string ArrivedViaFlowId { get; set; }

        /// <summary>
        /// Bei einem Token im Innenraum eines eingebetteten Subprozesses: die Id des aeusseren, am
        /// Subprozess-Knoten wartenden Tokens; sonst null.
        /// </summary>
        public string SubProcessOwnerTokenId { get; set; }

        /// <summary>
        /// Bei einem Token, das gerade einen Rueckabwicklungs-Pfad laeuft: die Id des Tokens, das am
        /// Ausloeser darauf wartet; sonst null.
        /// </summary>
        public string CompensationOwnerTokenId { get; set; }
    }

    /// <summary>
    /// Ein Protokolleintrag einer Instanz als eigene Zeile (append-only) - loest den frueheren
    /// HistoryJson-Blob ab: ein Zweig-Commit fuegt nur SEINE neuen Eintraege ein, statt das ganze
    /// (wachsende) Protokoll bei jedem Commit neu zu serialisieren. So bleibt der Schreibaufwand pro
    /// Commit konstant und das Protokoll wird filter-/abfragbar (Severity, Knoten, Zeit).
    /// </summary>
    public class HistoryEntryRow
    {
        /// <summary>Fortlaufender Schluessel (identitaets-vergeben) - zugleich globale Einfuege-Reihenfolge.</summary>
        public long Id { get; set; }

        /// <summary>Id der Instanz, zu der der Eintrag gehoert.</summary>
        public string InstanceId { get; set; }

        /// <summary>
        /// Id der obersten Instanz des Prozessbaums. Solange es keinen Eltern-Workflow gibt, ist das die
        /// eigene <see cref="InstanceId"/>. Denormalisiert, damit sich das Protokoll eines GESAMTEN
        /// Prozessbaums (Eltern + Subworkflows) mit EINER indizierten Abfrage lesen laesst.
        /// </summary>
        public string RootInstanceId { get; set; }

        /// <summary>Instanz-interne, fortlaufende Reihenfolge (0-basiert) - fuer das Anhaengen nur neuer Eintraege.</summary>
        public int Seq { get; set; }

        /// <summary>Zeitpunkt des Ereignisses (UTC).</summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>Betroffener Knoten, oder null bei instanzweiten Ereignissen.</summary>
        public string NodeId { get; set; }

        /// <summary>Kurzbezeichnung des Ereignisses.</summary>
        public string Event { get; set; }

        /// <summary>Freitext-Detail, oder null.</summary>
        public string Detail { get; set; }

        /// <summary>Schweregrad als Zahl (indizierbar) - siehe <see cref="Instances.HistorySeverity"/>.</summary>
        public int Severity { get; set; }
    }

    /// <summary>
    /// Persistierte Zeile einer Workflow-Definition (als JSON-Blob, Schluessel Id+Version).
    /// </summary>
    public class WorkflowDefinitionRow
    {
        /// <summary>
        /// Der technische Primaerschluessel dieser Zeile (von der Datenbank vergeben).
        /// </summary>
        /// <remarks>
        /// Frueher war der Schluessel (Id, Version). Das ging nicht auf: die Eindeutigkeit gilt <b>je
        /// Mandant</b>, und der Mandant kann null sein (= oeffentlich) - eine NULL-Spalte darf in
        /// keinem Primaerschluessel stehen. Zwei Mandanten konnten deshalb nicht dieselbe fachliche Id
        /// benutzen. Jetzt traegt ein eindeutiger INDEX (TenantId, Id, Version) die Regel, und der
        /// Schluessel ist das, worauf eine Instanz verweisen kann.
        /// </remarks>
        public int DefinitionKey { get; set; }

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
    /// Eine <b>vorgemerkte, noch nicht zugestellte Nachricht</b> - geschrieben im selben Commit wie der
    /// Zweig, der sie ausgeloest hat.
    /// </summary>
    /// <remarks>
    /// Genau darin liegt die Zustell-Garantie: eine Zustellung, die nur im Speicher vorgemerkt ist,
    /// verschwindet mit dem Prozess. Als Zeile in derselben Transaktion ueberlebt sie ihn, und ein
    /// Runner holt sie nach. Der Regelfall bleibt trotzdem die unmittelbare Zustellung durch den Sender
    /// selbst - was hier liegen bleibt, ist der Absturzfall.
    /// </remarks>
    public class WorkflowOutboxRow
    {
        /// <summary>Die sendende Instanz (Teil des Schluessels).</summary>
        public string InstanceId { get; set; }

        /// <summary>Kennung der Vormerkung (Teil des Schluessels).</summary>
        public string Id { get; set; }

        /// <summary>Der Signalname.</summary>
        public string SignalName { get; set; }

        /// <summary>Der Korrelationsschluessel, oder null.</summary>
        public string CorrelationKey { get; set; }

        /// <summary>Rundruf statt gerichteter Nachricht.</summary>
        public bool Broadcast { get; set; }

        /// <summary>Die ausdrueckliche Zielinstanz, oder null.</summary>
        public string TargetInstanceId { get; set; }

        /// <summary>Die Nutzdaten als JSON (typerhaltend), oder null.</summary>
        public string PayloadJson { get; set; }

        /// <summary>Das wartende Token des Senders, oder null.</summary>
        public string WaitingTokenId { get; set; }

        /// <summary>Die Variable fuer die Zahl der erreichten Empfaenger, oder null.</summary>
        public string ReachedVariable { get; set; }

        /// <summary>
        /// Der Tenant der sendenden Instanz - <b>denormalisiert</b>, damit der Nachhol-Lauf eines
        /// tenant-uebergreifenden Runners die Zeile ohne Join findet.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>Wann vorgemerkt (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Wie oft bereits versucht.</summary>
        public int Attempts { get; set; }

        /// <summary>Wer die Zustellung gerade nachholt, oder null.</summary>
        public string ClaimedBy { get; set; }

        /// <summary>Bis wann der Anspruch gilt, oder null.</summary>
        public DateTime? ClaimedUntil { get; set; }
    }

    /// <summary>
    /// Eine gehaltene Zweig-Sperre (prozessuebergreifender Ausschluss beim Vortrieb eines Zweigs).
    /// Reine Koordinations-Zeile ohne TTL: gilt, bis sie freigegeben oder ueber den Owner-Namen
    /// zurueckgesetzt wird. Bewusst OHNE Tenant-Filter - ein Runner sperrt tenant-uebergreifend.
    /// </summary>
    public class WorkflowBranchLockRow
    {
        /// <summary>Die gesperrte Instanz (Teil des Schluessels).</summary>
        public string InstanceId { get; set; }

        /// <summary>Das gesperrte Token/der Zweig (Teil des Schluessels).</summary>
        public string TokenId { get; set; }

        /// <summary>Der Besitzer der Sperre (stabiler Runner-Name).</summary>
        public string Owner { get; set; }

        /// <summary>Zeitpunkt des Erwerbs (UTC) - nur informativ; die Sperre hat keine TTL.</summary>
        public DateTime AcquiredUtc { get; set; }
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

        /// <summary>Die Tokens aller Instanzen (je Token eine Zeile).</summary>
        public DbSet<TokenRow> Tokens { get; set; }

        /// <summary>Die Protokolleintraege aller Instanzen (je Eintrag eine Zeile, append-only).</summary>
        public DbSet<HistoryEntryRow> HistoryEntries { get; set; }

        /// <summary>Die Workflow-Definitionen.</summary>
        public DbSet<WorkflowDefinitionRow> WorkflowDefinitions { get; set; }

        /// <summary>Die gehaltenen Zweig-Sperren (Koordination, ohne Tenant-Filter).</summary>
        public DbSet<WorkflowBranchLockRow> BranchLocks { get; set; }

        /// <summary>Die vorgemerkten, noch nicht zugestellten Nachrichten.</summary>
        public DbSet<WorkflowOutboxRow> Outbox { get; set; }

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
                e.HasIndex(n => n.ParentInstanceId);   // Kinder einer Instanz (Abbruch-Kaskade, Baum-Treiber)
                e.HasIndex(n => n.RootInstanceId);      // aggregierte Prozessbaum-Ansicht
                // "die dringendsten der lauffaehigen zuerst" - die Sortierung des Aufgriffs.
                e.HasIndex(n => new { n.Status, n.Priority });
                e.HasIndex(n => n.DefinitionKey);
                // Echter Fremdschluessel: eine Instanz ohne ihre Definition ist nicht ausfuehrbar. Kein
                // Kaskaden-Loeschen - eine Definition, an der noch Instanzen haengen, soll sich NICHT
                // nebenbei loeschen lassen.
                e.HasOne<WorkflowDefinitionRow>()
                    .WithMany()
                    .HasForeignKey(n => n.DefinitionKey)
                    .OnDelete(DeleteBehavior.Restrict);
                // Der Standard gehoert in die SPALTE, nicht nur ins Modell: eine Zeile, die von aussen
                // (Alt-Bestand, Migration, Reparatur-SQL) ohne Priority entsteht, waere sonst 0 - und 0
                // ist die HOECHSTE Stufe. Ausgerechnet die Alt-Instanzen wuerden alles ueberholen.
                e.Property(n => n.Priority).HasDefaultValue(Instances.WorkflowPriority.Normal);
                // Optimistische Nebenlaeufigkeit: die UPDATE-Klausel enthaelt Version=@original;
                // ein zwischenzeitlicher Commit laesst 0 Zeilen zu -> DbUpdateConcurrencyException.
                e.Property(n => n.Version).IsConcurrencyToken();
            });

            modelBuilder.Entity<TokenRow>(e =>
            {
                e.HasKey(n => new { n.InstanceId, n.TokenId });
                e.HasIndex(n => n.InstanceId);
                e.HasIndex(n => n.Status);
                e.HasIndex(n => n.WaitingSignal);
                e.HasIndex(n => n.DueUtc);
                // Die Zustellung sucht nach Name UND Art (gerichtete Nachricht gegen Rundruf) - ein
                // Rundruf traefe sonst erst nach dem Laden aller gleichnamig Wartenden seine Auswahl.
                e.HasIndex(n => new { n.WaitingSignal, n.WaitingKind });
                // Der Korrelationsschluessel des Wartepunkts: die zweite Art, einen Empfaenger zu finden.
                e.HasIndex(n => n.WaitingCorrelation);
                e.HasIndex(n => n.WaitingTarget);
                // DER Abfrage-Index der Arbeitsliste: "offene Aufgaben dieses Tenants, ggf. einer Art".
                // Status steht hinten, weil er die geringste Trennschaerfe hat (jede Liste sucht Waiting).
                e.HasIndex(n => new { n.TenantId, n.TaskKey, n.Status });
                // "meine Aufgaben" - der haeufigste Filter der Liste.
                e.HasIndex(n => n.AssignedTo);
            });

            modelBuilder.Entity<HistoryEntryRow>(e =>
            {
                e.HasKey(n => n.Id);
                e.HasIndex(n => n.InstanceId);
                e.HasIndex(n => n.RootInstanceId);   // aggregierte Prozessbaum-Ansicht (Eltern + Subworkflows)
                e.HasIndex(n => new { n.InstanceId, n.Seq });
                e.HasIndex(n => n.Severity);
            });

            modelBuilder.Entity<WorkflowDefinitionRow>(e =>
            {
                e.HasKey(n => n.DefinitionKey);
                e.Property(n => n.DefinitionKey).ValueGeneratedOnAdd();
                // Ohne den PK war die Spalte nur deshalb NOT NULL, weil sie Teil des Schluessels war.
                e.Property(n => n.Id).IsRequired();
                e.HasIndex(n => n.TenantId);
                // Die eigentliche fachliche Regel: je Mandant (bzw. einmal oeffentlich) genau eine
                // Definition mit dieser Id und Version. Als Primaerschluessel nicht formulierbar, weil
                // TenantId nullable ist.
                //
                // ACHTUNG, provider-abhaengig: SQL Server behandelt NULLs im eindeutigen Index als
                // GLEICH (genau richtig - hoechstens eine oeffentliche je Id/Version), PostgreSQL
                // standardmaessig als VERSCHIEDEN. Fuer PostgreSQL setzt die Migration deshalb
                // NULLS NOT DISTINCT; ohne das waere der Schutz dort stillschweigend wirkungslos.
                //
                // HasFilter(null) ist hier KEIN Detail: SQL Server haengt an einen eindeutigen Index
                // ueber nullable Spalten von selbst ein "WHERE TenantId IS NOT NULL" - und schloesse
                // damit ausgerechnet die OEFFENTLICHEN Definitionen von der Pruefung aus, also genau
                // den Fall, den es zu schuetzen gilt. Ohne Filter zaehlen NULLs bei SQL Server als
                // gleich: hoechstens eine oeffentliche je Id/Version. Genau das ist gemeint.
                e.HasIndex(n => new { n.TenantId, n.Id, n.Version }).IsUnique().HasFilter(null);
            });

            modelBuilder.Entity<WorkflowOutboxRow>(e =>
            {
                e.HasKey(n => new { n.InstanceId, n.Id });
                // Der Nachhol-Lauf sucht nach freien bzw. abgelaufenen Anspruechen - danach ist zu
                // indizieren, nicht nach der Instanz.
                e.HasIndex(n => n.ClaimedUntil);
                e.HasIndex(n => n.CreatedUtc);
            });

            modelBuilder.Entity<WorkflowBranchLockRow>(e =>
            {
                // Zusammengesetzter Schluessel (Instanz, Token) = der atomare Erwerbs-Punkt: ein
                // zweiter INSERT desselben Zweigs verletzt den PK -> Contention. Index auf Owner fuer
                // das Zuruecksetzen beim Runner-Neustart.
                e.HasKey(n => new { n.InstanceId, n.TokenId });
                e.HasIndex(n => n.Owner);
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
