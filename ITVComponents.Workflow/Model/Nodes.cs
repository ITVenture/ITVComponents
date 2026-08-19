using ITVComponents.Workflow.Expressions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ITVComponents.Workflow.Model
{
    /// <summary>
    /// Einstiegspunkt eines Workflows. Beim Start bekommt jeder Start-Knoten ein Token.
    /// </summary>
    public class StartNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Start;

        /// <summary>
        /// Die <b>Signatur</b> des Workflows: die deklarierten Start-Parameter. Die Bindungen werden
        /// beim Anlegen der Instanz gegen die <b>uebergebenen</b> Startvariablen aufgeloest (Konstante =
        /// Vorgabewert, Variable = umbenennen/durchreichen, Ausdruck = berechnen);
        /// <see cref="ActivityInputBinding.Parameter"/> ist der Name der zu setzenden Instanz-Variable.
        /// Leer = bisheriges Verhalten (die uebergebenen Variablen sind der Stack).
        /// </summary>
        /// <remarks>
        /// Gilt fuer JEDEN Einstieg in die Definition - direkter Start ebenso wie der Aufruf als
        /// Subworkflow (dort sind die "uebergebenen" Werte die Ausgaben der
        /// <see cref="CallWorkflowNode.Inputs"/> des Aufrufers).
        /// </remarks>
        public List<ActivityInputBinding> Inputs { get; set; } = new List<ActivityInputBinding>();

        /// <summary>
        /// Wie die Start-Parameter in den frischen Scope einfliessen. Standard
        /// <see cref="ActivityScopeMode.Extend"/> (additiv - uebergebene, nicht deklarierte Werte bleiben
        /// erhalten). <see cref="ActivityScopeMode.Replace"/> macht die Signatur <b>strikt</b>: der Stack
        /// besteht danach genau aus den deklarierten Parametern (plus <see cref="RetainVariables"/>),
        /// alles andere Uebergebene wird verworfen.
        /// </summary>
        public ActivityScopeMode ScopeMode { get; set; } = ActivityScopeMode.Extend;

        /// <summary>
        /// Bei <see cref="ActivityScopeMode.Replace"/>: Namen uebergebener Variablen, die trotz strikter
        /// Signatur erhalten bleiben (z.B. Korrelations-/Kontextwerte, die der Aufrufer mitgibt). Bei
        /// <see cref="ActivityScopeMode.Extend"/> ohne Wirkung.
        /// </summary>
        public List<string> RetainVariables { get; set; } = new List<string>();

        /// <summary>
        /// Die Deklaration der <b>Start-Maske</b>: was ein Mensch eingibt, wenn er diese Definition von
        /// Hand startet. Dieselbe Feldbeschreibung wie bei der Benutzer-Aufgabe
        /// (<see cref="UserActivityNode.FormFields"/>), damit es fuer "Formular aus Daten" nicht zwei
        /// Sprachen gibt. Der <see cref="UserTaskField.Name"/> ist der Name der <b>uebergebenen</b>
        /// Startvariable - also genau das, wogegen <see cref="Inputs"/> anschliessend aufgeloest wird.
        /// Leer = kein Formular; die Definition wird programmatisch (oder ohne Werte) gestartet.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Rein <b>beschreibend</b> und ausschliesslich fuer die Oberflaeche: die Engine liest die Felder
        /// nicht. Ein programmatischer Start bleibt unveraendert moeglich - er uebergibt die Werte direkt.
        /// Die Pflicht-Pruefung (<see cref="UserTaskField.Required"/>) ist damit eine Zusage der Maske, kein
        /// Engine-Vertrag; wer die Werte erzwingen will, deklariert sie zusaetzlich in <see cref="Inputs"/>.
        /// </para>
        /// <para>
        /// Zwei Eigenschaften des Feldes haben beim Start <b>keine</b> Bedeutung und werden ignoriert:
        /// <see cref="UserTaskField.ReadOnly"/> und <see cref="UserTaskField.PayloadName"/>. Beide beziehen
        /// sich auf den Payload einer laufenden Aufgabe - beim Start gibt es noch keinen.
        /// </para>
        /// </remarks>
        public List<UserTaskField> FormFields { get; set; } = new List<UserTaskField>();

        /// <summary>
        /// Optionale Anleitung ueber der Start-Maske ("Was starte ich hier eigentlich?"). Klartext ODER
        /// JSON-Objekt nach Kultur (<c>{"de":"...","fr":"..."}</c>) - dieselbe Konvention wie bei
        /// <see cref="UserActivityNode.Description"/>.
        /// </summary>
        public string FormDescription { get; set; }

        /// <summary>
        /// Optional: dieser Einstieg wird durch eine <b>eintreffende Nachricht</b> ausgeloest. Null
        /// (Standard) = die Definition wird nur von Hand oder programmatisch gestartet.
        /// </summary>
        public MessageStartTrigger MessageStart { get; set; }

        /// <summary>
        /// Optional: dieser Einstieg wird nach einem <b>Zeitplan</b> ausgeloest. Null (Standard) = kein
        /// zeitgesteuerter Start.
        /// </summary>
        public ScheduleStartTrigger ScheduleStart { get; set; }
    }

    /// <summary>
    /// Wie ein Message-Start mit einer Instanz umgeht, die bereits laeuft.
    /// </summary>
    /// <remarks>
    /// Es gibt die Wahl, weil beide Verhalten fachlich vorkommen: "jede Bestellung ist ein neuer Vorgang"
    /// verlangt das eine, "die Mitteilung gehoert zum laufenden Fall, sonst eroeffne ihn" das andere. Der
    /// Unterschied gehoert deshalb ins MODELL und nicht in eine globale Einstellung - sonst muesste eine
    /// Anlage sich fuer alle ihre Prozesse gemeinsam entscheiden.
    /// </remarks>
    public enum MessageStartMode
    {
        /// <summary>
        /// <b>Immer eine neue Instanz</b> (Standard). Wartende Empfangsknoten bleiben davon unberuehrt -
        /// die erreicht dieselbe Nachricht wie bisher ueber ihre eigene Korrelation.
        /// </summary>
        /// <remarks>
        /// Die vorhersagbare Vorgabe: was geschieht, steht im Modell und haengt nicht davon ab, ob
        /// zufaellig gerade jemand wartet.
        /// </remarks>
        AlwaysStart,

        /// <summary>
        /// <b>Erst zustellen, sonst starten</b>: hat die Nachricht eine passend korrelierte wartende
        /// Instanz erreicht, entsteht keine neue. Fuer "Vorgang fortsetzen oder eroeffnen".
        /// </summary>
        CorrelateOrStart,

        /// <summary>
        /// <b>Nur starten, wenn nichts laeuft</b>: existiert bereits eine laufende Instanz dieser
        /// Definition mit demselben Korrelationsschluessel, wird die Nachricht verworfen. Der Riegel gegen
        /// Doppelanlagen (zweimal geklickt, zweimal geliefert).
        /// </summary>
        /// <remarks>
        /// Braucht zwingend einen Korrelationsschluessel - ohne ihn waere "dieselbe Sache" nicht
        /// bestimmbar, und der Riegel liesse nach der ersten Instanz gar nichts mehr durch. Der Validator
        /// meldet das.
        /// </remarks>
        StartIfNoneRunning
    }

    /// <summary>
    /// Der Ausloeser "eine Nachricht dieses Namens trifft ein" an einem <see cref="StartNode"/>.
    /// </summary>
    /// <remarks>
    /// Damit entsteht eine Instanz, ohne dass jemand sie startet - der Unterschied zum
    /// <see cref="WaitNode"/>, der einen bereits LAUFENDEN Zweig weckt. Die Nutzdaten der Nachricht sind
    /// die uebergebenen Startvariablen; wie sie in den Variablen-Stack fliessen, entscheidet wie immer
    /// <see cref="StartNode.Inputs"/>.
    /// </remarks>
    public class MessageStartTrigger
    {
        /// <summary>Der Name der Nachricht, auf die dieser Einstieg horcht. Pflicht.</summary>
        public string SignalName { get; set; }

        /// <summary>Wie mit einer bereits laufenden Instanz umgegangen wird.</summary>
        public MessageStartMode Mode { get; set; } = MessageStartMode.AlwaysStart;

        /// <summary>
        /// Ob der Korrelationsschluessel der Nachricht der <b>Korrelationsschluessel der neuen Instanz</b>
        /// wird (Standard: ja). Damit findet eine spaetere Nachricht denselben Vorgang wieder.
        /// </summary>
        /// <remarks>
        /// Abschaltbar, weil der Schluessel des Absenders nicht immer der fachliche Bezug des Prozesses
        /// ist - wer den Vorgang unter einer Nummer fuehren will, die erst im Prozess entsteht, setzt sie
        /// spaeter selbst und will sie hier nicht vorbelegt haben.
        /// </remarks>
        public bool AdoptCorrelationKey { get; set; } = true;

        /// <summary>
        /// Ob ein Mandant diesen Einstieg <b>fuer sich uebernehmen</b> darf (siehe
        /// <see cref="ScheduleStartTrigger.AllowLocalActivation"/>). Standard false.
        /// </summary>
        public bool AllowLocalActivation { get; set; }

        /// <summary>
        /// Ob dieser Einstieg auch auf eine Nachricht <b>ohne Ursprungs-Mandanten</b> anspringt.
        /// Standard false.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Eine Nachricht traegt den Mandanten, aus dem sie stammt - die sendende Instanz, der
        /// Ausfuehrungs-Kontext oder die Anfrage. Nur der loest aus. Fehlt er (Systemnachricht, Host-Code
        /// ohne Kontext), loest ohne dieses Kennzeichen <b>nichts</b> aus, und das ist Absicht: sonst
        /// eroeffnet eine einzige namenlose Nachricht in jedem Mandanten einen Vorgang, der auf den Namen
        /// horcht - bei hundert Mandanten hundert Stueck.
        /// </para>
        /// <para>
        /// Wer genau das will ("Jahresabschluss beginnt, und zwar ueberall"), sagt es hier ausdruecklich.
        /// Es hat <b>nichts</b> mit <c>WorkflowEngine.BroadcastSignal</c> zu tun: ein Rundruf aus der
        /// Instanz eines Mandanten traegt sehr wohl einen Ursprung und loest nur dort aus. Es geht
        /// ausschliesslich um das FEHLEN des Ursprungs.
        /// </para>
        /// </remarks>
        public bool AllowTenantlessStart { get; set; }
    }

    /// <summary>
    /// Der Ausloeser "es ist soweit" an einem <see cref="StartNode"/> - der zeitgesteuerte Start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Zeitplan steht bewusst in der DEFINITION und nicht in einer Konfigurationsdatei daneben: sonst
    /// waere im Prozessbild nicht zu sehen, dass dieser Prozess von selbst anlaeuft - und der Fachbereich
    /// koennte es weder nachsehen noch aendern.
    /// </para>
    /// <para>
    /// Gerechnet wird in <b>Ortszeit</b> ("jeden Tag um 8" meint acht Uhr vor Ort, auch nach der
    /// Sommerzeit-Umstellung), gespeichert wird die Faelligkeit in UTC.
    /// </para>
    /// <para>
    /// Ein zeitgesteuerter Start laeuft fuer jeden Mandanten, der ihn <b>aktiviert</b> hat. Bei einer
    /// mandanteneigenen Definition ist das ihr eigener Mandant, und zwar von selbst. Bei einer
    /// oeffentlichen ist es jeder, der ihn sich geholt hat - siehe <see cref="AllowLocalActivation"/>;
    /// ohne dieses Kennzeichen laeuft eine oeffentliche Definition weiterhin fuer niemanden.
    /// </para>
    /// </remarks>
    public class ScheduleStartTrigger
    {
        /// <summary>
        /// Das Zeitplan-Muster (siehe <c>ITVComponents.Scheduling.TimeTable</c>). Pflicht.
        /// </summary>
        /// <remarks>
        /// Ein Maschinenformat - im Editor gehoert ein Zusammenbau davor, niemand tippt so etwas von Hand.
        /// Traegt es das <c>t</c>-Kennzeichen, laeuft der Plan einmalig sofort an, sobald es ihn gibt
        /// (fuer "soll beim Hochfahren durchlaufen").
        /// </remarks>
        public string Pattern { get; set; }

        /// <summary>
        /// Feste Startwerte fuer den zeitgesteuerten Lauf - es gibt ja niemanden, der ein Formular
        /// ausfuellt. Sie werden wie uebergebene Startvariablen behandelt, also gegen
        /// <see cref="StartNode.Inputs"/> aufgeloest.
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// <b>Ueberspringen, solange der vorige Lauf noch laeuft.</b> Standard false: der Termin ist der
        /// Termin.
        /// </summary>
        /// <remarks>
        /// Fuer lang laufende Auswertungen, die einander nicht ueberholen duerfen. Mit Bedacht einsetzen:
        /// ein Prozess, der auf eine Benutzer-Aufgabe wartet, gilt als "laeuft noch" und legt den ganzen
        /// Zeitplan still - das faellt oft erst auf, wenn wochenlang nichts passiert ist. Uebersprungene
        /// Termine stehen deshalb im Protokoll.
        /// </remarks>
        public bool SkipWhilePreviousRuns { get; set; }

        /// <summary>
        /// Ob ein Mandant diesen Zeitplan <b>fuer sich uebernehmen</b> darf - das Haekchen in der
        /// Uebersicht der zentralen Ablaeufe. Standard false.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Beantwortet eine andere Frage als <see cref="WorkflowDefinition.RequiredFeature"/> und
        /// <see cref="WorkflowDefinition.RequiredPermission"/>: die beiden sagen, <b>wer</b> darf, dieses
        /// Kennzeichen sagt, ob es ueberhaupt <b>zur Uebernahme gedacht</b> ist. Ohne das Kennzeichen
        /// taucht ein zentral gepflegter Prozess gar nicht erst in der Auswahl auf, auch wenn niemand
        /// ein Feature verlangt hat.
        /// </para>
        /// <para>
        /// An einer mandanteneigenen Definition ohne Wirkung - die hat ihre eine Aktivierung ohnehin.
        /// </para>
        /// </remarks>
        public bool AllowLocalActivation { get; set; }

        /// <summary>
        /// Ob ein Mandant, der diesen Zeitplan uebernommen hat, ein <b>eigenes Muster</b> setzen darf
        /// ("ich will ihn, aber am 25. statt am Letzten"). Standard false: es gilt das zentrale Muster.
        /// </summary>
        /// <remarks>
        /// Wird die Freigabe spaeter zurueckgenommen, bleibt ein abweichendes Muster stehen, wird aber
        /// nicht mehr beachtet - der Mandant laeuft ab der naechsten Faelligkeit wieder auf dem zentralen
        /// Plan. Das steht im Protokoll, denn von aussen sieht es aus, als haette sich der Termin
        /// grundlos verschoben.
        /// </remarks>
        public bool AllowReschedule { get; set; }

        /// <summary>
        /// Ob ein Mandant, der diesen Zeitplan uebernommen hat, <b>eigene Startwerte</b> setzen darf.
        /// Standard false: es gelten die festen Werte aus <see cref="Variables"/> fuer alle gleich.
        /// </summary>
        /// <remarks>Zuruecknehmen wirkt wie bei <see cref="AllowReschedule"/>.</remarks>
        public bool AllowOwnVariables { get; set; }
    }

    /// <summary>
    /// Endpunkt eines Zweigs. Erreicht ein Token diesen Knoten, wird es verbraucht. Sind danach
    /// keine Tokens mehr aktiv oder wartend, ist der Workflow abgeschlossen.
    /// </summary>
    /// <summary>
    /// Alles, was eine Knoten-Id hat. Existiert, damit Meldungen ueber mehrfach deklarierte Dinge
    /// ("... auf 2 Knoten deklariert") sowohl fuer Knotenklassen als auch fuer die Vertraege darueber
    /// formuliert werden koennen.
    /// </summary>
    public interface INodeIdentity
    {
        /// <summary>Innerhalb der Definition eindeutige Kennung des Knotens.</summary>
        string Id { get; }
    }

    /// <summary>
    /// Ein Knoten, der das <b>Ergebnis</b> des Workflows deklariert. Gemeinsamer Vertrag von
    /// <see cref="EndNode"/> und <see cref="TerminateEndNode"/>, damit die Ergebnis-Abbildung nur an
    /// EINER Stelle ausgewertet wird - sonst laufen regulaeres Ende und Abbruch auseinander, und der
    /// Unterschied faellt erst dem auf, der das Ergebnis vermisst.
    /// </summary>
    public interface IResultNode : INodeIdentity
    {
        /// <summary>Die Ergebnis-Abbildung; leer = der ganze Variablenstack ist das Ergebnis.</summary>
        List<ActivityOutputBinding> Outputs { get; }

        /// <summary>Zusaetzlich erhalten bleibende Variablen.</summary>
        List<string> RetainVariables { get; }
    }

    public class EndNode : WorkflowNode, IResultNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.End;

        /// <summary>
        /// Das <b>Ergebnis</b> des Workflows: bildet Instanz-Variablen auf Ergebnis-Namen ab
        /// (<see cref="ActivityOutputBinding.Parameter"/> = aktuelle Variable,
        /// <see cref="ActivityOutputBinding.Variable"/> = Name im Ergebnis). Ist die Liste nicht leer, wird
        /// der Scope beim Uebergang auf <see cref="Instances.WorkflowStatus.Completed"/> darauf
        /// <b>zurueckgesetzt</b> - danach besteht er genau aus dem Ergebnis (plus
        /// <see cref="RetainVariables"/>).
        /// </summary>
        /// <remarks>
        /// Der Re-Base ist der Grund, warum die Aufruferseite unveraendert bleibt: die End-Variablen der
        /// Kind-Instanz SIND das Ergebnis, das <see cref="CallWorkflowNode.Outputs"/> abbildet - es braucht
        /// weder eine zweite Ablage noch eine Migration. Leer = bisheriges Verhalten (der komplette
        /// Variablenstack ist das Ergebnis).
        /// </remarks>
        public List<ActivityOutputBinding> Outputs { get; set; } = new List<ActivityOutputBinding>();

        /// <summary>
        /// Zusaetzlich zum Ergebnis erhalten bleibende Variablen. Nur wirksam, wenn
        /// <see cref="Outputs"/> gesetzt ist (sonst bleibt ohnehin alles stehen).
        /// </summary>
        public List<string> RetainVariables { get; set; } = new List<string>();
    }

    /// <summary>
    /// Ein automatischer Schritt: fuehrt eine Aktivitaet aus (ohne Benutzerinteraktion) und laeuft
    /// dann ueber die einzige ausgehende Kante weiter.
    /// </summary>
    public class AutomatedActivityNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.AutomatedActivity;

        /// <summary>
        /// Verweist auf die auszufuehrende Aktivitaet. Ein <see cref="Activities.IActivityHost"/>
        /// loest diesen Verweis zur Laufzeit auf (Plugin aus der Factory).
        /// </summary>
        public string ActivityRef { get; set; }

        /// <summary>
        /// Optionale, statische Konfiguration fuer <b>generische</b> Aktivitaeten (z.B. Skripte), die
        /// keine deklarierten Parameter haben und ihre Konfiguration selbst aus diesem Dictionary
        /// lesen. Spezialisierte (Plugin-)Aktivitaeten mit deklarierten Parametern nutzen stattdessen
        /// <see cref="Inputs"/> und <see cref="Outputs"/>.
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Datenfluss <b>hinein</b>: bindet die deklarierten Eingabeparameter der Aktivitaet an
        /// Wertquellen (Konstante, Variable oder Ausdruck). Die Engine loest diese Bindungen vor der
        /// Ausfuehrung auf und stellt die Werte ueber
        /// <see cref="Activities.WorkflowActivityContext.Inputs"/> bereit.
        /// </summary>
        public List<ActivityInputBinding> Inputs { get; set; } = new List<ActivityInputBinding>();

        /// <summary>
        /// Datenfluss <b>heraus</b>: bildet die deklarierten Ausgabeparameter der Aktivitaet auf
        /// Instanz-Variablen ab. Nach der Ausfuehrung schreibt die Engine die von der Aktivitaet in
        /// <see cref="Activities.WorkflowActivityContext.Outputs"/> abgelegten Werte in diese Variablen.
        /// </summary>
        public List<ActivityOutputBinding> Outputs { get; set; } = new List<ActivityOutputBinding>();

        /// <summary>
        /// Wie die Ausgaben in den Scope einfliessen. Standard <see cref="ActivityScopeMode.Extend"/>
        /// (additiv). <see cref="ActivityScopeMode.Replace"/> macht den Knoten zu einer
        /// <b>Konsolidierung</b>: danach besteht der Scope nur noch aus den Ausgaben (plus
        /// <see cref="RetainVariables"/>).
        /// </summary>
        public ActivityScopeMode ScopeMode { get; set; } = ActivityScopeMode.Extend;

        /// <summary>
        /// Bei <see cref="ActivityScopeMode.Replace"/>: Namen von Variablen, die ueber die
        /// Konsolidierung hinaus erhalten bleiben (z.B. langlebige Korrelations-/Konfig-Werte). Bei
        /// <see cref="ActivityScopeMode.Extend"/> ohne Wirkung.
        /// </summary>
        public List<string> RetainVariables { get; set; } = new List<string>();

        /// <summary>
        /// Optional: fuehrt die Aktivitaet <b>je Element einer Sammlung</b> aus statt einmal - wahlweise
        /// mehrere Elemente gleichzeitig (siehe <see cref="ActivityIteration"/>). Null (Standard) = ein
        /// einziger Lauf wie bisher.
        /// </summary>
        public ActivityIteration Iteration { get; set; }

        /// <summary>
        /// Optionales Ausfuehrungs-Ziel fuer den verteilten Betrieb: der (freie) Name eines Host-Ziels,
        /// auf dem diese Aktivitaet laufen MUSS (z.B. "backend", "web"). Ist der Wert gesetzt und der
        /// aktuelle Runner bedient dieses Ziel nicht, parkt der Zweig hier
        /// (<see cref="Instances.TokenStatus.WaitingForTarget"/>) und wird von einem Runner mit passendem
        /// Ziel aufgenommen und dort ausgefuehrt. Null oder leer bedeutet: die Aktivitaet laeuft auf einem
        /// beliebigen Runner (der Standard - deckt den nicht-verteilten Betrieb ab).
        /// </summary>
        public string ExecutionTarget { get; set; }

        /// <summary>
        /// Optionaler <b>Fehler-Ausgang</b>: die Id der ausgehenden Kante, die genommen wird, wenn die
        /// Aktivitaet scheitert - durch eine Exception ODER kontrolliert ueber
        /// <see cref="Activities.WorkflowActivityContext.Fail"/>. Der Erfolgs-Ausgang ist dann die einzige
        /// andere ausgehende Kante. Ist der Wert null/leer, faultet ein Fehler wie bisher die ganze Instanz
        /// (Standard, rueckwaerts-kompatibel). Erlaubt Fehlerbehandlung im Graphen (Retry-Schleifen,
        /// Verzweigung nach Fehlerart/-anzahl, Benutzer-Korrektur).
        /// </summary>
        public string ErrorFlowId { get; set; }

        /// <summary>
        /// Beim Fehler-Ausgang: Name der Instanz-Variable, die die Fehlermeldung erhaelt (fuer Anzeige/
        /// Verzweigung). Null/leer = nicht setzen.
        /// </summary>
        public string ErrorVariable { get; set; }

        /// <summary>
        /// Beim Fehler-Ausgang: Name der Instanz-Variable, die den <b>Fehlversuchs-Zaehler</b> erhaelt - um
        /// +1 erhoeht bei jedem Fehlerlauf dieses Knotens, auf 0 zurueckgesetzt bei Erfolg. Damit laesst
        /// sich im Graphen nach Anzahl der Versuche verzweigen (z.B. 1x Auto-Korrektur, dann Benutzer-UI,
        /// dann Aufgeben). Null/leer = nicht mitzaehlen.
        /// </summary>
        public string AttemptVariable { get; set; }

        /// <summary>
        /// Beim Fehler-Ausgang: Name der Instanz-Variable, die den <b>Fehler-Code</b> erhaelt - den kurzen,
        /// stabilen Schluessel der Fehlerart, den die Aktivitaet ueber
        /// <c>WorkflowActivityContext.Fail(message, code)</c> gemeldet hat. Null/leer = nicht setzen.
        /// </summary>
        /// <remarks>
        /// Der Unterschied zu <see cref="ErrorVariable"/> ist der Adressat: die Meldung ist fuer Menschen,
        /// der Code fuer den PROZESS. Nach der Meldung zu verzweigen hiesse, sie zu parsen - und der
        /// Ablauf haenge damit an einer Formulierung, die jederzeit jemand umschreibt oder uebersetzt.
        /// Bleibt leer, wenn die Aktivitaet mit einer Exception gescheitert ist statt kontrolliert.
        /// </remarks>
        public string ErrorCodeVariable { get; set; }
    }

    /// <summary>
    /// Ruft einen anderen Workflow als <b>Subworkflow</b> auf. Der aufrufende Zweig parkt, bis der
    /// Subworkflow endet; danach laeuft er ueber die einzige ausgehende Kante weiter. So laesst sich ein
    /// parametrierter Workflow wie eine Aktivitaet in einen anderen einbauen (mit Ein- und Ausgabewerten).
    /// </summary>
    /// <remarks>
    /// Der Subworkflow laeuft als eigene, vollwertige Instanz (eigene Tokens/History/Monitoring), erbt den
    /// Tenant des Elternprozesses und ist ueber einen Rueck-Link mit dem wartenden Eltern-Token verbunden.
    /// Endet er, werden seine End-Variablen ueber <see cref="Outputs"/> auf die Eltern-Variablen abgebildet;
    /// faultet er, faultet standardmaessig der aufrufende Knoten. Das Vorantreiben von Subworkflows
    /// uebernimmt der <c>WorkflowRunner</c> (der nebenlaeufige Ausfuehrungspfad).
    /// </remarks>
    public class CallWorkflowNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.CallWorkflow;

        /// <summary>Fachliche Id der aufzurufenden (Sub-)Workflow-Definition.</summary>
        public string SubDefinitionId { get; set; }

        /// <summary>
        /// Optionale feste Version der Subworkflow-Definition. Null = jeweils die hoechste Version (wie beim
        /// Start eines Workflows).
        /// </summary>
        public int? SubDefinitionVersion { get; set; }

        /// <summary>
        /// Datenfluss <b>hinein</b>: bindet Startvariablen des Subworkflows an Wertquellen des
        /// Elternprozesses (Konstante, Variable oder Ausdruck). <see cref="ActivityInputBinding.Parameter"/>
        /// ist der Name der zu setzenden Kind-Variable.
        /// </summary>
        public List<ActivityInputBinding> Inputs { get; set; } = new List<ActivityInputBinding>();

        /// <summary>
        /// Datenfluss <b>heraus</b>: bildet End-Variablen des Subworkflows auf Eltern-Variablen ab.
        /// <see cref="ActivityOutputBinding.Parameter"/> ist der Name der Kind-Endvariable,
        /// <see cref="ActivityOutputBinding.Variable"/> die Ziel-Variable im Elternprozess.
        /// </summary>
        public List<ActivityOutputBinding> Outputs { get; set; } = new List<ActivityOutputBinding>();

        /// <summary>
        /// Wie die Ausgaben des Subworkflows in den Scope des Elternprozesses einfliessen. Standard
        /// <see cref="ActivityScopeMode.Extend"/> (additiv). <see cref="ActivityScopeMode.Replace"/>
        /// macht den Aufruf zu einer <b>Konsolidierung</b>: danach besteht der Eltern-Scope genau aus
        /// den hier abgebildeten Ausgaben (plus <see cref="RetainVariables"/>). Nuetzlich, wenn ein
        /// Subworkflow einen grossen Zwischenzustand aufbaut, von dem der Aufrufer nur das Ergebnis
        /// braucht.
        /// </summary>
        public ActivityScopeMode ScopeMode { get; set; } = ActivityScopeMode.Extend;

        /// <summary>
        /// Bei <see cref="ActivityScopeMode.Replace"/>: Namen von <b>Eltern</b>-Variablen, die ueber die
        /// Konsolidierung hinaus erhalten bleiben. Bei <see cref="ActivityScopeMode.Extend"/> ohne Wirkung.
        /// </summary>
        public List<string> RetainVariables { get; set; } = new List<string>();

        /// <summary>
        /// Optionaler <b>Fehler-Ausgang</b>: die Id der ausgehenden Kante, die genommen wird, wenn der
        /// Subworkflow scheitert (faultet oder abgebrochen wird). Der Erfolgs-Ausgang ist dann die einzige
        /// andere ausgehende Kante. Ist der Wert null/leer, faultet ein gescheiterter Subworkflow wie bisher
        /// den aufrufenden Knoten (Standard, rueckwaerts-kompatibel). Erlaubt Fehlerbehandlung im Graphen
        /// (Alternativpfad, Kompensation, oder - mit <see cref="AttemptVariable"/> - Wiederholung des
        /// Subworkflows).
        /// </summary>
        public string ErrorFlowId { get; set; }

        /// <summary>
        /// Beim Fehler-Ausgang: Name der Eltern-Variable, die die Fehlermeldung des Subworkflows erhaelt.
        /// Null/leer = nicht setzen.
        /// </summary>
        public string ErrorVariable { get; set; }

        /// <summary>
        /// Beim Fehler-Ausgang: Name der Eltern-Variable, die den <b>Fehlversuchs-Zaehler</b> erhaelt -
        /// +1 bei jedem gescheiterten Subworkflow-Lauf, 0 bei Erfolg. Ist er gesetzt, bekommt jeder Versuch
        /// eine EIGENE Kind-Instanz (die Fehlerkante kann also zum selben Knoten zurueckfuehren = echte
        /// Wiederholung); ohne ihn wird der Subworkflow nicht neu angelegt. Null/leer = nicht mitzaehlen.
        /// </summary>
        public string AttemptVariable { get; set; }

        /// <summary>
        /// Beim Fehler-Ausgang: Name der Eltern-Variable, die den <b>Fehler-Code</b> des Subworkflows
        /// erhaelt. Null/leer = nicht setzen.
        /// </summary>
        /// <remarks>
        /// <b>Damit kommt eine Fehlerart aus dem Subworkflow heraus.</b> Bisher erfuhr der Aufrufer nur
        /// eine Meldung - er konnte also nicht unterscheiden, WORAN es gelegen hat, ohne den Text zu
        /// parsen. Der Code stammt aus der Aktivitaet, die im Kind kontrolliert gescheitert ist
        /// (<c>Fail(message, code)</c>), und reist ueber den Fault der Kind-Instanz herauf. Bleibt leer,
        /// wenn das Kind an einer Exception gescheitert ist oder abgebrochen wurde.
        /// </remarks>
        public string ErrorCodeVariable { get; set; }
    }

    /// <summary>
    /// Wen ein eintreffendes Ereignis erreicht.
    /// </summary>
    public enum WaitKind
    {
        /// <summary>
        /// <b>Gerichtete Nachricht</b> (Standard): erreicht nur den Wartepunkt, zu dem sie korreliert -
        /// ueber <see cref="WaitNode.CorrelationExpression"/>, ersatzweise den Korrelationsschluessel
        /// oder die Id der Instanz. Ohne passenden Schluessel kommt sie NICHT an.
        /// </summary>
        Message,

        /// <summary>
        /// <b>Rundruf</b>: erreicht jeden Wartepunkt dieses Namens in jeder laufenden Instanz, ohne
        /// Korrelation. Fuer Ereignisse, die die ganze Anlage betreffen („Tagesabschluss gestartet",
        /// „Preisliste aktualisiert").
        /// </summary>
        Signal
    }

    /// <summary>
    /// Ein Wartepunkt, der den Workflow anhaelt, bis ein benanntes Signal eintrifft (z.B. eine
    /// Benutzereingabe oder ein externes Ereignis). Das Token wird waehrenddessen als wartend
    /// persistiert.
    /// </summary>
    public class WaitNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Wait;

        /// <summary>Der Name des Signals, auf das dieser Knoten wartet.</summary>
        public string SignalName { get; set; }

        /// <summary>
        /// Ob dieser Wartepunkt eine <b>gerichtete Nachricht</b> erwartet (Standard) oder ein
        /// <b>Rundruf</b> ist. Siehe <see cref="Model.WaitKind"/> - der Unterschied entscheidet, wen ein
        /// eintreffendes Ereignis erreicht.
        /// </summary>
        public WaitKind WaitKind { get; set; } = WaitKind.Message;

        /// <summary>
        /// Optionaler CScript-Ausdruck, der den <b>Korrelationsschluessel dieses Wartepunkts</b> liefert -
        /// ausgewertet, wenn der Zweig hier parkt, und am Token abgelegt. Eine eintreffende Nachricht
        /// findet den Wartepunkt darueber. Leer = es gilt der Korrelationsschluessel der Instanz (oder
        /// ihre Id).
        /// </summary>
        /// <remarks>
        /// Der Unterschied zum Instanz-Schluessel ist der Zeitpunkt: der steht beim Anlegen fest, dieser
        /// hier erst beim Warten. Erst damit laesst sich auf etwas korrelieren, das der Prozess selbst
        /// gerade erst erzeugt hat - eine Bestellnummer aus dem vorigen Schritt, ein Vorgang aus einem
        /// Fremdsystem. Wartet dieselbe Instanz an mehreren Stellen, hat jeder Wartepunkt seinen eigenen
        /// Schluessel.
        /// </remarks>
        public string CorrelationExpression { get; set; }

        /// <summary>
        /// Wie <see cref="CorrelationExpression"/> zu lesen ist: EIN Ausdruck (Standard) oder ein ganzes
        /// Skript mit <c>return</c>.
        /// </summary>
        public ScriptMode CorrelationExpressionMode { get; set; } = ScriptMode.Expression;
    }

    /// <summary>
    /// <b>Sendet</b> eine gerichtete Nachricht oder einen Rundruf - das Gegenstueck zum
    /// <see cref="WaitNode"/>. Der Zweig laeuft unmittelbar weiter.
    /// </summary>
    /// <remarks>
    /// Zugestellt wird <b>nach dem Commit</b> des sendenden Zweigs, nicht mitten in seiner Ausfuehrung.
    /// Der Unterschied ist nicht theoretisch: der Empfaenger wird beim Zustellen selbst vorangetrieben,
    /// und zwar auf dem Thread des Senders. Geschaehe das sofort, liefe er auf einem Stand des Senders,
    /// den es in der Datenbank noch gar nicht gibt - und ein Fehler des Empfaengers schluege mitten im
    /// Sender auf. Gepuffert wird deshalb bis zum naechsten Halt des sendenden Zweigs.
    /// <para>
    /// Daraus folgt eine Grenze, die man kennen muss: <b>wie viele Empfaenger erreicht wurden, steht
    /// beim Ausfuehren des Knotens noch nicht fest</b> - zu dem Zeitpunkt ist noch nichts zugestellt.
    /// Die Zahl kann deshalb nicht in eine Variable fliessen; sie landet im System-Log, und „niemand hat
    /// gewartet" wird dort eigens gemeldet. Wer den Ausgang im Prozess VERZWEIGEN muss, braucht statt
    /// dessen eine Aktivitaet, die selbst zustellt und das Ergebnis auswertet.
    /// </para>
    /// </remarks>
    public class SendMessageNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.SendMessage;

        /// <summary>Der Name des Signals, das gesendet wird.</summary>
        public string SignalName { get; set; }

        /// <summary>
        /// Ob eine <b>gerichtete Nachricht</b> (Standard) oder ein <b>Rundruf</b> gesendet wird -
        /// dieselbe Unterscheidung wie am Wartepunkt, nur von der anderen Seite.
        /// </summary>
        public WaitKind WaitKind { get; set; } = WaitKind.Message;

        /// <summary>
        /// Bei einer Nachricht: der CScript-Ausdruck, der den <b>Korrelationsschluessel</b> liefert -
        /// ausgewertet ueber den Variablen-Stand dieses Zweigs. Er muss denselben Wert ergeben wie der
        /// Ausdruck am Wartepunkt der Gegenseite.
        /// </summary>
        /// <remarks>
        /// Ohne Schluessel gibt es keine gerichtete Zustellung - die Nachricht wuerde zum Rundruf und
        /// jeden gleichnamigen Wartepunkt wecken. Der Validator meldet das als Fehler, statt es
        /// stillschweigend geschehen zu lassen.
        /// </remarks>
        public string CorrelationExpression { get; set; }

        /// <summary>
        /// Wie <see cref="CorrelationExpression"/> zu lesen ist: EIN Ausdruck (Standard) oder ein ganzes
        /// Skript mit <c>return</c>.
        /// </summary>
        public ScriptMode CorrelationExpressionMode { get; set; } = ScriptMode.Expression;

        /// <summary>
        /// Die <b>Nutzdaten</b> der Nachricht: dieselben Bindungen wie an einer Aktivitaet. Was hier
        /// entsteht, setzt der Empfaenger vor seinem Weiterlauf in seinen Variablen-Stand.
        /// </summary>
        public List<ActivityInputBinding> Inputs { get; set; } = new List<ActivityInputBinding>();

        /// <summary>
        /// Ob der Zweig auf die Zustellung <b>wartet</b> - dann steht ihm danach die Zahl der erreichten
        /// Empfaenger zur Verfuegung (<see cref="ReachedVariable"/>).
        /// </summary>
        /// <remarks>
        /// Das ist der Weg, „niemand hat gewartet" im Prozess auswertbar zu machen. Der Preis ist ein
        /// Halt: das Token parkt, der Zweig wird festgeschrieben, dann wird zugestellt, und erst danach
        /// laeuft er mit dem Ergebnis weiter - also zwei Commits statt einem. Fuer eine blosse
        /// Benachrichtigung ist das verschenkt, deshalb ist es aus.
        /// <para>
        /// Anders herum geht es nicht: waehrend der Sender noch laeuft, ist noch nichts zugestellt, und
        /// es gaebe nichts zu zaehlen.
        /// </para>
        /// </remarks>
        public bool WaitForDelivery { get; set; }

        /// <summary>
        /// Bei <see cref="WaitForDelivery"/>: die Variable, in die die Zahl der erreichten Empfaenger
        /// geschrieben wird. Leer = die Zahl wird nicht uebernommen (der Zweig wartet dann nur).
        /// </summary>
        public string ReachedVariable { get; set; }
    }

    /// <summary>
    /// Eine <b>Aufgabe fuer einen Menschen</b>: der Zweig parkt hier, bis die Aufgabe in der Oberflaeche
    /// erledigt wird. Danach laeuft er ueber die einzige ausgehende Kante weiter.
    /// </summary>
    /// <remarks>
    /// Technisch wartet das Token wie an einem <see cref="WaitNode"/> (<see cref="Instances.TokenStatus.Waiting"/>),
    /// fachlich ist es etwas anderes: es gibt eine Zustaendigkeit, eine Maske und einen definierten
    /// Abschluss. Deshalb ein eigener Knoten und ein eigener Abschlussweg
    /// (<c>WorkflowEngine.CompleteUserTask</c>) - ein Signal wuerde ALLE gleichnamig wartenden Tokens
    /// wecken und ohne Versionsvergleich schreiben.
    /// <para>
    /// Wer die Aufgabe sieht, entscheidet <see cref="RequiredPermission"/> (die Sorte Aufgabe); wessen
    /// Aufgabe der konkrete Fall ist, optional <see cref="Assignment"/>.
    /// </para>
    /// </remarks>
    public class UserActivityNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.UserActivity;

        /// <summary>
        /// Der fachliche Schluessel der <b>Aufgabenart</b> (z.B. "ApproveInvoice"). Er ist der Filter der
        /// Arbeitsliste und der Ausweichschluessel fuer die Oberflaeche, wenn kein <see cref="ViewKey"/>
        /// gesetzt ist. Pflicht.
        /// </summary>
        public string TaskKey { get; set; }

        /// <summary>
        /// Die Permission, die ein Benutzer braucht, um Aufgaben dieses Knotens zu SEHEN und zu erledigen.
        /// Null/leer = es genuegt das allgemeine Aufgaben-Recht.
        /// </summary>
        public string RequiredPermission { get; set; }

        /// <summary>
        /// Optionaler CScript-Ausdruck ueber den Variablen des Zweigs, der den Benutzernamen des
        /// Zustaendigen liefert. Er wird <b>einmal</b> beim Parken ausgewertet und am Token festgeschrieben -
        /// die Aufgabenliste ist eine Datenbankabfrage und kann kein Skript auswerten. Null/leer = die
        /// Aufgabe gehoert dem Pool (jeder mit der Permission sieht und erledigt sie).
        /// </summary>
        public string Assignment { get; set; }

        /// <summary>
        /// Wie <see cref="Assignment"/> zu lesen ist: EIN Ausdruck (Standard) oder ein ganzes Skript mit
        /// <c>return</c>.
        /// </summary>
        public ScriptMode AssignmentMode { get; set; } = ScriptMode.Expression;

        /// <summary>
        /// Optionaler Schluessel der Oberflaechen-Komponente, die diese Aufgabe darstellt. Aufgeloest wird
        /// er von der konsumenten-seitigen Registry (<c>ViewKey</c>, sonst <see cref="TaskKey"/>, sonst die
        /// generische Maske aus <see cref="FormFields"/>).
        /// </summary>
        /// <remarks>
        /// Bewusst ein freier Schluessel und <b>nie</b> ein Typname: Definitionen sind Daten aus der
        /// Datenbank - ein Typname darin waere Code-Ausfuehrung per Datenpflege.
        /// </remarks>
        public string ViewKey { get; set; }

        /// <summary>
        /// Der Titel der Aufgabe fuer die Arbeitsliste. Klartext ODER ein JSON-Objekt nach Kultur
        /// (<c>{"de":"Rechnung freigeben","fr":"Approuver la facture"}</c>) - dieselbe Konvention wie bei
        /// den Navigations-Eintraegen. Der Wert wird beim Parken <b>unaufgeloest</b> am Token
        /// festgeschrieben und erst beim Anzeigen uebersetzt; sonst wuerde die Kultur des Servers die des
        /// Lesers bestimmen.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Optionale Beschreibung/Arbeitsanweisung fuer die Maske. Klartext oder Kultur-JSON wie
        /// <see cref="Title"/>.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Optionaler CScript-Ausdruck, der ein <b>Datenobjekt</b> fuer die Formatierung von
        /// <see cref="Title"/> UND <see cref="Description"/> liefert (z.B. <c>{ InvoiceNo: rechnungsNr }</c>).
        /// Ist er gesetzt, werden Titel und Beschreibung (nach der Kultur-Aufloesung) als Formatierungs-
        /// Prototyp behandelt: Platzhalter wie <c>[InvoiceNo:0000000000]</c> werden aus den Membern dieses
        /// Objekts gefuellt (siehe <c>ITVComponents.Formatting</c>). So bleiben Titel/Beschreibung
        /// mehrsprachig (je Kultur ein Prototyp), waehrend die konkreten Werte aus dem aktuellen
        /// Variablen-Stack kommen. Ausgewertet beim OEFFNEN der Aufgabe (nicht beim Parken), damit der
        /// aktuelle Stand einfliesst. Leer/null = Titel und Beschreibung werden unveraendert angezeigt.
        /// </summary>
        public string FormatData { get; set; }

        /// <summary>
        /// Wie <see cref="FormatData"/> zu lesen ist: EIN Ausdruck (Standard) oder ein ganzes Skript mit
        /// <c>return</c>.
        /// </summary>
        public ScriptMode FormatDataMode { get; set; } = ScriptMode.Expression;

        /// <summary>
        /// Gehoert diese Aufgabe zu einem <b>gefuehrten Abschnitt</b>? Dann fuehrt eine Oberflaeche, die das
        /// kann, nach ihrem Abschluss gleich zum naechsten Schritt weiter, statt sich zu schliessen.
        /// Vorgabe: nein - eine Aufgabe steht fuer sich.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Die Eigenschaft gehoert dem KNOTEN und nicht dem Aufruf: sonst haenge der gefuehrte Modus daran,
        /// WO der Benutzer eingestiegen ist. Wer den Assistenten mittendrin schliesst und die Aufgabe
        /// spaeter aus seiner Arbeitsliste heraus wieder oeffnet, bekommt damit denselben gefuehrten Ablauf
        /// - der Vorgang ist gefuehrt, nicht die Sitzung.
        /// </para>
        /// <para>
        /// Wo der gefuehrte Abschnitt endet, kann <see cref="EndsAssistant"/> genauer sagen. Ohne dieses
        /// Feld endet er von selbst, sobald der naechste Schritt nicht mehr dazugehoert - nur eben erst,
        /// nachdem die Oberflaeche auf ihn gewartet hat.
        /// </para>
        /// </remarks>
        public bool RunsInAssistant { get; set; }

        /// <summary>
        /// Optionaler CScript-Ausdruck: liefert er <c>true</c>, endet mit dieser Aufgabe der <b>gefuehrte
        /// Teil</b> des Vorgangs - eine Oberflaeche, die einen Assistenten fuehrt, schliesst danach, statt
        /// auf den naechsten Schritt zu warten. Leer/null = der Vorgang gilt als fortsetzbar.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Der Knoten sagt damit etwas ueber SICH: "nach mir ist der gefuehrte Teil zu Ende". Bewusst nicht
        /// am ersten NICHT-gefuehrten Knoten - der Assistent muesste diesen sonst erst abwarten und finden,
        /// nur um ihn zu verwerfen; und bei einem spaeter eingeschobenen Schritt saesse die Aussage an der
        /// falschen Stelle.
        /// </para>
        /// <para>
        /// Ein Ausdruck und kein Schalter, weil die Antwort von den Daten abhaengen darf ("ab 1000 geht es
        /// zur Freigabe, sonst ist Schluss"). Ausgewertet wird beim ABSCHLUSS, nach dem Uebernehmen der
        /// Ergebniswerte - die Eingaben dieses Schritts stehen also schon im Variablen-Stack.
        /// </para>
        /// <para>
        /// Eine fehlgeschlagene Auswertung faultet die Instanz <b>nicht</b>: die Aufgabe ist erledigt, das
        /// Ergebnis gespeichert. Sie gilt dann als "nicht beendet" - der Assistent laeuft weiter, was
        /// sichtbar ist und niemandem schadet. Die umgekehrte Vorgabe wuerde einen Tippfehler zu einem
        /// stillen Abbruch nach dem ersten Schritt machen.
        /// </para>
        /// </remarks>
        public string EndsAssistant { get; set; }

        /// <summary>
        /// Wie <see cref="EndsAssistant"/> zu lesen ist: EIN Ausdruck (Standard) oder ein ganzes Skript mit
        /// <c>return</c>.
        /// </summary>
        public ScriptMode EndsAssistantMode { get; set; } = ScriptMode.Expression;

        /// <summary>
        /// Datenfluss <b>hinein</b>: was die Maske zu sehen bekommt. Die Bindungen werden aufgeloest, wenn
        /// die Aufgabe geoeffnet wird (nicht beim Parken) - die Maske sieht damit den aktuellen Stand und es
        /// braucht keine zweite Ablage. <see cref="ActivityInputBinding.Parameter"/> ist der Name im
        /// Payload der Maske.
        /// </summary>
        public List<ActivityInputBinding> Inputs { get; set; } = new List<ActivityInputBinding>();

        /// <summary>
        /// Datenfluss <b>heraus</b>: bildet die Ergebniswerte der Maske (bei der generischen Maske die
        /// <see cref="FormFields"/>) auf Variablen des Zweigs ab.
        /// </summary>
        public List<ActivityOutputBinding> Outputs { get; set; } = new List<ActivityOutputBinding>();

        /// <summary>
        /// Wie das Ergebnis in den Scope einfliesst. Standard <see cref="ActivityScopeMode.Extend"/>
        /// (additiv), <see cref="ActivityScopeMode.Replace"/> konsolidiert wie bei der Aktivitaet.
        /// </summary>
        public ActivityScopeMode ScopeMode { get; set; } = ActivityScopeMode.Extend;

        /// <summary>
        /// Bei <see cref="ActivityScopeMode.Replace"/>: Variablen, die ueber die Konsolidierung hinaus
        /// erhalten bleiben. Bei <see cref="ActivityScopeMode.Extend"/> ohne Wirkung.
        /// </summary>
        public List<string> RetainVariables { get; set; } = new List<string>();

        /// <summary>
        /// Die Deklaration der <b>generischen Maske</b>: ist keine eigene Komponente registriert, baut die
        /// Oberflaeche daraus ein Formular. Leer = die Aufgabe wird nur bestaetigt (kein Eingabefeld).
        /// </summary>
        public List<UserTaskField> FormFields { get; set; } = new List<UserTaskField>();

        /// <summary>
        /// Optionale Frist in Stunden ab dem Parken. Setzt <see cref="Instances.Token.TaskDueUtc"/> - die
        /// Aufgabenliste kann danach sortieren und Ueberfaelliges hervorheben. Der Ablauf laesst die
        /// Aufgabe <b>nicht</b> automatisch weiterlaufen. Null/0 = keine Frist.
        /// </summary>
        /// <remarks>
        /// Soll die Frist etwas <b>ausloesen</b> (Erinnerung, Eskalation, automatische Ablehnung), gehoert
        /// ein <see cref="BoundaryTimerNode"/> an diesen Schritt. Ein <see cref="TimerNode"/> in einem
        /// parallelen Zweig taugt dafuer NICHT: der zugehoerige Join wartet auf beide Straenge, der
        /// Hauptfluss haenge also bis zum Ablauf der Frist - und da jeder Strang nach dem Split seine
        /// eigene Scope-Kopie hat, koennte der Timer-Zweig gar nicht pruefen, ob die Aufgabe erledigt ist.
        /// </remarks>
        public double? DueInHours { get; set; }
    }

    /// <summary>
    /// Ein <b>Fristen-Timer am Schritt</b> (BPMN: Boundary-Timer-Event). Er haengt an einem Schritt, an
    /// dem ein Token parkt, und loest nach Ablauf einen <b>Nebenpfad</b> aus - typisch eine Eskalation
    /// ("erinnere den Zustaendigen").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nicht unterbrechend</b> (Standard): der Hauptfluss laeuft unveraendert weiter und wartet nicht.
    /// Ausgeloest wird ein ZUSAETZLICHES Token auf der ausgehenden Kante dieses Timers; es traegt eine
    /// Kopie des Scopes des Haupt-Tokens, und was es schreibt, fliesst NICHT zurueck. Genau deshalb ist das
    /// kein AND-Split: dort muesste jeder Strang wieder gejoint werden, und der Hauptfluss haenge bis zum
    /// Ablauf der Frist.
    /// </para>
    /// <para>
    /// <b>Lebensdauer:</b> der Timer wird scharf, wenn das Haupt-Token an seinem Schritt <b>parkt</b>
    /// (Benutzer-Aufgabe, Subworkflow-Aufruf, Warten auf ein Ausfuehrungs-Ziel). Verlaesst das Haupt-Token
    /// den Schritt, werden der Timer UND ein eventuell noch laufender Nebenpfad verworfen. Ein Timer an
    /// einem Schritt, an dem nie geparkt wird (schnelle Aktivitaet), feuert nie - der Validator meldet das.
    /// </para>
    /// <para>
    /// <b>Wiederholung:</b> <see cref="Deadlines"/> wird der Reihe nach abgearbeitet (z.B. "24h, dann 12h"
    /// = erste Erinnerung nach 24h, zweite 12h spaeter). Ist die Liste erschoepft, wiederholt
    /// <see cref="RepeatLast"/> die letzte Frist endlos; sonst schweigt der Timer.
    /// </para>
    /// </remarks>
    public class BoundaryTimerNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.BoundaryTimer;

        /// <summary>
        /// Die Id des Schritts, an dem dieser Timer haengt. Erlaubt sind Schritte, an denen ein Token
        /// parken kann: <see cref="UserActivityNode"/>, <see cref="CallWorkflowNode"/> und
        /// <see cref="AutomatedActivityNode"/> (dort nur mit <c>ExecutionTarget</c>, sonst laeuft das
        /// Token synchron durch).
        /// </summary>
        public string AttachedToNodeId { get; set; }

        /// <summary>
        /// Die Fristen, der Reihe nach ab dem Parken. Die erste gilt ab dem Parken, jede weitere ab der
        /// vorigen Ausloesung. Beim <see cref="Interrupting"/>-Timer zaehlt nur die erste - danach steht
        /// das Token woanders.
        /// </summary>
        public List<BoundaryDeadline> Deadlines { get; set; } = new List<BoundaryDeadline>();

        /// <summary>
        /// <b>Altbestand:</b> die frueheren Fristen als reine Stundenzahlen. Wird nur noch gelesen, damit
        /// bereits gespeicherte Definitionen unveraendert weiterlaufen; <see cref="EffectiveDeadlines"/>
        /// bildet sie auf <see cref="Deadlines"/> ab, und der Editor migriert sie beim Oeffnen einmalig.
        /// </summary>
        /// <remarks>
        /// Bewusst NICHT entfernt: die Definitionen liegen als JSON in der Datenbank. Ein entferntes Feld
        /// haette bestehende Timer still verstummen lassen - der Graph saehe unveraendert aus, die
        /// Eskalation bliebe einfach aus.
        /// </remarks>
        public List<double> IntervalsInHours { get; set; } = new List<double>();

        /// <summary>
        /// Nach dem letzten Eintrag aus <see cref="Deadlines"/> die letzte Frist endlos wiederholen?
        /// ("danach alle 2 Stunden"). Ohne das schweigt der Timer, wenn die Liste durch ist.
        /// </summary>
        /// <remarks>
        /// Sinnvoll nur mit einer Frist, die eine <b>Dauer</b> liefert. Eine absolute Zeitangabe
        /// (<see cref="System.DateTime"/>) ergaebe wiederholt denselben, dann vergangenen Zeitpunkt; die
        /// Engine laesst den Timer in diesem Fall verstummen, statt in einer Schleife zu feuern.
        /// </remarks>
        public bool RepeatLast { get; set; }

        /// <summary>
        /// <b>Unterbrechend</b>: statt eines Nebenpfads nimmt das HAUPT-Token die ausgehende Kante - der
        /// Schritt gilt damit als abgebrochen (eine wartende Benutzer-Aufgabe verschwindet aus der
        /// Arbeitsliste). Fuer "Frist verstrichen -&gt; automatisch abgelehnt". Standard false.
        /// </summary>
        /// <remarks>
        /// Unterbrechend feuert naturgemaess <b>einmal</b>: danach steht das Token woanders, und es gibt
        /// nichts mehr, woran der Timer haengen koennte. <see cref="RepeatLast"/> und weitere Intervalle
        /// haben dann keine Wirkung.
        /// </remarks>
        public bool Interrupting { get; set; }

        /// <summary>
        /// Optionaler Variablenname, in dem die Nummer der Ausloesung landet (1 beim ersten Mal). Sie
        /// steht im Scope des Nebenpfads - so kann die Eskalation "zum dritten Mal" anders formulieren.
        /// </summary>
        public string CountVariable { get; set; }

        /// <summary>
        /// Die tatsaechlich geltenden Fristen: <see cref="Deadlines"/>, oder - solange die leer ist -
        /// der Altbestand aus <see cref="IntervalsInHours"/> als gleichwertige Ausdruecke. Reine
        /// Abfrage, sie veraendert den Knoten nicht (der Vortrieb darf die Definition nicht umschreiben).
        /// </summary>
        public IReadOnlyList<BoundaryDeadline> EffectiveDeadlines()
        {
            if (Deadlines != null && Deadlines.Count != 0)
            {
                return Deadlines;
            }

            if (IntervalsInHours == null || IntervalsInHours.Count == 0)
            {
                return Array.Empty<BoundaryDeadline>();
            }

            return IntervalsInHours
                .Select(h => new BoundaryDeadline
                {
                    Expression = h.ToString(CultureInfo.InvariantCulture)
                })
                .ToList();
        }

        /// <summary>
        /// Ob an dem angegebenen Schritt ein Fristen-Timer haengen darf: nur dort, wo das Token
        /// tatsaechlich PARKT. Eine gewoehnliche Aktivitaet laeuft synchron durch - ein Timer an ihr
        /// koennte nie feuern.
        /// </summary>
        /// <remarks>
        /// Die Regel steht bewusst am Modell und nicht im Validator: sie wird an drei Stellen
        /// gebraucht (Pruefung, Auswahlliste im Editor, Andocken per Ziehen im Designer). Drei Kopien
        /// derselben Bedingung wuerden frueher oder spaeter auseinanderlaufen - und die Oberflaeche
        /// boete dann etwas an, das der Validator gleich darauf beanstandet.
        /// </remarks>
        /// <param name="node">der Schritt (darf null sein)</param>
        /// <returns>true, wenn an diesem Schritt ein Fristen-Timer haengen darf</returns>
        public static bool CanHost(WorkflowNode node)
        {
            switch (node)
            {
                case UserActivityNode _:
                case CallWorkflowNode _:
                // Der eigentliche Gewinn des eingebetteten Abschnitts: eine Frist ueber MEHRERE Schritte
                // ("die ganze Pruefung muss in 48 Stunden durch sein") - vorher nur je Einzelschritt
                // modellierbar. Der Knoten parkt, waehrend innen gearbeitet wird, also greift die Frist.
                case SubProcessNode _:
                    return true;
                case AutomatedActivityNode a:
                    return !string.IsNullOrWhiteSpace(a.ExecutionTarget);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Uebernimmt den Altbestand einmalig in <see cref="Deadlines"/> und leert ihn. Der Editor ruft
        /// das beim Oeffnen, damit die Definition beim naechsten Speichern in der neuen Form liegt.
        /// Liefert true, wenn dabei etwas umgestellt wurde.
        /// </summary>
        public bool MigrateLegacyDeadlines()
        {
            if (IntervalsInHours == null || IntervalsInHours.Count == 0
                || (Deadlines != null && Deadlines.Count != 0))
            {
                return false;
            }

            Deadlines = EffectiveDeadlines().ToList();
            IntervalsInHours = new List<double>();
            return true;
        }
    }

    /// <summary>
    /// EINE Frist eines <see cref="BoundaryTimerNode"/>: ein CScript-Ausdruck, der sagt, wann sie ablaeuft.
    /// </summary>
    /// <remarks>
    /// Erlaubt sind drei Ergebnisse - dieselbe Konvention wie beim gewoehnlichen <see cref="TimerNode"/>,
    /// erweitert um die Kurzform, die den frueheren Stunden-Feldern entspricht:
    /// <list type="bullet">
    /// <item><description>eine <see cref="System.TimeSpan"/> - Dauer ab dem Parken bzw. ab der vorigen
    /// Ausloesung,</description></item>
    /// <item><description>ein <see cref="System.DateTime"/> - ein absoluter Zeitpunkt,</description></item>
    /// <item><description>eine Zahl - Dauer in <b>Stunden</b> (so bleibt "24" die kuerzeste Schreibweise
    /// des haeufigsten Falls).</description></item>
    /// </list>
    /// Eine Dauer von null oder weniger ist ein Fehler, kein "sofort": sie wuerde zusammen mit
    /// <see cref="BoundaryTimerNode.RepeatLast"/> endlos feuern.
    /// <para>
    /// Beispiele: <c>24</c>, <c>tageBisFrist * 24</c>, <c>faelligAm</c> (eine DateTime-Variable),
    /// <c>'System.TimeSpan'.FromHours(36)</c>. Statische Aufrufe schreibt CScript mit dem Typnamen in
    /// Anfuehrungszeichen - <c>TimeSpan.FromHours(36)</c> ohne sie laeuft auf einen Aufruf gegen null.
    /// </para>
    /// </remarks>
    public class BoundaryDeadline
    {
        /// <summary>Der CScript-Ausdruck, ausgewertet ueber dem Scope des Haupt-Tokens.</summary>
        public string Expression { get; set; }

        /// <summary>
        /// Wie <see cref="Expression"/> zu lesen ist: EIN Ausdruck (Standard) oder ein ganzes Skript mit
        /// <c>return</c>.
        /// </summary>
        public ScriptMode ExpressionMode { get; set; } = ScriptMode.Expression;
    }

    /// <summary>
    /// Der Endpunkt eines <b>Nebenpfads</b> (Eskalation): verbraucht das Token und sonst nichts.
    /// </summary>
    /// <remarks>
    /// Bewusst ein eigener Knoten und nicht der <see cref="EndNode"/>: der beendet den WORKFLOW (er
    /// deklariert das Ergebnis und es darf genau einen davon geben). Ein Nebenpfad soll aber nur
    /// auslaufen - und ohne Endpunkt wuerde sein letzter Schritt an der Regel "genau eine ausgehende
    /// Kante" scheitern. Erreicht ein Nebenpfad diesen Knoten, ist die Runde vorbei; der zugehoerige
    /// Timer wartet auf sein naechstes Intervall.
    /// </remarks>
    public class SidePathEndNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.SidePathEnd;
    }

    /// <summary>
    /// Beendet die <b>ganze Instanz</b> sofort: alle anderen Zweige werden verworfen, laufende
    /// Subworkflows abgebrochen. Der Workflow gilt danach als regulaer beendet
    /// (<see cref="Instances.WorkflowStatus.Completed"/>), nicht als abgebrochen.
    /// </summary>
    /// <remarks>
    /// Der Unterschied zum <see cref="EndNode"/>: der verbraucht nur SEIN Token und laesst die
    /// Geschwister weiterlaufen - die Instanz endet erst, wenn das letzte Token weg ist. Hier endet sie
    /// mit diesem einen Zweig. Der klassische Fall ist der Abbruch aus einem Nebenpfad heraus („Kunde hat
    /// storniert" - der Rest der Bearbeitung ist gegenstandslos).
    /// <para>
    /// Er deklariert ein <see cref="Outputs"/> wie der End-Knoten, und das ist Absicht: sonst endet ein
    /// Workflow auf diesem Weg ohne jede Aussage darueber, WARUM. Anders als beim End-Knoten ist die
    /// Quelle des Ergebnisses der Scope des <b>terminierenden Zweigs</b> - er wird dafuer in den
    /// Instanz-Scope veroeffentlicht. Anders geht es nicht: innerhalb einer parallelen Region steht der
    /// Instanz-Scope noch auf dem Stand des Splits, und der Zweig, der abbricht, ist der einzige, der
    /// weiss warum.
    /// </para>
    /// <para>
    /// Er zaehlt NICHT als der eine End-Knoten der Definition - es darf beliebig viele geben, wie beim
    /// <see cref="SidePathEndNode"/>.
    /// </para></remarks>
    public class TerminateEndNode : WorkflowNode, IResultNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.TerminateEnd;

        /// <summary>
        /// Das Ergebnis des Workflows bei Abbruch ueber diesen Knoten - gleiche Bedeutung wie
        /// <see cref="EndNode.Outputs"/>, nur aus dem Scope des terminierenden Zweigs. Leer = der ganze
        /// Stack ist das Ergebnis.
        /// </summary>
        public List<ActivityOutputBinding> Outputs { get; set; } = new List<ActivityOutputBinding>();

        /// <summary>Zusaetzlich zum Ergebnis erhalten bleibende Variablen.</summary>
        public List<string> RetainVariables { get; set; } = new List<string>();
    }

    /// <summary>
    /// Ereignisbasiertes Gateway: wartet auf <b>mehrere</b> Ereignisse gleichzeitig - das erste, das
    /// eintrifft, gewinnt, die uebrigen werden verworfen. „Antwort oder Frist", „Zusage, Absage oder
    /// Rueckfrage".
    /// </summary>
    /// <remarks>
    /// Umgesetzt als Rennen echter Tokens: das Gateway verbraucht sein Token und setzt je Ausgang ein
    /// Kind-Token auf den dahinterliegenden Wartepunkt. Das sind ganz gewoehnliche wartende Tokens - die
    /// Aufgriffs-Abfragen fuer Signale und Timer bleiben damit unveraendert, und ein Rennen kostet keine
    /// Sonderbehandlung im Store. Sobald eines weiterlaeuft, verbraucht die Engine seine Geschwister
    /// (siehe <see cref="Instances.Token.RaceTokenId"/>).
    /// <para>
    /// Deshalb muss hinter JEDEM Ausgang ein Knoten stehen, der auch wirklich <b>parkt</b> (Wartepunkt,
    /// Timer, Benutzer-Aufgabe). Eine automatische Aktivitaet liefe sofort durch und gewaenne jedes
    /// Rennen - das Gateway waere ein stiller Nicht-Effekt. Der Validator lehnt das ab.
    /// </para>
    /// <para>
    /// Die Zweige bekommen bewusst KEINE eigenen Variablen-Kopien wie bei einem parallelen Split: es
    /// ueberlebt genau einer, es gibt also nichts zusammenzufuehren.
    /// </para></remarks>
    public class EventGatewayNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.EventGateway;

        /// <summary>
        /// Darf dieser Knoten hinter einem Ausgang des Gateways stehen? Nur Knoten, die auf ein
        /// <b>Ereignis</b> warten - ein Wartepunkt, ein Timer oder eine Benutzer-Aufgabe.
        /// </summary>
        /// <remarks>
        /// Die Regel liegt am Modell und nicht im Validator, weil sie an mehreren Stellen gebraucht wird
        /// (Pruefung und Oberflaeche) - dieselbe Ueberlegung wie bei
        /// <see cref="BoundaryTimerNode.CanHost"/>. Bewusst NICHT dabei:
        /// <list type="bullet">
        /// <item><description>eine automatische Aktivitaet - sie liefe sofort durch und gewaenne jedes
        /// Rennen, das Gateway waere ein stiller Nicht-Effekt;</description></item>
        /// <item><description>ein Subworkflow-Aufruf - der parkt zwar, startet aber echte Arbeit, die
        /// beim Verlieren des Rennens weggeworfen wuerde;</description></item>
        /// <item><description>eine Aktivitaet mit Ausfuehrungs-Ziel - sie wartet auf einen Runner, nicht
        /// auf ein Ereignis.</description></item>
        /// </list></remarks>
        public static bool CanRace(WorkflowNode node)
        {
            return node is WaitNode or TimerNode or UserActivityNode;
        }
    }

    /// <summary>
    /// Ein <b>Rueckabwicklungs-Pfad</b>: haengt an einem Schritt und beschreibt, wie dessen Wirkung
    /// zurueckgenommen wird („Buchung stornieren" zu „Buchung anlegen"). Er laeuft NICHT im normalen
    /// Fluss - nur, wenn spaeter ein <see cref="CompensateNode"/> die Rueckabwicklung ausloest.
    /// </summary>
    /// <remarks>
    /// Baugleich zum <see cref="BoundaryTimerNode"/>: er haengt ueber
    /// <see cref="AttachedToNodeId"/> an seinem Schritt, hat keine eingehende Kante und startet ueber
    /// seine einzige ausgehende Kante einen Nebenpfad, der in einem
    /// <see cref="SidePathEndNode"/> endet. Der Unterschied liegt darin, WANN er scharf wird und WAS ihn
    /// ausloest: der Fristen-Timer wird beim PARKEN scharf und feuert nach Zeit - dieser hier wird bei der
    /// erfolgreichen VOLLENDUNG des Schritts vorgemerkt und feuert nur auf Zuruf.
    /// <para>
    /// Der Pfad laeuft mit den Variablen, die der Schritt bei seiner Vollendung hinterlassen hat - nicht
    /// mit dem aktuellen Stand. Anders waere er nicht brauchbar: eine Stornierung braucht die
    /// Buchungsnummer von damals, und die kann laengst ueberschrieben sein.
    /// </para></remarks>
    public class CompensationNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Compensation;

        /// <summary>Die Id des Schritts, dessen Wirkung dieser Pfad zurueecknimmt.</summary>
        public string AttachedToNodeId { get; set; }

        /// <summary>
        /// Kann an diesem Knoten ein Rueckabwicklungs-Pfad haengen? Nur an Schritten, die ueberhaupt
        /// etwas <b>bewirken</b> - was nichts tut, ist auch nicht zurueckzunehmen.
        /// </summary>
        /// <remarks>
        /// Die Regel liegt am Modell und nicht im Validator, weil sie an mehreren Stellen gebraucht wird
        /// (Pruefung, Oberflaeche, Vormerkung zur Laufzeit) - dieselbe Ueberlegung wie bei
        /// <see cref="BoundaryTimerNode.CanHost"/>. Ein Wartepunkt oder ein Gateway steht bewusst nicht
        /// dabei: sie hinterlassen nichts, was rueckgaengig zu machen waere.
        /// </remarks>
        public static bool CanCompensate(WorkflowNode node)
        {
            return node is AutomatedActivityNode or CallWorkflowNode or SubProcessNode or UserActivityNode;
        }
    }

    /// <summary>
    /// Loest die <b>Rueckabwicklung</b> aus: die bereits erledigten Schritte mit einem
    /// <see cref="CompensationNode"/> werden in <b>umgekehrter Reihenfolge</b> zurueckgenommen. Der
    /// ausloesende Zweig wartet, bis alles durch ist, und laeuft dann ueber seine einzige ausgehende
    /// Kante weiter.
    /// </summary>
    /// <remarks>
    /// Umgekehrte Reihenfolge ist nicht Geschmackssache: die Schritte bauen aufeinander auf, also muss
    /// der zuletzt gemachte zuerst zurueckgenommen werden (erst die Zahlung stornieren, dann die
    /// Buchung, dann die Reservierung). Und <b>nacheinander</b>, nicht gleichzeitig - eine Stornierung,
    /// die auf einer anderen aufbaut, faende ihre Grundlage sonst schon abgeraeumt vor.
    /// </remarks>
    public class CompensateNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Compensate;

        /// <summary>
        /// Optional: die Id EINES Schritts, der zurueckgenommen wird. Leer = alle vorgemerkten Schritte
        /// der eigenen Ebene (des eigenen Abschnitts bzw. der obersten Ebene).
        /// </summary>
        public string TargetNodeId { get; set; }
    }

    /// <summary>
    /// Ein <b>eingebetteter</b> Teilablauf: seine Knoten liegen im selben Graphen (erkennbar an
    /// <see cref="WorkflowNode.ParentNodeId"/>), er hat einen eigenen Variablen-Scope, aber - anders als
    /// der <see cref="CallWorkflowNode"/> - <b>keine eigene Instanz</b>.
    /// </summary>
    /// <remarks>
    /// Der Unterschied zum Subworkflow-Aufruf ist der Preis: eine Kind-Instanz kostet eine eigene Zeile,
    /// eigenes Monitoring, eigene Versionsbindung und einen Rueck-Link. Das ist richtig, wenn der
    /// Teilablauf fuer sich steht (eigene Definition, eigene Version, wiederverwendbar) - und zu viel,
    /// wenn er nur ein <b>Abschnitt</b> desselben Prozesses ist.
    /// <para>
    /// Der eigentliche Gewinn ist der <b>Fristen-Timer am Abschnitt</b>: der Subprozess-Knoten parkt,
    /// waehrend innen gearbeitet wird, und erfuellt damit
    /// <see cref="BoundaryTimerNode.CanHost"/>. „Die ganze Pruefung muss in 48 Stunden durch sein" liess
    /// sich vorher nicht modellieren - nur je Einzelschritt.
    /// </para>
    /// <para>
    /// Innen gelten dieselben Regeln wie aussen: genau ein Start- und ein End-Knoten <b>je Subprozess</b>
    /// (der Validator zaehlt je Ebene), und ein Ende innen beendet den Abschnitt, nicht den Workflow.
    /// </para></remarks>
    public class SubProcessNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.SubProcess;

        /// <summary>
        /// Wie das Ergebnis des Abschnitts in den aeusseren Scope einfliesst - gleiche Bedeutung wie bei
        /// <see cref="AutomatedActivityNode.Outputs"/>. Leer = alles, was innen entstanden ist, fliesst
        /// nach aussen.
        /// </summary>
        public List<ActivityOutputBinding> Outputs { get; set; } = new List<ActivityOutputBinding>();

        /// <summary>
        /// Wie die Ausgaben einfliessen. Standard <see cref="ActivityScopeMode.Extend"/> (additiv);
        /// <see cref="ActivityScopeMode.Replace"/> macht den Abschnitt zu einer Konsolidierung.
        /// </summary>
        public ActivityScopeMode ScopeMode { get; set; } = ActivityScopeMode.Extend;

        /// <summary>
        /// Bei <see cref="ActivityScopeMode.Replace"/>: Namen aeusserer Variablen, die ueber die
        /// Konsolidierung hinaus erhalten bleiben.
        /// </summary>
        public List<string> RetainVariables { get; set; } = new List<string>();

        /// <summary>
        /// Optionaler <b>Fehler-Ausgang</b>: die Kante, die genommen wird, wenn der Abschnitt scheitert.
        /// Null/leer = ein Fehler innen faultet die Instanz wie bisher.
        /// </summary>
        public string ErrorFlowId { get; set; }

        /// <summary>Beim Fehler-Ausgang: Name der aeusseren Variable fuer die Fehlermeldung.</summary>
        public string ErrorVariable { get; set; }

        /// <summary>
        /// Ob der Abschnitt im Diagramm <b>zugeklappt</b> gezeichnet wird (als einzelner Knoten statt als
        /// Rahmen um seine Knoten). Reine Darstellung - auf den Ablauf hat es keine Wirkung.
        /// </summary>
        /// <remarks>
        /// Gehoert trotzdem in die Definition und nicht in einen Sitzungszustand: wer einen grossen
        /// Prozess aufgeraeumt hat, will ihn beim naechsten Oeffnen so wiederfinden - und ein Leser des
        /// Diagramms soll dasselbe Bild sehen wie der Autor. BPMN legt es aus demselben Grund im
        /// Diagramm-Teil ab.
        /// </remarks>
        public bool Collapsed { get; set; }
    }

    /// <summary>
    /// Ein <b>Nachrichten-Empfang am Schritt</b> (BPMN: Boundary-Message-Event). Er haengt an einem
    /// Schritt, an dem ein Token parkt, und feuert, wenn dort eine Nachricht eintrifft, waehrend
    /// gearbeitet wird.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Das Gegenstueck zum <see cref="BoundaryTimerNode"/> - und die Antwort auf die Frage, die der nicht
    /// beantworten kann: eine laufende Aufgabe liess sich bisher nur ueber eine <b>Frist</b> unterbrechen,
    /// nicht durch ein Ereignis. "Der Kunde storniert, waehrend die Pruefung offen ist" war nicht
    /// modellierbar - der Empfaenger haette am Wartepunkt stehen muessen, und genau das tut er nicht, er
    /// arbeitet ja.
    /// </para>
    /// <para>
    /// <b>Lebensdauer</b> wie beim Fristen-Timer: scharf, sobald das Haupt-Token an seinem Schritt
    /// <b>parkt</b>; verlaesst es den Schritt, werden Empfang UND ein eventuell laufender Nebenpfad
    /// verworfen. An einem Schritt, an dem nie geparkt wird, koennte er nie feuern - der Validator meldet
    /// das (die Regel steht in <see cref="BoundaryTimerNode.CanHost"/>, sie gilt fuer beide).
    /// </para>
    /// <para>
    /// <b>Nicht unterbrechend</b> (Standard) bleibt nach dem Feuern <b>scharf</b> und kann erneut
    /// ausloesen - anders als der Timer, dessen Fristenliste einmal durchlaeuft. Das ist der Unterschied
    /// zwischen "nach 24 Stunden erinnern" und "jedes Mal, wenn der Kunde nachfragt": eine Nachricht kann
    /// beliebig oft kommen, und der Prozess soll nicht nach der ersten taub werden.
    /// </para>
    /// </remarks>
    public class BoundaryMessageNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.BoundaryMessage;

        /// <summary>
        /// Die Id des Schritts, an dem dieser Empfang haengt - dieselbe Regel wie beim Fristen-Timer
        /// (<see cref="BoundaryTimerNode.CanHost"/>): nur dort, wo ein Token tatsaechlich parkt.
        /// </summary>
        public string AttachedToNodeId { get; set; }

        /// <summary>Der Name der Nachricht, auf die dieser Empfang horcht. Pflicht.</summary>
        public string SignalName { get; set; }

        /// <summary>
        /// Ob eine <b>gerichtete Nachricht</b> erwartet wird (Standard) oder ein <b>Rundruf</b> - dieselbe
        /// Unterscheidung wie am gewoehnlichen Wartepunkt.
        /// </summary>
        public WaitKind WaitKind { get; set; } = WaitKind.Message;

        /// <summary>
        /// Optionaler CScript-Ausdruck fuer den <b>Korrelationsschluessel</b> dieses Empfangs - ausgewertet,
        /// wenn der Schritt geparkt wird, und am Empfangs-Token abgelegt. Leer = es gilt der
        /// Korrelationsschluessel der Instanz (oder ihre Id).
        /// </summary>
        /// <remarks>
        /// Ausgewertet wird ueber den Variablen-Stand des HAUPT-Tokens: der Empfang gehoert zu dessen
        /// Schritt, und der Schluessel soll auf das zeigen, woran gerade gearbeitet wird.
        /// </remarks>
        public string CorrelationExpression { get; set; }

        /// <summary>
        /// Wie <see cref="CorrelationExpression"/> zu lesen ist: EIN Ausdruck (Standard) oder ein ganzes
        /// Skript mit <c>return</c>.
        /// </summary>
        public ScriptMode CorrelationExpressionMode { get; set; } = ScriptMode.Expression;

        /// <summary>
        /// <b>Unterbrechend</b>: statt eines Nebenpfads nimmt das HAUPT-Token die ausgehende Kante - der
        /// Schritt gilt damit als abgebrochen (eine wartende Benutzer-Aufgabe verschwindet aus der
        /// Arbeitsliste). Fuer "Storno trifft ein -&gt; Pruefung abbrechen". Standard false.
        /// </summary>
        /// <remarks>
        /// Unterbrechend feuert naturgemaess <b>einmal</b>: danach steht das Token woanders, und es gibt
        /// nichts mehr, woran der Empfang haengen koennte.
        /// </remarks>
        public bool Interrupting { get; set; }

        /// <summary>
        /// Optionaler Variablenname, in dem die Nummer der Ausloesung landet (1 beim ersten Mal). Sie steht
        /// im Scope des Nebenpfads - so kann die Reaktion "zum dritten Mal" anders ausfallen.
        /// </summary>
        public string CountVariable { get; set; }
    }

    /// <summary>
    /// Ein Wartepunkt, der bis zu einem berechneten Zeitpunkt wartet.
    /// </summary>
    public class TimerNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Timer;

        /// <summary>
        /// CScript-Ausdruck, ausgewertet ueber den Variablen der Instanz. Liefert entweder einen
        /// <see cref="System.DateTime"/> (absoluter Faelligkeitszeitpunkt) oder eine
        /// <see cref="System.TimeSpan"/> (Wartedauer ab Betreten des Knotens).
        /// </summary>
        public string DueExpression { get; set; }

        /// <summary>
        /// Wie <see cref="DueExpression"/> zu lesen ist: EIN Ausdruck (Standard) oder ein ganzes Skript
        /// mit <c>return</c>.
        /// </summary>
        public ScriptMode DueExpressionMode { get; set; } = ScriptMode.Expression;
    }

    /// <summary>
    /// Exklusives Gateway (XOR): waehlt genau einen ausgehenden Pfad. Die ausgehenden Kanten tragen
    /// CScript-Bedingungen; die erste erfuellte gewinnt. Trifft keine zu, wird der Default-Ausgang
    /// genommen.
    /// </summary>
    public class ExclusiveGatewayNode : WorkflowNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.ExclusiveGateway;

        /// <summary>
        /// Id der ausgehenden Kante, die genommen wird, wenn keine Bedingung zutrifft. Null
        /// bedeutet: trifft keine Bedingung zu, faellt die Instanz auf Faulted.
        /// </summary>
        public string DefaultFlowId { get; set; }
    }

    /// <summary>
    /// Paralleles Gateway (AND). Mit mehreren Ausgaengen wirkt es als Split (ein Token je Ausgang),
    /// mit mehreren Eingaengen als Join (feuert erst, wenn auf jedem Eingang ein Token liegt).
    /// </summary>
    /// <remarks>
    /// Split/Join sind ausgefuehrt (<c>WorkflowEngine.ProcessParallelGateway</c> bzw.
    /// <c>ResolveJoins</c>). Ob der Knoten als Split oder Join wirkt, entscheidet die ZAHL der
    /// eingehenden Kanten (&lt;= 1 = Split, &gt; 1 = Join); zwei rein und zwei raus ist beides in einem.
    /// <para>
    /// <b>Zweig-Scopes:</b> Der Split gibt jedem Strang eine eigene Kopie des Variablen-Stacks
    /// (<see cref="Instances.Token.Variables"/>) - parallele Zweige koennen einander also nicht mehr
    /// ueberschreiben. Der Join fuehrt die Kopien wieder zusammen: standardmaessig fliessen alle
    /// Aenderungen der Zweige nach oben; mit <see cref="Outputs"/> bestimmt der Join, WAS die parallele
    /// Region als Ergebnis liefert.
    /// </para>
    /// </remarks>
    public class ParallelGatewayNode : WorkflowNode, IMergingGateway
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.ParallelGateway;

        /// <summary>
        /// Als Join: das <b>Ergebnis</b> der parallelen Region.
        /// <see cref="ActivityOutputBinding.Parameter"/> ist der Variablenname im zusammengefuehrten
        /// Zweig-Stand, <see cref="ActivityOutputBinding.Variable"/> der Zielname im Scope, in dem es
        /// weitergeht.
        /// </summary>
        /// <remarks>
        /// Leer (Standard) = alles, was die Zweige geschrieben haben, fliesst nach oben - so verhalten
        /// sich bestehende Definitionen unveraendert. Sind Bindungen deklariert, kommt GENAU das aus der
        /// Region heraus: der Zwischenzustand der Zweige bleibt drin. Das ist zugleich die saubere Antwort
        /// auf gleichnamige Schreibzugriffe in mehreren Zweigen - statt "irgendein Zweig gewinnt"
        /// entscheidet das Mapping (z.B. <c>a_result</c> aus Zweig A und <c>b_result</c> aus Zweig B).
        /// </remarks>
        public List<ActivityOutputBinding> Outputs { get; set; } = new List<ActivityOutputBinding>();

        /// <summary>
        /// Als Join: wie das Ergebnis in den umgebenden Scope einfliesst. Standard
        /// <see cref="ActivityScopeMode.Extend"/> (additiv). <see cref="ActivityScopeMode.Replace"/>
        /// macht den Join zur <b>Konsolidierung</b>: danach besteht der Scope genau aus
        /// <see cref="Outputs"/> plus <see cref="RetainVariables"/>.
        /// </summary>
        public ActivityScopeMode ScopeMode { get; set; } = ActivityScopeMode.Extend;

        /// <summary>
        /// Bei <see cref="ActivityScopeMode.Replace"/>: Namen von Variablen des umgebenden Scopes, die
        /// ueber die Konsolidierung hinaus erhalten bleiben. Bei <see cref="ActivityScopeMode.Extend"/>
        /// ohne Wirkung.
        /// </summary>
        public List<string> RetainVariables { get; set; } = new List<string>();
    }

    /// <summary>
    /// Der gemeinsame Vertrag der Gateways, die parallele Zweige wieder <b>zusammenfuehren</b> (AND und
    /// OR). Die Zusammenfuehrung ist an genau einer Stelle ausgefuehrt; ohne diesen Vertrag muesste sie
    /// je Gateway-Art kopiert werden - und die beiden liefen beim naechsten Detail auseinander, ohne dass
    /// es jemandem auffiele.
    /// </summary>
    public interface IMergingGateway : INodeIdentity
    {
        /// <summary>Anzeigename (fuer Protokoll und Meldungen).</summary>
        string Name { get; }

        /// <summary>Das deklarierte <b>Ergebnis</b> der Region; leer = alles fliesst nach oben.</summary>
        List<ActivityOutputBinding> Outputs { get; }

        /// <summary>Wie das Ergebnis in den umgebenden Scope einfliesst.</summary>
        ActivityScopeMode ScopeMode { get; }

        /// <summary>Bei <see cref="ActivityScopeMode.Replace"/>: was darueber hinaus erhalten bleibt.</summary>
        List<string> RetainVariables { get; }
    }

    /// <summary>
    /// Inklusives Gateway (OR): als Split werden <b>alle zutreffenden</b> Ausgaenge genommen (1 bis n),
    /// als Join wird auf genau die Zweige gewartet, die der zugehoerige Split aktiviert hat.
    /// </summary>
    /// <remarks>
    /// Der Join ist der Grund, warum es diesen Knoten ueberhaupt gesondert gibt. Auf ALLE Eingaenge zu
    /// warten (AND) waere ein Deadlock, sobald ein Zweig nicht genommen wurde; nach dem ERSTEN
    /// weiterzulaufen (XOR-Merge) fuehrt alles Nachfolgende mehrfach aus. Die Frage „kann mich noch
    /// jemand erreichen?" allgemein zu beantworten, ist ueber Bedingungen und Schleifen hinweg nicht
    /// entscheidbar.
    /// <para>
    /// Deshalb der <b>strukturierte</b> Weg: der Split WEISS, wie viele Zweige er aktiviert hat (er hat
    /// die Bedingungen gerade ausgewertet), und stempelt die Zahl auf seine Tokens
    /// (<see cref="Instances.Token.SplitBranchCount"/>). Der Join zaehlt nur noch. Der Preis dafuer ist,
    /// dass Split und Join ein <b>Paar</b> bilden muessen - der Validator prueft das, statt eine
    /// unpaarige Zeichnung still haengen zu lassen.
    /// </para>
    /// </remarks>
    public class InclusiveGatewayNode : WorkflowNode, IMergingGateway
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.InclusiveGateway;

        /// <summary>
        /// Als Split: die Kante, die genommen wird, wenn <b>keine</b> Bedingung zutrifft. Ohne sie ist
        /// „nichts trifft zu" ein Fehler - stillschweigend gar nicht weiterzulaufen hiesse, den Zweig
        /// spurlos zu verlieren.
        /// </summary>
        public string DefaultFlowId { get; set; }

        /// <summary>Als Join: das <b>Ergebnis</b> der Region (siehe <see cref="IMergingGateway"/>).</summary>
        public List<ActivityOutputBinding> Outputs { get; set; } = new List<ActivityOutputBinding>();

        /// <summary>Als Join: wie das Ergebnis in den umgebenden Scope einfliesst.</summary>
        public ActivityScopeMode ScopeMode { get; set; } = ActivityScopeMode.Extend;

        /// <summary>Bei <see cref="ActivityScopeMode.Replace"/>: was darueber hinaus erhalten bleibt.</summary>
        public List<string> RetainVariables { get; set; } = new List<string>();
    }
}
