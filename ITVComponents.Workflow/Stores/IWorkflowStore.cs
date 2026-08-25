using System;
using System.Collections.Generic;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Retention;

namespace ITVComponents.Workflow.Stores
{
    /// <summary>
    /// Persistiert Workflow-Definitionen und -Instanzen und beantwortet die Abfragen, die die
    /// Engine zum Wiederaufnehmen braucht.
    /// </summary>
    /// <remarks>
    /// Bewusst abstrahiert: die Engine haengt nur an diesem Vertrag. Standard-Implementierung wird
    /// ein DB-Store (EFRepo); die In-Memory-Variante dient Tests und einfachen Szenarien. Die
    /// Abfragen <see cref="FindWaitingForSignal"/> und <see cref="FindDueTimers"/> sind der Grund,
    /// warum der Store mehr koennen muss als blosses Ablegen von Bytes.
    /// </remarks>
    public interface IWorkflowStore
    {
        /// <summary>
        /// Legt eine Definition ab (Upsert nach fachlicher Id + Version + Mandant) und schreibt die
        /// vergebene technische Kennung nach <see cref="WorkflowDefinition.Key"/> zurueck.
        /// </summary>
        /// <remarks>
        /// Der Mandant wird dabei festgelegt: <see cref="WorkflowDefinition.IsPublic"/> heisst
        /// oeffentlich (kein Mandant), sonst gilt der am Modell gesetzte - und wenn keiner gesetzt ist,
        /// der Mandant des laufenden Kontexts. „Nichts gesetzt" darf nicht stillschweigend zu
        /// „oeffentlich" werden; oeffentlich ist eine ausdrueckliche Entscheidung.
        /// </remarks>
        void SaveDefinition(WorkflowDefinition definition);

        /// <summary>
        /// Laedt eine Definition ueber ihren <b>sprechenden</b> Namen. Ist <paramref name="version"/>
        /// null, wird die hoechste Version geliefert. Liefert null, wenn nichts gefunden wird.
        /// </summary>
        /// <param name="definitionId">die fachliche Id</param>
        /// <param name="version">die Version, oder null fuer die hoechste</param>
        /// <param name="tenantId">
        /// der Mandant, aus dessen Sicht gesucht wird, oder null fuer den Mandanten des laufenden
        /// Kontexts. Gesucht wird jeweils die <b>eigene</b> Definition dieses Mandanten und ersatzweise
        /// die oeffentliche - die eigene hat Vorrang.
        /// </param>
        /// <remarks>
        /// Der Weg zum <b>Suchen</b>. Ein Verweis, der stehen bleiben soll, gehoert dagegen ueber die
        /// technische Kennung (<see cref="GetDefinition(int)"/>): welcher Name gerade welche Zeile
        /// meint, kann sich aendern, sobald ein Mandant eine eigene Fassung anlegt.
        /// </remarks>
        WorkflowDefinition GetDefinition(string definitionId, int? version = null, string tenantId = null);

        /// <summary>
        /// Laedt eine Definition ueber ihre <b>technische</b> Kennung - eindeutig, ohne Namens- oder
        /// Mandanten-Aufloesung. Liefert null, wenn es sie nicht (mehr) gibt.
        /// </summary>
        WorkflowDefinition GetDefinition(int definitionKey);

        /// <summary>Legt eine Instanz ab (Upsert nach Id). Setzt den Aenderungszeitpunkt.</summary>
        void SaveInstance(WorkflowInstance instance);

        /// <summary>
        /// Speichert die Instanz nur, wenn ihr Stand in der Datenbank noch <paramref name="baseVersion"/>
        /// entspricht (optimistische Nebenlaeufigkeit). Liefert true bei Erfolg (und erhoeht die Version),
        /// false bei einem Versionskonflikt (ein anderer Zweig hat inzwischen committed). Bei false laedt
        /// der Aufrufer neu und wendet sein Delta erneut an - die Aktivitaet selbst wird dabei NICHT erneut
        /// ausgefuehrt. So serialisieren sich gleichzeitige Zweig-Merges derselben Instanz kurz, waehrend
        /// die eigentliche (lange) Ausfuehrung parallel bleibt.
        /// </summary>
        bool TryCommitInstance(WorkflowInstance instance, int baseVersion);

        /// <summary>Laedt eine Instanz, oder null.</summary>
        WorkflowInstance GetInstance(string instanceId);

