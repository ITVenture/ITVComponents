using ITVComponents.Workflow.Expressions;
using System.Collections.Generic;

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
    }

    /// <summary>
    /// Endpunkt eines Zweigs. Erreicht ein Token diesen Knoten, wird es verbraucht. Sind danach
    /// keine Tokens mehr aktiv oder wartend, ist der Workflow abgeschlossen.
    /// </summary>
    public class EndNode : WorkflowNode
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
        /// Optionaler CScript-Ausdruck, der ueber den Variablen der Instanz einen
        /// Korrelationsschluessel liefert. Trifft ein Signal mit Korrelationsschluessel ein, findet
        /// es so die richtige wartende Instanz. Null bedeutet Korrelation ueber die Instanz-Id.
        /// </summary>
        public string CorrelationExpression { get; set; }
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
        /// Optionaler CScript-Ausdruck, der den Titel aus den Variablen des Zweigs berechnet (z.B.
        /// "Rechnung " + rechnungsNr). Ist er gesetzt, gewinnt er gegen <see cref="Title"/> - der so
        /// entstandene Titel ist dann aber Klartext und damit <b>nicht</b> mehrsprachig.
        /// </summary>
        public string TitleExpression { get; set; }

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
    /// <b>Wiederholung:</b> <see cref="IntervalsInHours"/> wird der Reihe nach abgearbeitet (z.B. 24, 12 =
    /// erste Erinnerung nach 24h, zweite 12h spaeter). Ist die Liste erschoepft, wiederholt
    /// <see cref="RepeatLast"/> das letzte Intervall endlos; sonst schweigt der Timer.
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
        /// Die Fristen in Stunden, der Reihe nach ab dem Parken. Der erste Wert ist der Abstand zum
        /// Parken, jeder weitere der Abstand zur vorigen Ausloesung.
        /// </summary>
        public List<double> IntervalsInHours { get; set; } = new List<double>();

        /// <summary>
        /// Nach dem letzten Eintrag aus <see cref="IntervalsInHours"/> dieses Intervall endlos
        /// wiederholen? ("danach alle 2 Stunden"). Ohne das schweigt der Timer, wenn die Liste durch ist.
        /// </summary>
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
    public class ParallelGatewayNode : WorkflowNode
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
}
