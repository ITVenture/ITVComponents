using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.EFRepo.Options;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;

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
        /// Ob die Instanz <b>angehalten</b> ist: kein Runner greift sie auf. Eigene Spalte statt eines
        /// weiteren Status-Werts - der Status traegt den Lebenszyklus, und der wird im Vortrieb staendig
        /// auf "laeuft" zurueckgesetzt.
        /// </summary>
        public bool Suspended { get; set; }

        /// <summary>Warum angehalten wurde, oder null.</summary>
        public string SuspendedReason { get; set; }

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

        /// <summary>
        /// Der Fehler-Code bei Faulted (Fehlerart), oder null - das, worauf ein aufrufender Prozess
        /// verzweigt, statt die Meldung zu parsen.
        /// </summary>
        public string FaultCode { get; set; }

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
    /// Die <b>Beschreibung</b> eines Anhangs an einem Vorgang - Name, Groesse, wer und wann. Der Inhalt
    /// liegt beim <c>IWorkflowAttachmentStore</c> und wird ueber <see cref="FileIdentifier"/> gefunden.
    /// </summary>
    /// <remarks>
    /// Die Trennung ist der Punkt: eine Liste von Anhaengen zeigt Namen und Groessen, und die soll nicht
    /// die Dateien selbst aus der Datenbank ziehen. Ausserdem laesst sich die Ablage so austauschen, ohne
    /// dass die Beschreibung wandert.
    /// </remarks>
    public class WorkflowAttachmentRow
    {
        /// <summary>Der technische Schluessel dieser Zeile.</summary>
        public int AttachmentKey { get; set; }

        /// <summary>Der Vorgang, an dem der Anhang haengt.</summary>
        public string InstanceId { get; set; }

        /// <summary>Die Aufgabe, bei der er entstanden ist, oder null - nur nachrichtlich.</summary>
        public string TokenId { get; set; }

        /// <summary>Der Mandant des Vorgangs (denormalisiert, wie bei den Kommentaren).</summary>
        public string TenantId { get; set; }

        /// <summary>Der Dateiname, wie ihn der Hochladende kannte.</summary>
        public string FileName { get; set; }

        /// <summary>Der Inhaltstyp, oder null.</summary>
        public string ContentType { get; set; }

        /// <summary>Die Groesse in Bytes - fuer die Anzeige, ohne die Datei zu laden.</summary>
        public long SizeBytes { get; set; }

        /// <summary>Wer hochgeladen hat.</summary>
        public string Author { get; set; }

        /// <summary>Wann (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Die Kennung, unter der die Ablage den Inhalt fuehrt.</summary>
        public string FileIdentifier { get; set; }
    }

    /// <summary>
    /// Der <b>Inhalt</b> eines Anhangs in der eingebauten Ablage.
    /// </summary>
    /// <remarks>
    /// Eigene Tabelle und nicht eine Spalte an der Beschreibung: so laedt eine Liste von Anhaengen
    /// garantiert keine Bytes mit, auch wenn jemand die Projektion vergisst. Wer eine andere Ablage
    /// registriert, laesst diese Tabelle schlicht leer.
    /// </remarks>
    public class WorkflowAttachmentBlobRow
    {
        /// <summary>Die Kennung (Primaerschluessel) - was die Beschreibung als Verweis traegt.</summary>
        public string FileIdentifier { get; set; }

        /// <summary>Der Inhaltstyp, oder null.</summary>
        public string ContentType { get; set; }

        /// <summary>Der vorgeschlagene Dateiname, oder null.</summary>
        public string DownloadName { get; set; }

        /// <summary>Die Bytes.</summary>
        public byte[] Content { get; set; }
    }

    /// <summary>
    /// Ein <b>Kommentar</b> an einem Vorgang - die Rueckfrage, der Vermerk, die Begruendung.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Er haengt am VORGANG und nur nachrichtlich an der Aufgabe, bei der er entstanden ist. Das ist der
    /// Punkt: eine Aufgabe verschwindet mit ihrem Abschluss, der Gespraechsfaden soll aber bleiben - wer
    /// spaeter fragt, warum so entschieden wurde, sucht am Vorgang und nicht an einem Schritt, den es
    /// nicht mehr gibt.
    /// </para>
    /// <para>
    /// Die Engine kennt Kommentare <b>nicht</b> - genau wie die weiche Sperre. Ein Kommentar ist kein
    /// Prozess-Zustand: kein Ablauf haengt von ihm ab, keine Bedingung liest ihn. Waere er Teil der
    /// Instanz, reiste er durch jeden Zweig-Commit und muesste bei jedem Versionskonflikt mit
    /// zusammengefuehrt werden - fuer etwas, das niemand auswertet.
    /// </para>
    /// </remarks>
    public class WorkflowCommentRow
    {
        /// <summary>Der technische Schluessel dieser Zeile (von der Datenbank vergeben).</summary>
        public int CommentKey { get; set; }

        /// <summary>Der Vorgang, zu dem der Kommentar gehoert.</summary>
        public string InstanceId { get; set; }

        /// <summary>
        /// Die Aufgabe, bei der er entstanden ist, oder null (aus dem Monitoring heraus geschrieben).
        /// Nur nachrichtlich - der Kommentar bleibt sichtbar, wenn die Aufgabe laengst erledigt ist.
        /// </summary>
        public string TokenId { get; set; }

        /// <summary>
        /// Der Mandant des Vorgangs - <b>denormalisiert</b>, damit die Anzeige ohne Join filtern kann
        /// (dieselbe Ueberlegung wie bei der Outbox und den Ausloesern).
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>Wer den Kommentar geschrieben hat.</summary>
        public string Author { get; set; }

        /// <summary>Wann (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Der Text.</summary>
        public string Text { get; set; }
    }

    /// <summary>
    /// Ein <b>Ausloeser</b>: die abfragbare Form dessen, was ein Start-Knoten ueber seinen Einstieg
    /// deklariert hat ("horcht auf diese Nachricht", "laeuft nach diesem Zeitplan").
    /// </summary>
    /// <remarks>
    /// Die Wahrheit steht in der Definition; diese Zeile wird beim Speichern daraus neu aufgebaut. Sie
    /// existiert, weil die beiden Fragen dahinter nur als Abfrage zu beantworten sind: bei jeder
    /// eintreffenden Nachricht saemtliche Definitions-JSONs auszupacken, waere eine Last, die mit der Zahl
    /// der Prozesse waechst - und "welcher Zeitplan ist jetzt faellig?" liesse sich gar nicht indizieren.
    /// </remarks>
    public class WorkflowStartTriggerRow
    {
        /// <summary>Der technische Schluessel dieser Zeile (von der Datenbank vergeben).</summary>
        public int TriggerKey { get; set; }

        /// <summary>Die Definition, die gestartet wird - ihr technischer Schluessel.</summary>
        public int DefinitionKey { get; set; }

        /// <summary>Die fachliche Id der Definition - denormalisiert fuer Meldungen und Diagnose.</summary>
        public string DefinitionId { get; set; }

        /// <summary>Die Version der Definition, aus der dieser Ausloeser stammt.</summary>
        public int DefinitionVersion { get; set; }

        /// <summary>
        /// Der Mandant der Definition. <b>Denormalisiert</b> - aus demselben Grund wie bei der Outbox: der
        /// Aufgriff laeuft mandantenuebergreifend und soll die Zeile ohne Join finden.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>Der Start-Knoten, an dem der Ausloeser deklariert ist.</summary>
        public string NodeId { get; set; }

        /// <summary>Woraufhin er feuert (siehe <c>WorkflowStartTriggerKind</c>).</summary>
        public int Kind { get; set; }

        /// <summary>Bei einem Nachrichten-Ausloeser: der Name der Nachricht.</summary>
        public string SignalName { get; set; }

        /// <summary>Bei einem Nachrichten-Ausloeser: der Umgang mit laufenden Instanzen.</summary>
        public int Mode { get; set; }

        /// <summary>Bei einem Nachrichten-Ausloeser: Korrelationsschluessel der Nachricht uebernehmen?</summary>
        public bool AdoptCorrelationKey { get; set; }

        /// <summary>Bei einem Zeitplan: das Muster.</summary>
        public string Pattern { get; set; }

        /// <summary>Bei einem Zeitplan: die festen Startwerte als JSON, oder null.</summary>
        public string VariablesJson { get; set; }

        /// <summary>Bei einem Zeitplan: ueberspringen, solange der vorige Lauf laeuft.</summary>
        public bool SkipWhilePreviousRuns { get; set; }

        /// <summary>Bei einem Zeitplan: die naechste Faelligkeit (UTC), oder null.</summary>
        public DateTime? NextDueUtc { get; set; }

        /// <summary>Bei einem Zeitplan: wann zuletzt gestartet wurde (UTC), oder null.</summary>
        public DateTime? LastRunUtc { get; set; }

        /// <summary>Bei einem Zeitplan: die zuletzt gestartete Instanz, oder null.</summary>
        public string LastInstanceId { get; set; }

        /// <summary>
        /// Wer diesen faelligen Zeitplan gerade aufgegriffen hat, in der Form <c>owner#aufruf-guid</c> -
        /// dasselbe Verfahren wie beim Timer-Anspruch.
        /// </summary>
        public string LeaseOwner { get; set; }

        /// <summary>Bis wann der Anspruch gilt (UTC).</summary>
        public DateTime? LeaseUntilUtc { get; set; }
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
    public class WorkflowContext : DbContext, IPlugin, IUserAwareContext
    {
        private readonly DbContextModelBuilderOptions<WorkflowContext> modelOptions;

        private readonly IServiceProvider services;
        //private readonly IUserAwareContext userContext;

        private readonly IPermissionScope? scopeProvider;
        private readonly IContextUserProvider? userProvider;

        /// <summary>
        /// Options-only-Ctor fuer Migrationen/Design-Time/Tests. Es wird KEIN Tenant-Filter gesetzt -
        /// alle Zeilen sind sichtbar.
        /// </summary>
        /// <remarks>
        /// Die Markierung mit <see cref="ActivatorUtilitiesConstructorAttribute"/> ist kein Beiwerk: dieser
        /// Kontext hat mehrere Ctors, und <c>ActivatorUtilities</c> - das hinter
        /// <c>AddDbContextFactory&lt;WorkflowContext&gt;</c> und dem <c>dotnet ef</c>-Discovery steht -
        /// bricht bei Mehrdeutigkeit ab ("Multiple constructors accepting all given argument types") und
        /// nimmt den ganzen Host-Start mit. Die Markierung macht die Wahl eindeutig und trifft dabei die
        /// richtige: die Factory ist der <b>filterfreie</b> Weg (Runner, Inline-Ausfuehrung, Migrationen).
        /// <para>
        /// Wer den <b>tenant-faehigen</b> Kontext aus der DI will, registriert ihn mit einem eigenen
        /// Factory-Delegaten auf einen der beiden anderen Ctors - Auto-Wiring ueber
        /// <c>AddTransient&lt;WorkflowContext&gt;()</c> ist dafuer ausdruecklich nicht der Weg.
        /// </para>
        /// </remarks>
        [ActivatorUtilitiesConstructor]
        public WorkflowContext(DbContextOptions<WorkflowContext> options) : base(options)
        {
        }

        /// <summary>
        /// Basis-Ctor mit Model-Optionen und Tenant-Quelle. Wird vom Plugin-Ctor genutzt.
        /// </summary>
        public WorkflowContext(DbContextOptions options, DbContextModelBuilderOptions<WorkflowContext> modelOptions,
            IServiceProvider services) : base(options)
        {
            this.modelOptions = modelOptions;
            this.services = services;
            this.scopeProvider = services.GetService<IPermissionScope>();
            this.userProvider = services.GetService<IContextUserProvider>();
        }

        /// <summary>
        /// Plugin-/Laufzeit-Ctor: die Options kommen ueber einen <see cref="ContextOptionsLoader{TContext}"/>
        /// (Provider-Wahl im Host), der Tenant ueber den injizierten <see cref="IUserAwareContext"/>.
        /// <paramref name="useTenantFilter"/> schaltet die Tenant-Schicht.
        /// </summary>
        public WorkflowContext(ContextOptionsLoader<WorkflowContext> dbOptions, IServiceProvider services,
            bool useTenantFilter, IOptions<DbContextModelBuilderOptions<WorkflowContext>> modelOptions)
            : this(dbOptions.Options, modelOptions.Value, services)
        {
            UseTenantFilter = useTenantFilter;
            this.modelOptions.ConfigureExpressionProperty(() => CurrentTenant);
        }

        /// <summary>
        /// DI-Ctor: wie der Plugin-/Laufzeit-Ctor, aber mit dem Tenant-Schalter als aufloesbarem
        /// <see cref="WorkflowContextOptions"/> statt als <see cref="bool"/> - damit laesst sich der
        /// tenant-faehige Kontext ohne literales <c>true</c> im Registrierungs-Delegaten bauen und der
        /// Schalter aus der Konfiguration speisen.
        /// </summary>
        /// <remarks>
        /// Eine <b>Ergaenzung</b>, kein Ersatz: im Mehr-Instanzen-Betrieb (je Umgebung eine eigene
        /// scope-owned Kontext-Dependency) bleibt der bool-Ctor der praktischere Weg, weil dort ohnehin je
        /// Umgebung explizit gebaut wird. Diese Ueberladung leitet nur auf ihn um.
        /// <para>
        /// Fehlt die Registrierung von <c>IOptions&lt;WorkflowContextOptions&gt;</c>, gilt der Standard
        /// des Options-Typs (<see cref="WorkflowContextOptions.UseTenantFilter"/> = true).
        /// </para>
        /// </remarks>
        public WorkflowContext(ContextOptionsLoader<WorkflowContext> dbOptions, IServiceProvider services,
            IOptions<WorkflowContextOptions> tenantOptions,
            IOptions<DbContextModelBuilderOptions<WorkflowContext>> modelOptions)
            : this(dbOptions, services, tenantOptions?.Value?.UseTenantFilter ?? true, modelOptions)
        {
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
            : (UseTenantFilter ? scopeProvider?.PermissionPrefix : null);

        public string? CurrentUserName => userProvider?.User?.Identity?.Name;

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

        /// <summary>Die Ausloeser der Definitionen (Nachricht bzw. Zeitplan).</summary>
        public DbSet<WorkflowStartTriggerRow> WorkflowStartTriggers { get; set; }

        /// <summary>Die Kommentare an den Vorgaengen.</summary>
        public DbSet<WorkflowCommentRow> WorkflowComments { get; set; }

        /// <summary>Die Beschreibungen der Anhaenge.</summary>
        public DbSet<WorkflowAttachmentRow> WorkflowAttachments { get; set; }

        /// <summary>Die Inhalte der Anhaenge in der eingebauten Ablage.</summary>
        public DbSet<WorkflowAttachmentBlobRow> WorkflowAttachmentBlobs { get; set; }

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
                // "die dringendsten der lauffaehigen zuerst" - die Sortierung des Aufgriffs. Angehalten
                // steht mit drin, weil JEDE Aufgriffs-Abfrage danach filtert: ohne die Spalte im Index
                // liest der Aufgriff die angehaltenen Zeilen erst und wirft sie dann weg.
                e.HasIndex(n => new { n.Status, n.Suspended, n.Priority });
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

            modelBuilder.Entity<WorkflowAttachmentRow>(e =>
            {
                e.HasKey(n => n.AttachmentKey);
                // "die Anhaenge dieses Vorgangs, aelteste zuerst" - dieselbe Abfrage wie beim Faden.
                e.HasIndex(n => new { n.InstanceId, n.CreatedUtc });
                e.HasOne<WorkflowInstanceRow>()
                    .WithMany()
                    .HasForeignKey(n => n.InstanceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WorkflowAttachmentBlobRow>(e =>
            {
                e.HasKey(n => n.FileIdentifier);
                // BEWUSST kein Fremdschluessel auf die Beschreibung: die Ablage ist austauschbar, und der
                // eingebaute Blob-Speicher soll nicht die einzige moegliche Umsetzung zementieren. Das
                // Aufraeumen erledigt der Store beim Loeschen des Anhangs.
            });

            modelBuilder.Entity<WorkflowCommentRow>(e =>
            {
                e.HasKey(n => n.CommentKey);
                // DER Abfrage-Index: "der Faden dieses Vorgangs, neueste zuletzt".
                e.HasIndex(n => new { n.InstanceId, n.CreatedUtc });
                // Echter Fremdschluessel MIT Kaskade: ein Kommentar ohne seinen Vorgang ist nichts, was
                // jemand noch lesen wollte - er waere Muell, den niemand mehr zuordnen kann.
                e.HasOne<WorkflowInstanceRow>()
                    .WithMany()
                    .HasForeignKey(n => n.InstanceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WorkflowStartTriggerRow>(e =>
            {
                e.HasKey(n => n.TriggerKey);
                // DER Abfrage-Index der Zustellung: "wer horcht auf diesen Namen?". Die Art steht mit
                // drin, weil auf demselben Weg auch die Zeitplaene liegen - ohne sie liefe jede Nachricht
                // ueber alle Ausloeser.
                e.HasIndex(n => new { n.Kind, n.SignalName });
                // Der Aufgriff des Runners: faellige Zeitplaene, aelteste zuerst.
                e.HasIndex(n => new { n.Kind, n.NextDueUtc });
                // Der Neuaufbau beim Speichern einer Definition raeumt ueber diese beiden Spalten auf.
                e.HasIndex(n => new { n.TenantId, n.DefinitionId });
                // Echter Fremdschluessel MIT Kaskade - anders als bei der Instanz: ein Ausloeser ohne
                // seine Definition ist kein Verlust, sondern Muell, der sonst weiter feuern wuerde.
                e.HasOne<WorkflowDefinitionRow>()
                    .WithMany()
                    .HasForeignKey(n => n.DefinitionKey)
                    .OnDelete(DeleteBehavior.Cascade);
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