        /// <summary>
        /// Liefert nur die Dringlichkeit einer Instanz (<see cref="WorkflowInstance.Priority"/>), oder
        /// null, wenn es sie nicht gibt.
        /// </summary>
        /// <remarks>
        /// Eigene Abfrage, weil die Ausfuehrungsschicht den Wert braucht, um einen Auftrag einzureihen -
        /// und dafuer nicht Variablen, Tokens und das ganze Protokoll der Instanz laden soll. Genau dafuer
        /// steht die Prioritaet als eigene Spalte neben dem JSON.
        /// </remarks>
        int? GetInstancePriority(string instanceId);

        /// <summary>
        /// Findet Instanzen mit einem wartenden Token auf das angegebene Signal. Ist
        /// <paramref name="correlationKey"/> gesetzt, werden nur Instanzen geliefert, bei denen der
        /// Schluessel passt - entweder am <b>Wartepunkt</b> (<c>Token.WaitingCorrelation</c>) oder an der
        /// Instanz (Korrelationsschluessel oder Id).
        /// </summary>
        /// <remarks>
        /// Der Schluessel am Wartepunkt ist der spezifischere: er entsteht erst beim Warten und kann
        /// deshalb auf etwas zeigen, das der Prozess selbst gerade erzeugt hat. Eine Instanz kann an
        /// mehreren Stellen auf verschiedene Schluessel warten - deswegen reicht der Filter an der Instanz
        /// allein nicht.
        /// </remarks>
        IEnumerable<WorkflowInstance> FindWaitingForSignal(string signalName, string correlationKey = null);

        /// <summary>
        /// Findet Instanzen mit einem wartenden Token, das einen <b>Rundruf</b> dieses Namens erwartet
        /// (<c>Token.WaitingKind == WaitKind.Signal</c>) - ohne Korrelation.
        /// </summary>
        /// <remarks>
        /// Eigene Abfrage statt eines Filters am Ergebnis von <see cref="FindWaitingForSignal"/>: ein
        /// Rundruf kann tausende Instanzen betreffen, und die Auswahl gehoert in die Datenbank. Sonst
        /// muesste fuer jede Instanz erst ihre Definition geladen werden, nur um die Art des Wartepunkts
        /// zu erfahren.
        /// </remarks>
        IEnumerable<WorkflowInstance> FindWaitingForBroadcast(string signalName);

        /// <summary>Findet Instanzen mit einem faelligen Timer-Token (DueUtc &lt;= nowUtc).</summary>
        /// <remarks>
        /// Reines Lesen - jeder Aufrufer sieht jeden faelligen Timer. Fuer den Aufgriff durch einen
        /// Runner ist <see cref="ClaimDueTimers"/> gedacht; diese Abfrage bleibt fuer Diagnose und
        /// Tests.
        /// </remarks>
        IEnumerable<WorkflowInstance> FindDueTimers(DateTime nowUtc);

        /// <summary>
        /// Nimmt faellige Timer fuer sich in Anspruch und liefert die zugehoerigen Instanzen. Anders als
        /// <see cref="FindDueTimers"/> ist das kein blosses Lesen: die aufgegriffenen Timer werden fuer
        /// die Dauer von <paramref name="lease"/> auf <paramref name="owner"/> gestempelt und sind fuer
        /// andere Runner so lange unsichtbar.
        /// </summary>
        /// <remarks>
        /// Der Anspruch dient der <b>Last</b>, nicht der Korrektheit. Dass ein Timer genau einmal
        /// feuert, sichert weiterhin allein der Versions-Check beim Commit
        /// (<c>WorkflowEngine.ReactivateTimers</c>) - er muss es auch, denn ein Anspruch kann ablaufen,
        /// waehrend sein Halter noch arbeitet. Was der Anspruch verhindert, ist, dass N Runner dieselben
        /// Instanzen laden, um danach N-1 mal am Commit zu scheitern. Genau deshalb darf ein
        /// <b>abgelaufener</b> Anspruch gefahrlos uebernommen werden - ein abgestuerzter Runner haelt
        /// nichts fest.
        /// <para>
        /// Ein Store ohne verteilte Sicht (In-Memory) darf schlicht die faelligen Instanzen liefern.
        /// </para>
        /// </remarks>
        /// <param name="nowUtc">der Jetzt-Zeitpunkt</param>
        /// <param name="owner">
        /// der aufgreifende Runner - derselbe stabile Name wie bei den Zweig-Sperren, damit
        /// <see cref="ReleaseLocksOfOwner"/> beim Neustart auch seine Ansprueche mit abraeumt
        /// </param>
        /// <param name="lease">wie lange der Anspruch gilt</param>
        /// <param name="maxInstances">
        /// Obergrenze je Aufruf; der Rest bleibt fuer den naechsten Poll oder einen anderen Runner
        /// liegen. Ohne sie riesse bei einem Stau der erste Runner alles an sich.
        /// </param>
        /// <returns>die Instanzen, deren faellige Timer dieser Aufruf bekommen hat</returns>
        IEnumerable<WorkflowInstance> ClaimDueTimers(DateTime nowUtc, string owner, TimeSpan lease,
            int maxInstances);

        /// <summary>
        /// Nimmt vorgemerkte, noch nicht zugestellte Nachrichten fuer sich in Anspruch.
        /// </summary>
        /// <param name="owner">der beanspruchende Runner</param>
        /// <param name="lease">wie lange der Anspruch gilt</param>
        /// <param name="maxMessages">Obergrenze je Aufgriff</param>
        /// <returns>die beanspruchten Nachrichten, jeweils mit ihrer Instanz</returns>
        /// <remarks>
        /// Der <b>Nachhol</b>-Weg, nicht der normale: im Regelfall stellt der sendende Prozess unmittelbar
        /// nach seinem Commit selbst zu und raeumt die Vormerkung weg. Was hier auftaucht, ist liegen
        /// geblieben - der Prozess ist zwischen Commit und Zustellung gestorben. Der Anspruch verhindert,
        /// dass mehrere Runner dieselbe Nachricht gleichzeitig nachholen.
        /// </remarks>
        IReadOnlyList<OutgoingMessage> ClaimOutgoingMessages(string owner, TimeSpan lease, int maxMessages);

        /// <summary>
        /// Streicht eine Vormerkung - die Nachricht ist zugestellt.
        /// </summary>
        /// <remarks>
        /// Bewusst NACH dem Zustellen: stirbt der Prozess dazwischen, wird erneut zugestellt. Andersherum
        /// (erst streichen, dann zustellen) waere die Nachricht bei einem Absturz weg - und das ist der
        /// teurere Fehler.
        /// </remarks>
        void CompleteOutgoingMessage(string instanceId, string messageId);

        /// <summary>
        /// Liefert die frueheste NOCH NICHT faellige Timer-Faelligkeit (DueUtc &gt; nowUtc) im Sichtbereich des
        /// Stores, oder null, wenn kein Timer aussteht. Erlaubt einem Background-Worker, den naechsten Poll
        /// exakt auf den naechsten Timer zu legen, statt blind zu pollen.
        /// </summary>
        DateTime? PeekNextTimerDueUtc(DateTime nowUtc);

        /// <summary>
        /// Die <b>Nachrichten-Ausloeser</b> dieses Namens: welche Definitionen sollen anlaufen, wenn eine
        /// Nachricht so heisst? Liefert nie null.
        /// </summary>
        /// <remarks>
        /// Bewusst eine eigene Abfrage und nicht ein Durchsuchen der Definitionen: die Zustellung laeuft
        /// bei JEDER Nachricht durch diesen Weg, und jedes Mal saemtliche Definitions-JSONs auszupacken,
        /// waere eine Last, die mit der Zahl der Prozesse waechst.
        /// </remarks>
        /// <param name="signalName">der Name der eingetroffenen Nachricht</param>
        /// <param name="originTenantId">
        /// der Mandant, aus dem die Nachricht stammt, oder null (kein Ursprung bekannt bzw. bewusst
        /// mandantenfrei)
        /// </param>
        /// <remarks>
        /// <para>
        /// Der <b>Ursprungs-Mandant entscheidet</b>: es springen die Aktivierungen dieses Mandanten an.
        /// Ist kein Ursprung bekannt, springt nur an, was
        /// <see cref="WorkflowStartTrigger.AllowTenantlessStart"/> ausdruecklich erlaubt - sonst
        /// eroeffnete eine einzige namenlose Nachricht in jedem Mandanten einen Vorgang.
        /// </para>
        /// <para>
        /// In einem Ein-Mandanten-Host faellt beides zusammen (alles traegt null) und die Regel wirkt
        /// nicht - dort ist der Ursprung immer "der eine Mandant".
        /// </para>
        /// </remarks>
        WorkflowMessageTriggerLookup FindMessageTriggers(string signalName, string originTenantId);

        /// <summary>
        /// Greift faellige <b>Zeitplan-Ausloeser</b> auf und beansprucht sie fuer diesen Aufrufer -
        /// dasselbe Muster wie <see cref="ClaimDueTimers"/>.
        /// </summary>
        /// <param name="nowUtc">der aktuelle Zeitpunkt (UTC)</param>
        /// <param name="owner">wer aufgreift (Runner-Kennung)</param>
        /// <param name="lease">wie lange der Anspruch gilt</param>
        /// <param name="maxTriggers">Obergrenze je Aufruf</param>
        /// <returns>die aufgegriffenen Ausloeser; nie null</returns>
        /// <remarks>
        /// Der Anspruch ist hier <b>mehr</b> als eine Optimierung. Bei den Timern verhindert er nur
        /// doppelte Arbeit - die Zusicherung traegt dort der versionsgepruefte Commit. Ein Start hat
        /// nichts dergleichen: es gibt noch keine Instanz, deren Version jemanden ausbremsen koennte.
        /// Ohne Anspruch liefe derselbe Zeitplan in einem Verbund aus drei Knoten dreimal an.
        /// </remarks>
        IReadOnlyList<WorkflowStartTriggerMatch> ClaimDueScheduleTriggers(DateTime nowUtc, string owner,
            TimeSpan lease, int maxTriggers);

        /// <summary>
        /// Schreibt den Stand einer Zeitplan-<b>Aktivierung</b> fort und gibt ihren Anspruch frei.
        /// </summary>
        /// <param name="activationKey">die Aktivierung</param>
        /// <param name="nextDueUtc">die naechste Faelligkeit, oder null (kein weiterer Termin)</param>
        /// <param name="lastRunUtc">der Zeitpunkt dieses Laufs, oder null (nicht ausgefuehrt)</param>
        /// <param name="lastInstanceId">die gestartete Instanz, oder null</param>
        /// <remarks>
        /// Der Stand haengt an der Aktivierung und nicht am Ausloeser: fahren drei Mandanten denselben
        /// zentralen Zeitplan, hat jeder seinen eigenen letzten Lauf und seinen eigenen naechsten Termin.
        /// </remarks>
        void UpdateScheduleActivation(int activationKey, DateTime? nextDueUtc, DateTime? lastRunUtc,
            string lastInstanceId);

        /// <summary>
        /// Loest die fachliche Identitaet einer Definition in ihren <b>technischen Schluessel</b> auf -
        /// exakt beim genannten Besitzer, ohne die "eigene schlaegt oeffentliche"-Regel von
        /// <see cref="GetDefinition(string, int?, string)"/>.
        /// </summary>
        /// <param name="ownerTenantId">der Mandant der Definition; null = die oeffentliche</param>
        /// <param name="definitionId">die fachliche Id</param>
        /// <param name="version">die Version, oder null fuer die hoechste</param>
        /// <returns>der Schluessel, oder null</returns>
        /// <remarks>
        /// Der Weg fuer alles, was auf eine BESTIMMTE Definition zeigt und trotzdem ihrer neuesten Fassung
        /// folgen soll - allen voran der Start aus einem Ausloeser. Ueber den Namen allein waere er
        /// zweideutig, sobald ein Mandant eine eigene Fassung gleichen Namens anlegt: die wuerde die
        /// zentrale ueberdecken, und der Zeitplan startete still den falschen Prozess.
        /// </remarks>
        int? ResolveDefinitionKey(string ownerTenantId, string definitionId, int? version = null);

        /// <summary>
        /// Die Ausloeser, die dieser Mandant <b>uebernehmen</b> kann - oeffentliche Definitionen, deren
        /// Start-Knoten das ausdruecklich erlaubt. Ohne Beruecksichtigung von Feature und Berechtigung:
        /// die entscheidet der Mantel, der den Benutzer kennt.
        /// </summary>
        /// <param name="tenantId">der fragende Mandant</param>
        /// <returns>die uebernehmbaren Ausloeser; nie null</returns>
        IReadOnlyList<WorkflowStartTrigger> FindActivatableTriggers(string tenantId);

        /// <summary>
        /// Die Aktivierungen dieses Mandanten - auch die abgehakten (<c>Enabled == false</c>) und die
        /// verwaisten, damit die Uebersicht beide zeigen kann.
        /// </summary>
        /// <param name="tenantId">der Mandant</param>
        /// <returns>seine Aktivierungen; nie null</returns>
        IReadOnlyList<WorkflowStartTriggerActivation> GetActivations(string tenantId);

        /// <summary>
        /// Legt eine Aktivierung an oder aendert sie - erkannt an ihrer fachlichen Identitaet
        /// (Besitzer + Definition + Knoten + Art + Mandant), nicht an <c>ActivationKey</c>.
        /// </summary>
        /// <param name="activation">die Aktivierung; ihr <c>ActivationKey</c> wird nachgetragen</param>
        /// <remarks>
        /// Der Lauf-Zustand einer <b>bestehenden</b> Zeile bleibt dabei unangetastet. Das ist der Grund,
        /// warum Abhaken nicht loescht: haekelt jemand ein halbes Jahr spaeter wieder an, soll der
        /// Zeitplan da weitermachen, wo er war - und nicht ein "sofort"-Kennzeichen ein zweites Mal
        /// ausloesen.
        /// </remarks>
        void SaveActivation(WorkflowStartTriggerActivation activation);

        /// <summary>
        /// Die Widersprueche dieses Mandanten gegen die Aufbewahrungsfristen - der Weg der Oberflaeche,
        /// die zeigt, was er selbst eingestellt hat.
        /// </summary>
        /// <param name="tenantId">der widersprechende Mandant</param>
        /// <returns>seine Widersprueche; nie null</returns>
        /// <remarks>
        /// Auch die zurueckgenommenen (beide Fristen null) sind dabei: sie sind der Nachweis, dass
        /// jemand die Frist einmal angefasst hat, und die Uebersicht soll das zeigen koennen.
        /// </remarks>
        IReadOnlyList<WorkflowRetentionOverride> GetRetentionOverrides(string tenantId);

        /// <summary>
        /// Alle Widersprueche gegen EINE Definition - der Weg des Aufbewahrungslaufs, der eine Definition
        /// in die Hand nimmt und fuer jeden ihrer Mandanten die geltende Frist braucht.
        /// </summary>
        /// <param name="ownerTenantId">der Mandant der Definition; null = die oeffentliche</param>
        /// <param name="definitionId">die fachliche Id der Definition</param>
        /// <returns>die Widersprueche; nie null</returns>
        /// <remarks>
        /// Ueber die fachliche Identitaet und nicht ueber <c>DefinitionKey</c>: der Widerspruch gehoert
        /// der Definition als Ganzem, nicht einer ihrer Fassungen.
        /// </remarks>
        IReadOnlyList<WorkflowRetentionOverride> GetRetentionOverridesForDefinition(string ownerTenantId,
            string definitionId);

        /// <summary>
        /// Legt einen Widerspruch an oder aendert ihn - erkannt an seiner fachlichen Identitaet
        /// (Besitzer + Definition + widersprechender Mandant).
        /// </summary>
        /// <param name="retentionOverride">der Widerspruch; <c>SetUtc</c> wird gesetzt, wenn er leer ist</param>
        /// <remarks>
        /// <b>Es gibt bewusst keinen Loesch-Weg.</b> Eine Ruecknahme setzt beide Fristen auf null - damit
        /// gilt wieder die Vorgabe, aber die Zeile haelt fest, wer sie wann zurueckgenommen hat. Wo das
        /// Loeschen von Daten an einer Einstellung haengt, ist die Spur mehr wert als die aufgeraeumte
        /// Tabelle.
        /// </remarks>
        void SaveRetentionOverride(WorkflowRetentionOverride retentionOverride);

        /// <summary>
        /// Die frueheste noch nicht faellige Zeitplan-Faelligkeit (&gt; nowUtc), oder null. Das Gegenstueck
        /// zu <see cref="PeekNextTimerDueUtc"/>, damit ein Runner auch fuer Zeitplaene gezielt schlafen
        /// kann statt blind zu pollen.
        /// </summary>
        DateTime? PeekNextScheduleDueUtc(DateTime nowUtc);

        /// <summary>
        /// Laeuft (oder wartet) bereits eine Instanz dieser Definition mit diesem Korrelationsschluessel?
        /// Die Frage hinter <see cref="Model.MessageStartMode.StartIfNoneRunning"/>.
        /// </summary>
        /// <param name="definitionKey">die Definition (technischer Schluessel)</param>
        /// <param name="correlationKey">der Korrelationsschluessel; null liefert immer false</param>
        /// <remarks>
        /// Ohne Schluessel ausdruecklich false und nicht "irgendeine laeuft": ein Riegel, der ohne
        /// Unterscheidungsmerkmal nach der ersten Instanz alles Weitere verwirft, waere kein Schutz mehr,
        /// sondern ein Ausschalter.
        /// </remarks>
        bool HasRunningInstance(int definitionKey, string correlationKey);

        /// <summary>
        /// Findet Instanzen mit einem Zweig, der auf eines der angegebenen Ausfuehrungs-Ziele wartet
        /// (Token-Status <see cref="TokenStatus.WaitingForTarget"/> mit passendem
        /// <see cref="Token.WaitingTarget"/>). Damit nimmt ein Runner die Zweige auf, die auf genau die von
        /// ihm bedienten Ziele warten (verteilter Handoff). Eine leere Zielmenge liefert nichts.
        /// </summary>
        IEnumerable<WorkflowInstance> FindBranchesWaitingForTarget(IEnumerable<string> targets);

        /// <summary>
        /// Findet lauffaehige Instanzen (Status <see cref="WorkflowStatus.Running"/>). Damit nimmt
        /// ein startender Dienst Instanzen wieder auf, deren Vortrieb - etwa durch einen Absturz
        /// mitten im Lauf - liegengeblieben ist.
        /// </summary>
        IEnumerable<WorkflowInstance> FindRunnable();

        /// <summary>
        /// Findet die direkten Kind-Instanzen (Subworkflows) der angegebenen Eltern-Instanz. Dient dem
        /// Abbruch einer ganzen Prozess-Kaskade und dem Vortrieb eines Prozessbaums im einfachen Betrieb.
        /// </summary>
        IEnumerable<WorkflowInstance> FindChildInstances(string parentInstanceId);

        /// <summary>
        /// Findet beendete (Completed/Faulted) Subworkflow-Instanzen, auf die noch ein Eltern-Token wartet.
        /// Damit holt ein Runner eine Ergebnis-Zustellung nach, die im schmalen Fenster zwischen
        /// Kind-Abschluss und Eltern-Benachrichtigung (z.B. durch einen Absturz) liegen geblieben ist.
        /// </summary>
        IEnumerable<WorkflowInstance> FindFinishedChildrenWithWaitingParent();

        /// <summary>
        /// Versucht, den Zweig <c>(instanceId, tokenId)</c> fuer den angegebenen Owner (stabiler
        /// Runner-Name) zu sperren - der prozessuebergreifende Ausschluss, damit nicht zwei Runner
        /// denselben Zweig gleichzeitig vorantreiben. Liefert ein Handle bei Erfolg, <c>null</c>, wenn
        /// der Zweig bereits gesperrt ist. Die Sperre hat KEINE TTL (siehe
        /// <see cref="IWorkflowBranchLock"/>).
        /// </summary>
        IWorkflowBranchLock TryAcquireBranchLock(string instanceId, string tokenId, string owner);

        /// <summary>
        /// Gibt alle Sperren des angegebenen Owners frei - Zweig-Sperren und Timer-Ansprueche
        /// (<see cref="ClaimDueTimers"/>). Ein Runner ruft das beim Neustart mit seinem eigenen Namen
        /// auf (ein noch gehaltener Lock nach Neustart bedeutet: er ist mittendrin abgestuerzt) - so
        /// werden die betroffenen Zweige sofort wieder frei, ohne Wartefrist. Dient zugleich als
        /// Admin-/Uebernahme-Operation fuer einen endgueltig toten Runner.
        /// </summary>
        /// <remarks>
        /// Fuer die Timer-Ansprueche ist das reine Beschleunigung: die laufen ohnehin ab. Ohne diesen
        /// Aufruf staende ein neu gestarteter Runner aber bis zum Ablauf vor seinen eigenen, verwaisten
        /// Anspruechen - und zwar genau in dem Moment, in dem er die liegengebliebene Arbeit aufholen soll.
        /// </remarks>
        void ReleaseLocksOfOwner(string owner);
    }
}
