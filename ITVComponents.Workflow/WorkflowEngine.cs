using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Formatting;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Expressions;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Runtime;
using ITVComponents.Workflow.Serialization;
using ITVComponents.Workflow.Stores;

namespace ITVComponents.Workflow
{
    /// <summary>
    /// Treibt Workflow-Instanzen voran: startet sie, verarbeitet aktive Tokens Schritt fuer Schritt
    /// und nimmt sie nach Signalen oder faelligen Timern wieder auf.
    /// </summary>
    /// <remarks>
    /// Phase 0/1: sequenzielle Ausfuehrung mit automatischen Schritten, exklusiven Gateways,
    /// Wartepunkten (Signal/Timer) und Checkpoint nach jedem Knoten. Parallele Gateways (AND) folgen
    /// in Phase 2; bis dahin lehnt die Engine ihr Betreten mit klarer Meldung ab.
    ///
    /// Die Engine geht davon aus, dass eine Instanz nicht gleichzeitig von zwei Threads
    /// vorangetrieben wird - die Nebenlaeufigkeitssteuerung pro Instanz kommt mit der
    /// Ausfuehrungsschicht in einer spaeteren Phase.
    /// </remarks>
    public class WorkflowEngine
    {
        /// <summary>Obergrenze der Knotenausfuehrungen pro Vortrieb - Schutz vor Endlosschleifen.</summary>
        private const int MaxStepsPerAdvance = 100000;

        /// <summary>Maximale Subworkflow-Verschachtelungstiefe - Schutz vor Selbst-/Wechselrekursion.</summary>
        private const int MaxCallDepth = 50;

        private readonly IWorkflowStore store;
        private readonly IActivityHost activities;
        private readonly IExpressionEvaluator evaluator;
        private readonly HashSet<string> hostTargets;

        /// <summary>
        /// Initialisiert die Engine.
        /// </summary>
        /// <param name="store">der Persistenz-Store</param>
        /// <param name="activities">der Host, der je Vortrieb einen Aktivitaets-Scope vergibt</param>
        /// <param name="evaluator">der Ausdrucks-Auswerter, oder null fuer den CScript-Standard</param>
        /// <param name="hostTargets">
        /// Die Ausfuehrungs-Ziele, die DIESER Host/diese Engine bedienen kann (freie Namen, siehe
        /// <see cref="Model.AutomatedActivityNode.ExecutionTarget"/>). Trifft ein Zweig auf einen
        /// Aktivitaets-Knoten mit einem Ziel, das hier nicht enthalten ist, parkt der Zweig
        /// (<see cref="Instances.TokenStatus.WaitingForTarget"/>) und wird von einem Runner mit passendem
        /// Ziel aufgenommen. Null/leer = die Engine fuehrt nur ziel-lose Aktivitaeten aus (der
        /// nicht-verteilte Standard).
        /// </param>
        /// <param name="historyFilter">
        /// Entscheidet, welche Eintraege ueberhaupt ins Ablauf-Protokoll der von dieser Engine
        /// vorangetriebenen Instanzen kommen. Null = der prozessweite
        /// <see cref="WorkflowHistoryFilter.Default"/>. Eine Definition kann die Mindest-Stufe einzeln
        /// uebersteuern (<see cref="WorkflowDefinition.MinHistorySeverity"/>).
        /// </param>
        public WorkflowEngine(IWorkflowStore store, IActivityHost activities,
            IExpressionEvaluator evaluator = null, IEnumerable<string> hostTargets = null,
            IWorkflowHistoryFilter historyFilter = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.activities = activities ?? throw new ArgumentNullException(nameof(activities));
            this.evaluator = evaluator ?? new CScriptExpressionEvaluator();
            this.hostTargets = new HashSet<string>(
                hostTargets ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            HistoryFilter = historyFilter;
            Runtime = new WorkflowRuntimeContext();
        }

        /// <summary>
        /// Der Protokoll-Filter dieser Engine (siehe Konstruktor). Null = der prozessweite
        /// <see cref="WorkflowHistoryFilter.Default"/>.
        /// </summary>
        public IWorkflowHistoryFilter HistoryFilter { get; set; }

        /// <summary>
        /// Die Ausfuehrungs-Ziele, die diese Engine/dieser Host bedient (siehe Konstruktor). Der Runner
        /// liest sie, um Zweige aufzunehmen, die auf genau diese Ziele warten
        /// (<c>IWorkflowStore.FindBranchesWaitingForTarget</c>).
        /// </summary>
        public IReadOnlyCollection<string> HostTargets => hostTargets;

        /// <summary>
        /// Die geteilte Laufzeit-Umgebung dieser Engine (u.a. das <see cref="InstanceGate"/>). Wird an
        /// mitwirkende Dienste (etwa die Worker der Ausfuehrungsschicht) als Property weitergereicht -
        /// siehe <see cref="IWorkflowRuntimeAware"/>.
        /// </summary>
        public WorkflowRuntimeContext Runtime { get; }

        /// <summary>
        /// Legt eine neue Instanz an (mit aktiven Start-Tokens) und persistiert sie, treibt sie aber
        /// NICHT voran. Fuer den nebenlaeufigen Betrieb: der Runner nimmt die aktiven Start-Tokens auf und
        /// treibt sie ueber <see cref="RunBranch"/> voran. Fuer den sequenziellen/einfachen Betrieb siehe
        /// <see cref="StartWorkflow"/>.
        /// </summary>
        /// <param name="definitionId">die Id der Definition</param>
        /// <param name="initialVariables">Startvariablen, oder null</param>
        /// <param name="correlationKey">optionaler Korrelationsschluessel fuer Signale</param>
        /// <param name="priority">
        /// die Dringlichkeit der neuen Instanz (kleinere Zahl = wichtiger, siehe
        /// <see cref="WorkflowPriority"/>), oder null fuer die Vorgabe der Definition
        /// (<see cref="WorkflowDefinition.DefaultPriority"/>) bzw. <see cref="WorkflowPriority.Normal"/>
        /// </param>
        public WorkflowInstance CreateInstance(string definitionId,
            IDictionary<string, object> initialVariables = null, string correlationKey = null,
            int? priority = null)
        {
            WorkflowDefinition definition = store.GetDefinition(definitionId)
                ?? throw new InvalidOperationException($"No definition found for '{definitionId}'.");

            // Eine als fehlerhaft markierte Definition wird gar nicht erst instanziiert - sonst entstuende
            // eine Instanz, die sofort (oder am ersten fehlerhaften Knoten) faultet. Das Flag setzt der
            // Editor beim Speichern anhand der Validierung; bereits laufende Instanzen sind nicht betroffen.
            if (definition.DisabledForStart)
            {
                throw new InvalidOperationException(
                    $"Definition '{definitionId}' (v{definition.Version}) is disabled for start because it has " +
                    "validation errors. Fix the errors in the designer and save again.");
            }

            var startNodes = definition.StartNodes().ToList();
            if (startNodes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Definition '{definitionId}' has no start node.");
            }

            WarnOnMultipleStarts(definition, startNodes);

            var now = DateTime.UtcNow;
            var instance = new WorkflowInstance
            {
                // Der Verweis ist die technische Kennung - ueber Name und Version allein waere ab der
                // ersten mandanteneigenen Fassung desselben Namens nicht mehr entscheidbar, welche
                // Definition gemeint ist. Name und Version stehen als Anzeige daneben.
                DefinitionKey = definition.Key,
                DefinitionId = definition.Id,
                DefinitionVersion = definition.Version,
                Status = WorkflowStatus.Running,
                CorrelationKey = correlationKey,
                Priority = priority ?? definition.DefaultPriority ?? WorkflowPriority.Normal,
                HistoryFilter = FilterFor(definition),
                CreatedUtc = now,
                UpdatedUtc = now
            };

            if (initialVariables != null)
            {
                foreach (KeyValuePair<string, object> pair in initialVariables)
                {
                    instance.Variables[pair.Key] = pair.Value;
                }
            }

            // Die deklarierte Signatur der Definition auf die uebergebenen Werte anwenden (Vorgaben,
            // Umbenennungen, berechnete Parameter). Scheitert sie, entsteht bewusst KEINE Instanz - ein
            // Workflow, dessen Parameter nicht aufloesbar sind, soll gar nicht erst starten.
            try
            {
                ApplyStartInputs(instance, definition, startNodes);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Start parameters of definition '{definition.Id}' v{definition.Version} could not be " +
                    $"resolved: {ex.OutlineException()}", LogSeverity.Error);
                throw new InvalidOperationException(
                    $"Start parameters of definition '{definition.Id}' could not be resolved: {ex.Message}", ex);
            }

            foreach (StartNode start in startNodes)
            {
                instance.Tokens.Add(new Token { NodeId = start.Id, Status = TokenStatus.Active });
            }

            instance.Log("Started");
            store.SaveInstance(instance);
            return instance;
        }

        /// <summary>
        /// Startet eine neue Instanz der angegebenen Definition und treibt sie <b>sequenziell</b> bis zum
        /// ersten Wartepunkt (oder bis zum Ende) voran. Fuer den nebenlaeufigen Betrieb ueber den Runner
        /// stattdessen <see cref="CreateInstance"/> + Zweig-Tasks nutzen.
        /// </summary>
        /// <param name="definitionId">die Id der Definition</param>
        /// <param name="initialVariables">Startvariablen, oder null</param>
        /// <param name="correlationKey">optionaler Korrelationsschluessel fuer Signale</param>
        /// <param name="priority">
        /// die Dringlichkeit der neuen Instanz, oder null fuer die Vorgabe der Definition. Fuer DIESEN
        /// (sequenziellen) Weg ohne Wirkung - der Wert wird nur mitgefuehrt, damit ein spaeter
        /// uebernehmender Runner ihn kennt.
        /// </param>
        /// <returns>die gestartete Instanz</returns>
        public WorkflowInstance StartWorkflow(string definitionId,
            IDictionary<string, object> initialVariables = null, string correlationKey = null,
            int? priority = null)
        {
            WorkflowInstance instance = CreateInstance(definitionId, initialVariables, correlationKey, priority);
            Advance(instance, LoadDefinition(instance));
            return instance;
        }

        /// <summary>
        /// Setzt die Dringlichkeit einer laufenden Instanz neu (kleinere Zahl = wichtiger, siehe
        /// <see cref="WorkflowPriority"/>). Wirkt auf die noch nicht eingereihten Zweige: der naechste
        /// Poll des Runners nimmt sie mit der neuen Stufe auf. Bereits in der Warteschlange stehende
        /// Auftraege behalten ihre alte Stufe - sie sind ohnehin gleich dran.
        /// </summary>
        /// <returns>true, wenn die Aenderung gespeichert wurde; false bei unbekannter Instanz</returns>
        public bool SetPriority(string instanceId, int priority)
        {
            WorkflowInstance instance = store.GetInstance(instanceId);
            if (instance == null)
            {
                LogEnvironment.LogEvent(
                    $"Priority of workflow instance '{instanceId}' could not be changed: no such instance.",
                    LogSeverity.Warning);
                return false;
            }

            if (instance.Priority == priority)
            {
                return true;
            }

            int previous = instance.Priority;
            using (WorkflowExecutionScope.UseTenant(instance.TenantId))
            {
                instance.HistoryFilter = FilterFor(store.GetDefinition(instance.DefinitionId, instance.DefinitionVersion));
                instance.Priority = priority;
                instance.Log("PriorityChanged", detail:
                    $"{WorkflowPriority.Name(previous)} -> {WorkflowPriority.Name(priority)}");

                // Versionsgepruefter Commit: laeuft gerade ein Zweig, gewinnt dessen Commit und die
                // Aenderung muss wiederholt werden - sie darf den Zweig-Fortschritt nicht ueberschreiben.
                if (!store.TryCommitInstance(instance, instance.Version))
                {
                    LogEnvironment.LogEvent(
                        $"Priority of workflow instance '{instanceId}' could not be changed: a concurrent commit " +
                        "won the race. Retry.", LogSeverity.Warning);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Treibt eine Instanz voran, bis kein Token mehr aktiv ist (alle warten oder sind
        /// verbraucht).
        /// </summary>
        /// <param name="instance">die Instanz</param>
        public void Advance(WorkflowInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            Advance(instance, LoadDefinition(instance));
        }

        /// <summary>
        /// Liefert ein Signal an eine wartende Instanz. Alle Tokens, die auf dieses Signal warten,
        /// laufen weiter.
        /// </summary>
        /// <param name="instanceId">die Instanz-Id</param>
        /// <param name="signalName">der Signalname</param>
        /// <param name="payloadVariables">optionale Variablen, die vor dem Weiterlauf gesetzt werden</param>
        /// <returns>true, wenn mindestens ein wartendes Token weitergelaufen ist</returns>
        public bool SignalWorkflow(string instanceId, string signalName,
            IDictionary<string, object> payloadVariables = null, string correlationKey = null)
        {
            return SignalInstance(instanceId, signalName, payloadVariables, correlationKey, broadcast: false);
        }

        /// <summary>
        /// Der gemeinsame Kern der Zustellung an EINE Instanz (inline: speichern und vortreiben).
        /// <paramref name="broadcast"/> entscheidet, welche Wartepunkte angesprochen werden - siehe
        /// <see cref="Accepts"/>.
        /// </summary>
        private bool SignalInstance(string instanceId, string signalName,
            IDictionary<string, object> payloadVariables, string correlationKey, bool broadcast)
        {
            WorkflowInstance instance = store.GetInstance(instanceId)
                ?? throw new InvalidOperationException($"No instance found for '{instanceId}'.");

            var waiting = instance.Tokens
                .Where(t => Accepts(instance, t, signalName, correlationKey, broadcast))
                .ToList();
            if (waiting.Count == 0)
            {
                LogEnvironment.LogEvent(
                    $"Signal '{signalName}' delivered to instance '{instanceId}', but no token was waiting for it.",
                    LogSeverity.Warning);
                return false;
            }

            WorkflowDefinition definition = LoadDefinition(instance);

            foreach (Token token in waiting)
            {
                // Die Payload landet im Scope des EMPFANGENDEN Zweigs - wartet das Token innerhalb einer
                // parallelen Region, gehoert sie in dessen Zweig-Scope, nicht in den (eingefrorenen)
                // Instanz-Scope. Ausserhalb einer Region ist das genau wie bisher.
                ApplyPayload(Scope(instance, token), payloadVariables);
                instance.Log("SignalReceived", token.NodeId, signalName);
                ClearWait(token);
                token.Status = TokenStatus.Active;
                MoveAlongSingleOutgoing(instance, definition, token);
            }

            instance.Status = WorkflowStatus.Running;
            store.SaveInstance(instance);

            Advance(instance, definition);
            return true;
        }

        /// <summary>
        /// Nimmt in der angegebenen Instanz alle Timer wieder auf, deren Faelligkeit erreicht ist.
        /// </summary>
        /// <param name="instance">die Instanz</param>
        /// <param name="nowUtc">der aktuelle Zeitpunkt (UTC)</param>
        /// <returns>true, wenn mindestens ein Timer ausgeloest wurde</returns>
        public bool TriggerTimers(WorkflowInstance instance, DateTime nowUtc)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var due = instance.Tokens
                .Where(t => t.Status == TokenStatus.Waiting && t.DueUtc.HasValue && t.DueUtc.Value <= nowUtc)
                .ToList();
            if (due.Count == 0)
            {
                return false;
            }

            WorkflowDefinition definition = LoadDefinition(instance);
            foreach (Token token in due)
            {
                // Ein Fristen-Timer am Schritt laeuft anders ab als ein Wartepunkt: er loest einen
                // Nebenpfad aus (oder unterbricht), statt selbst weiterzuziehen.
                if (definition.GetNode(token.NodeId) is BoundaryTimerNode boundary)
                {
                    FireBoundaryTimer(instance, definition, token, boundary, nowUtc);
                    continue;
                }

                instance.Log("TimerElapsed", token.NodeId);
                token.DueUtc = null;
                token.WaitingSignal = null;
                token.Status = TokenStatus.Active;
                MoveAlongSingleOutgoing(instance, definition, token);
            }

            instance.Status = WorkflowStatus.Running;
            store.SaveInstance(instance);

            Advance(instance, definition);
            return true;
        }

        /// <summary>
        /// Nimmt ueber alle wartenden Instanzen des Stores die faelligen Timer wieder auf.
        /// </summary>
        /// <param name="nowUtc">der aktuelle Zeitpunkt (UTC)</param>
        public void TriggerDueTimers(DateTime nowUtc)
        {
            foreach (WorkflowInstance instance in store.FindDueTimers(nowUtc).ToList())
            {
                TriggerTimers(instance, nowUtc);
            }
        }

        /// <summary>
        /// Nimmt ein wartendes Token dieses Ereignis an? Die EINE Regel dafuer - sie gilt fuer den
        /// gezielten wie den korrelierten wie den Rundruf-Weg, sonst empfaengt derselbe Wartepunkt je nach
        /// Aufrufweg etwas anderes.
        /// </summary>
        /// <remarks>
        /// Ein Wartepunkt ohne ausgewiesene Art (Altbestand, Benutzer-Aufgabe, Subworkflow) gilt als
        /// <see cref="WaitKind.Message"/> - gerichtet ist die vorsichtigere Annahme.
        /// <para>
        /// Beim gerichteten Weg ohne Korrelationsschluessel greift bewusst KEINE Pruefung: der Aufrufer
        /// hat die Instanz bereits benannt, das IST die Adressierung. Der Schluessel ist nur noetig, wenn
        /// dieselbe Instanz an mehreren Stellen auf denselben Namen wartet.
        /// </para></remarks>
        private static bool Accepts(WorkflowInstance instance, Token token, string signalName,
            string correlationKey, bool broadcast)
        {
            if (token.Status != TokenStatus.Waiting || token.WaitingSignal != signalName)
            {
                return false;
            }

            WaitKind kind = token.WaitingKind ?? WaitKind.Message;
            if (broadcast)
            {
                return kind == WaitKind.Signal;
            }

            if (correlationKey == null)
            {
                return true;
            }

            // Der Schluessel des Wartepunkts schlaegt den der Instanz: er ist der spezifischere und der
            // spaeter entstandene.
            return token.WaitingCorrelation != null
                ? token.WaitingCorrelation == correlationKey
                : instance.CorrelationKey == correlationKey || instance.Id == correlationKey;
        }

        /// <summary>Loescht alle Warte-Anker eines Tokens, das seinen Wartepunkt verlaesst.</summary>
        private static void ClearWait(Token token)
        {
            token.WaitingSignal = null;
            token.WaitingCorrelation = null;
            token.WaitingKind = null;
            token.DueUtc = null;
        }

        /// <summary>
        /// Liefert eine <b>gerichtete Nachricht</b> ueber Korrelation an alle passenden wartenden
        /// Instanzen.
        /// </summary>
        /// <param name="signalName">der Signalname</param>
        /// <param name="correlationKey">
        /// der Korrelationsschluessel - der eines Wartepunkts
        /// (<see cref="WaitNode.CorrelationExpression"/>), der der Instanz oder ihre Id
        /// </param>
        /// <param name="payloadVariables">optionale Variablen, die vor dem Weiterlauf gesetzt werden</param>
        /// <returns>die Anzahl der Instanzen, die weitergelaufen sind</returns>
        /// <remarks>
        /// <b>Ohne Korrelationsschluessel erreicht dieser Weg nur noch Wartepunkte der Art
        /// <see cref="WaitKind.Signal"/></b> (er verhaelt sich dann wie
        /// <see cref="BroadcastSignal"/>). Frueher traf ein schluessselloser Aufruf JEDE Instanz, die auf
        /// den Namen wartete - eine offene Flanke: zwei Vorgaenge desselben Musters weckten einander.
        /// Wer wirklich alle meint, sagt es jetzt mit <see cref="BroadcastSignal"/>.
        /// </remarks>
        public int DeliverSignal(string signalName, string correlationKey = null,
            IDictionary<string, object> payloadVariables = null)
        {
            if (correlationKey == null)
            {
                LogEnvironment.LogEvent(
                    $"Signal '{signalName}' was delivered without a correlation key - it reaches broadcast " +
                    "wait points only. Pass a key to address a message wait, or call BroadcastSignal to say " +
                    "so explicitly.", LogSeverity.Report);
                return BroadcastSignal(signalName, payloadVariables);
            }

            int count = 0;
            foreach (WorkflowInstance instance in store.FindWaitingForSignal(signalName, correlationKey).ToList())
            {
                if (SignalWorkflow(instance.Id, signalName, payloadVariables, correlationKey))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// <b>Rundruf</b>: erreicht jeden Wartepunkt der Art <see cref="WaitKind.Signal"/> mit diesem
        /// Namen in jeder laufenden Instanz - ohne Korrelation. Fuer Ereignisse, die die ganze Anlage
        /// betreffen.
        /// </summary>
        /// <returns>die Anzahl der Instanzen, die weitergelaufen sind</returns>
        /// <remarks>
        /// Je Instanz ein eigener, versionsgeprueter Commit - bewusst keine gemeinsame Transaktion ueber
        /// alle Empfaenger: ein Rundruf kann tausende Instanzen treffen, und eine davon, die gerade
        /// anderweitig committet, darf nicht die restlichen mitreissen.
        /// </remarks>
        public int BroadcastSignal(string signalName, IDictionary<string, object> payloadVariables = null)
        {
            int count = 0;
            foreach (WorkflowInstance instance in store.FindWaitingForBroadcast(signalName).ToList())
            {
                if (SignalInstance(instance.Id, signalName, payloadVariables, null, broadcast: true))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Bricht eine Instanz ab: verbraucht ihre Tokens und setzt den Status auf
        /// <see cref="WorkflowStatus.Cancelled"/>. Bereits <b>beendete</b> Instanzen (Completed,
        /// Cancelled) bleiben unveraendert.
        /// </summary>
        /// <remarks>
        /// Eine <see cref="WorkflowStatus.Faulted"/>-Instanz ist ausdruecklich abbrechbar: sie ist nicht
        /// beendet, sondern haengt - ihre Tokens stehen noch auf ihren Knoten (siehe
        /// <see cref="RetryFaulted"/>). Wer den Wiederaufsatz aufgibt, muss den Fall schliessen koennen,
        /// sonst bliebe er fuer immer in der Uebersicht liegen.
        /// </remarks>
        /// <param name="instanceId">die Instanz-Id</param>
        /// <returns>true, wenn die Instanz abgebrochen wurde</returns>
        public bool CancelWorkflow(string instanceId)
        {
            WorkflowInstance instance = store.GetInstance(instanceId);
            if (instance == null
                || instance.Status == WorkflowStatus.Completed
                || instance.Status == WorkflowStatus.Cancelled)
            {
                return false;
            }

            bool wasFaulted = instance.Status == WorkflowStatus.Faulted;

            foreach (Token token in instance.Tokens)
            {
                token.Status = TokenStatus.Consumed;
            }

            instance.Status = WorkflowStatus.Cancelled;
            // Die Fehlermeldung bleibt bewusst stehen: sie ist der Grund, warum abgebrochen wurde, und
            // waere nach dem Statuswechsel sonst nirgends mehr sichtbar.
            instance.Log("Cancelled", severity: HistorySeverity.Warning,
                detail: wasFaulted ? $"given up after: {instance.FaultMessage}" : null);
            store.SaveInstance(instance);

            // Abbruch-Kaskade: laufende Subworkflows dieser Instanz mit abbrechen (ihr Ergebnis wuerde
            // ohnehin von einem nicht mehr wartenden Elternprozess verworfen).
            foreach (WorkflowInstance child in store.FindChildInstances(instanceId).ToList())
            {
                CancelWorkflow(child.Id);
            }

            return true;
        }

        /// <summary>
        /// Traegt die Korrekturen EINES Zweigs in dessen Scope ein und liefert die geaenderten Namen.
        /// </summary>
        private static IEnumerable<string> ApplyBranchUpdates(WorkflowInstance instance, Token token,
            IDictionary<string, object> updates)
        {
            if (updates == null)
            {
                yield break;
            }

            IDictionary<string, object> scope = ScopeOf(instance, token);
            foreach (KeyValuePair<string, object> pair in updates)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                scope[pair.Key] = pair.Value;
                yield return pair.Key;
            }
        }

        /// <summary>
        /// Marker fuer den einfachen Aufruf (<see cref="RetryFaulted"/>): die Korrekturen gelten dem
        /// Wiederaufsatzpunkt, dessen Token-Id der Aufrufer nicht kennen muss.
        /// </summary>
        private sealed class RetryPointUpdates : Dictionary<string, IDictionary<string, object>>
        {
            public RetryPointUpdates(IDictionary<string, object> updates)
            {
                Updates = updates;
            }

            public IDictionary<string, object> Updates { get; }
        }

        /// <summary>
        /// Bestimmt den Token, an dem ein Wiederaufsatz ansetzen wuerde - den, der beim Fehler noch aktiv
        /// auf seinem Knoten steht. Liefert null, wenn es keinen gibt (dann haengt der Fehler an keinem
        /// Schritt und es gibt nichts zu wiederholen).
        /// </summary>
        /// <remarks>
        /// Bei parallelen Zweigen koennen beim Fault mehrere Tokens aktiv geblieben sein - der Vortrieb
        /// bricht ab, sobald EIN Zweig faultet. Der letzte Fehler-Protokolleintrag benennt den Knoten, um
        /// den es geht; nur wenn der keinen Token traegt, wird auf den ersten aktiven zurueckgefallen.
        /// Oeffentlich, damit Oberflaechen dieselbe Stelle anzeigen, an der der Retry dann wirklich ansetzt.
        /// </remarks>
        public static Token FindRetryPoint(WorkflowInstance instance)
            => FindStalledBranches(instance).FirstOrDefault();

        /// <summary>
        /// Alle Zweige, die beim Fehlschlag stehen geblieben sind - je ein aktives Token. Zuerst die, deren
        /// Knoten im Protokoll als fehlgeschlagen vermerkt ist (der zuletzt gemeldete Fehler vorne), danach
        /// die uebrigen aktiven Tokens: Zweige, die schlicht nicht mehr drankamen, weil der Vortrieb beim
        /// Fault abbrach.
        /// </summary>
        /// <remarks>
        /// Im nebenlaeufigen Betrieb koennen mehrere Zweige gleichzeitig scheitern (<c>RunBranch</c> hat
        /// keine Faulted-Sperre - ein bereits laufender Zweig fuehrt seinen Schritt zu Ende und committet).
        /// Jeder von ihnen hat seinen EIGENEN Scope; eine Korrektur muss deshalb je Zweig moeglich sein.
        /// Oeffentlich, damit die Oberflaeche genau die Zweige anbietet, die der Retry auch anfasst.
        /// </remarks>
        public static IReadOnlyList<Token> FindStalledBranches(WorkflowInstance instance)
        {
            if (instance == null)
            {
                return Array.Empty<Token>();
            }

            // Die Knoten mit Fehler-Eintrag, juengster zuerst - das ist die Reihenfolge, in der ein Mensch
            // sie erwartet (der zuletzt gemeldete Fehler ist der, den er gerade gesehen hat).
            var faultedNodes = new List<string>();
            if (instance.History != null)
            {
                for (int i = instance.History.Count - 1; i >= 0; i--)
                {
                    HistoryEntry h = instance.History[i];
                    if (string.Equals(h.Event, "Faulted", StringComparison.Ordinal)
                        && h.NodeId != null && !faultedNodes.Contains(h.NodeId))
                    {
                        faultedNodes.Add(h.NodeId);
                    }
                }
            }

            var active = instance.ActiveTokens.ToList();
            var ordered = new List<Token>();
            foreach (string nodeId in faultedNodes)
            {
                ordered.AddRange(active.Where(t => t.NodeId == nodeId && !ordered.Contains(t)));
            }

            ordered.AddRange(active.Where(t => !ordered.Contains(t)));
            return ordered;
        }

        /// <summary>
        /// Der Variablen-Scope, in dem ein Token arbeitet: innerhalb einer parallelen Region der
        /// Zweig-Scope des Tokens, sonst der Instanz-Scope. Oeffentlich, damit eine Oberflaeche genau die
        /// Werte zeigt und korrigiert, die der betreffende Schritt auch liest.
        /// </summary>
        public static IDictionary<string, object> ScopeOf(WorkflowInstance instance, Token token)
            => Scope(instance, token);

        /// <summary>
        /// Nimmt eine fehlgeschlagene Instanz an der <b>Fehlerstelle</b> wieder auf: optional werden
        /// Variablen korrigiert, dann faellt der Status auf <see cref="WorkflowStatus.Running"/> zurueck.
        /// Der Token, an dem der Fehler auftrat, steht noch AKTIV auf seinem Knoten - der naechste
        /// Vortrieb (Runner oder <see cref="Advance(WorkflowInstance)"/>) fuehrt genau diesen Schritt
        /// erneut aus. Es wird nichts zurueckgespult und nichts uebersprungen.
        /// </summary>
        /// <param name="instanceId">die Instanz-Id</param>
        /// <param name="variableUpdates">
        /// zu setzende Variablen (Name -&gt; Wert), oder null. Sie gehen in den Scope des fehlgeschlagenen
        /// Tokens - also genau dorthin, wo die Aktivitaet beim naechsten Versuch liest (nach einem Split
        /// ist das der Zweig-Scope, sonst der Instanz-Scope).
        /// </param>
        /// <param name="note">optionale Notiz fuer das Protokoll (z.B. wer korrigiert hat)</param>
        /// <returns>true, wenn die Instanz wieder aufgenommen wurde; false, wenn es sie nicht gibt</returns>
        /// <exception cref="InvalidOperationException">
        /// wenn die Instanz nicht fehlgeschlagen ist oder es keinen Wiederaufsatzpunkt gibt.
        /// </exception>
        public bool RetryFaulted(string instanceId, IDictionary<string, object> variableUpdates = null,
            string note = null)
            => RetryFaultedBranches(instanceId,
                variableUpdates == null ? null : new RetryPointUpdates(variableUpdates), note);

        /// <summary>
        /// Wie <see cref="RetryFaulted"/>, aber mit Korrekturen <b>je Zweig</b>: der Schluessel ist die
        /// Token-Id (siehe <see cref="FindStalledBranches"/>), der Wert sind die zu setzenden Variablen.
        /// Noetig, wenn mehrere Zweige gleichzeitig gescheitert sind - jeder hat seinen eigenen Scope, und
        /// eine Korrektur im einen erreicht den anderen nicht.
        /// </summary>
        /// <param name="instanceId">die Instanz-Id</param>
        /// <param name="updatesByTokenId">
        /// Korrekturen je Token, oder null. Unbekannte Token-Ids werden protokolliert und uebergangen -
        /// ein zwischenzeitlich weitergelaufener Zweig soll den Wiederaufsatz nicht scheitern lassen.
        /// </param>
        /// <param name="note">optionale Notiz fuer das Protokoll</param>
        /// <returns>true, wenn die Instanz wieder aufgenommen wurde; false, wenn es sie nicht gibt</returns>
        public bool RetryFaultedBranches(string instanceId,
            IDictionary<string, IDictionary<string, object>> updatesByTokenId = null, string note = null)
        {
            WorkflowInstance instance = store.GetInstance(instanceId);
            if (instance == null)
            {
                return false;
            }

            if (instance.Status != WorkflowStatus.Faulted)
            {
                // Eine laufende oder beendete Instanz "wieder aufzunehmen" hiesse, sie ein zweites Mal
                // anzustossen bzw. einen Abschluss zurueckzunehmen - beides waere kein Retry.
                throw new InvalidOperationException(
                    $"Instance '{instanceId}' is not faulted (status {instance.Status}) - there is nothing to retry.");
            }

            Token failed = FindRetryPoint(instance);
            if (failed == null)
            {
                // Kein aktives Token = kein Schritt, der wiederholt werden koennte. Das passiert bei
                // instanzweiten Fehlern (z.B. eine Definition, die nicht mehr ladbar ist). Ein Retry
                // muesste raten, wo er ansetzt - besser eine klare Meldung als ein willkuerlicher Neustart.
                throw new InvalidOperationException(
                    $"Instance '{instanceId}' has no active token to resume from. The failure was not tied to a " +
                    "step (see the fault message and the history); it cannot be retried from here.");
            }

            // Korrekturen je Zweig einspielen. RetryPointUpdates ist der Sonderfall "eine Korrektur, gemeint
            // ist der Wiederaufsatzpunkt" - so bleibt der einfache Aufruf einfach.
            var changed = new List<string>();
            if (updatesByTokenId is RetryPointUpdates single)
            {
                changed.AddRange(ApplyBranchUpdates(instance, failed, single.Updates));
            }
            else if (updatesByTokenId != null)
            {
                foreach (KeyValuePair<string, IDictionary<string, object>> pair in updatesByTokenId)
                {
                    Token target = instance.Tokens.FirstOrDefault(t => t.Id == pair.Key);
                    if (target == null)
                    {
                        LogEnvironment.LogEvent(
                            $"Retry of instance '{instanceId}': corrections for token '{pair.Key}' were " +
                            "dropped - no such token (the branch moved on in the meantime).",
                            LogSeverity.Warning);
                        continue;
                    }

                    changed.AddRange(ApplyBranchUpdates(instance, target, pair.Value)
                        .Select(name => $"{target.NodeId}.{name}"));
                }
            }

            string previousFault = instance.FaultMessage;
            instance.Status = WorkflowStatus.Running;
            instance.FaultMessage = null;

            string detail = changed.Count == 0
                ? $"retry after: {previousFault}"
                : $"retry after: {previousFault} (corrected: {string.Join(", ", changed)})";
            instance.Log("Retry", failed.NodeId,
                string.IsNullOrWhiteSpace(note) ? detail : $"{detail} - {note}", HistorySeverity.Warning);

            store.SaveInstance(instance);
            LogEnvironment.LogEvent(
                $"Workflow instance '{instanceId}' was resumed at node '{failed.NodeId}' after a failure " +
                $"({previousFault}); {changed.Count} variable(s) corrected.", LogSeverity.Warning);
            return true;
        }

        /// <summary>
        /// Treibt EINEN Zweig (Token) einer Instanz <b>nebenlaeufigkeits-sicher</b> voran: laedt die
        /// Instanz, fuehrt die Aktivitaet EINMAL aus und committet das Zweig-Delta optimistisch (Retry bei
        /// Versionskonflikt, OHNE die Aktivitaet erneut auszufuehren). Fertige Joins werden im
        /// (serialisierten) Commit gegen frischen Stand aufgeloest - so entscheiden zwei gleichzeitig
        /// ankommende Zweige konfliktfrei, wer die Fortsetzung spawnt. Liefert die Ids der durch diesen
        /// Vortrieb NEU entstandenen aktiven Tokens (Split-Kinder / Join-Fortsetzungen), die der Runner als
        /// eigene Zweig-Tasks einreiht. Der Aufrufer haelt waehrenddessen die Zweig-Sperre.
        /// </summary>
        public IReadOnlyList<string> RunBranch(string instanceId, string tokenId)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            if (tokenId == null)
            {
                throw new ArgumentNullException(nameof(tokenId));
            }

            WorkflowInstance instance = store.GetInstance(instanceId);
            if (instance == null)
            {
                LogEnvironment.LogEvent($"RunBranch: instance '{instanceId}' not found - skipped.",
                    LogSeverity.Warning);
                return Array.Empty<string>();
            }

            Token token = instance.Tokens.FirstOrDefault(t => t.Id == tokenId && t.Status == TokenStatus.Active);
            if (token == null)
            {
                // Der Zweig ist schon verarbeitet (nicht mehr aktiv) - idempotenter Leerlauf; ein
                // doppelter Zweig-Task ist damit harmlos.
                return Array.Empty<string>();
            }

            WorkflowDefinition definition = LoadDefinition(instance);
            var snapshot = new BranchSnapshot(instance);

            // Ausfuehrung EINMAL, rein in-memory (kein Save). AdvanceBranch parkt am Join als Joining und
            // feuert NICHT - der Fire faellt gleich im serialisierten Commit gegen frischen Stand. Die
            // Ausfuehrung laeuft unter dem Tenant der Instanz (tenant-uebergreifender Runner), damit die
            // Aktivitaeten die richtigen Daten sehen.
            using (WorkflowExecutionScope.UseTenant(instance.TenantId))
            using (IActivityScope scope = activities.OpenScope(instance))
            {
                AdvanceBranch(instance, definition, token, scope);
            }

            BranchDelta delta = snapshot.DiffTo(instance);

            const int maxRetries = 100;
            for (int attempt = 0; ; attempt++)
            {
                WorkflowInstance fresh = store.GetInstance(instanceId);
                if (fresh == null)
                {
                    LogEnvironment.LogEvent(
                        $"RunBranch: instance '{instanceId}' vanished during commit - skipped.", LogSeverity.Warning);
                    return Array.Empty<string>();
                }

                int baseVersion = fresh.Version;

                // Nebenlaeufiger Schreibkonflikt: hat ein Geschwister-Zweig seit unserem Fork dieselbe Variable
                // auf einen ANDEREN Wert gesetzt, ist das kein stiller last-writer, sondern ein Fehler (Entscheid:
                // parallele Same-Variable-Writes -> Fault). Gleicher Zielwert = harmlos. Wird gegen JEDEN frischen
                // Stand neu geprueft (ein konfliktierender Zweig kann zwischen den Retries committen).
                string conflictVar = delta.Faulted ? null : FindWriteConflict(snapshot, delta, fresh);
                bool faultedByConflict = conflictVar != null;
                if (faultedByConflict)
                {
                    // Delta NICHT anwenden - stattdessen faulten (die konfliktierende Variable bleibt auf dem
                    // Wert des Geschwister-Zweigs; wir ueberschreiben ihn nicht still).
                    Fault(fresh,
                        $"Variable '{conflictVar}' was written concurrently by parallel branches with conflicting " +
                        "values. Let only one branch write it, or consolidate after the join.");
                }
                else
                {
                    delta.ApplyTo(fresh);
                    if (fresh.Status != WorkflowStatus.Faulted)
                    {
                        // Fertige Joins gegen den frischen Stand aufloesen (kann eine Fortsetzung spawnen).
                        ResolveJoins(fresh, definition);
                    }

                    if (fresh.Status != WorkflowStatus.Faulted && fresh.Status != WorkflowStatus.Cancelled)
                    {
                        UpdateTerminalStatus(fresh, definition);
                    }
                }

                if (store.TryCommitInstance(fresh, baseVersion))
                {
                    if (faultedByConflict)
                    {
                        LogEnvironment.LogEvent(
                            $"RunBranch: parallel write conflict on variable '{conflictVar}' in instance " +
                            $"'{instanceId}' (token '{tokenId}') - instance faulted.", LogSeverity.Error);
                        NotifyParentIfFinished(fresh);
                        return Array.Empty<string>();
                    }

                    // Neu entstandene aktive Tokens (Ids, die es beim Laden noch nicht gab) = neue Zweige.
                    List<string> newTokenIds = fresh.ActiveTokens
                        .Where(t => !snapshot.HasToken(t.Id))
                        .Select(t => t.Id)
                        .ToList();
                    // Ist DIESE Instanz gerade ein beendeter Subworkflow, liefert sie ihr Ergebnis an den
                    // wartenden Elternprozess (der dadurch wieder lauffaehig wird).
                    NotifyParentIfFinished(fresh);
                    return newTokenIds;
                }

                if (attempt >= maxRetries)
                {
                    LogEnvironment.LogEvent(
                        $"RunBranch: giving up after {maxRetries} version conflicts for instance '{instanceId}' " +
                        $"token '{tokenId}'.", LogSeverity.Error);
                    return Array.Empty<string>();
                }
            }
        }

        /// <summary>
        /// Reaktiviert nebenlaeufigkeits-sicher alle Tokens, die auf das angegebene Signal warten
        /// (schiebt sie ueber den Wartepunkt hinaus, setzt optionale Payload-Variablen), OHNE die Zweige
        /// selbst voranzutreiben. Liefert die Ids der nun aktiven Tokens - der Runner reiht sie als
        /// Zweig-Tasks ein. Retry bei Versionskonflikt.
        /// </summary>
        /// <param name="instanceId">die Instanz</param>
        /// <param name="signalName">der Signalname</param>
        /// <param name="payloadVariables">optionale Variablen fuer den Weiterlauf</param>
        /// <param name="correlationKey">
        /// optionaler Korrelationsschluessel: wartet die Instanz an mehreren Stellen auf denselben Namen,
        /// waehlt er den gemeinten Wartepunkt aus. Null = alle Wartepunkte dieses Namens in dieser Instanz
        /// (die Instanz ist ja bereits gezielt angesprochen).
        /// </param>
        /// <param name="broadcast">
        /// true fuer einen <b>Rundruf</b>: dann werden nur Wartepunkte der Art
        /// <see cref="WaitKind.Signal"/> geweckt, unabhaengig von jeder Korrelation
        /// </param>
        public IReadOnlyList<string> ReactivateSignal(string instanceId, string signalName,
            IDictionary<string, object> payloadVariables = null, string correlationKey = null,
            bool broadcast = false)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            return ReactivateAndCommit(instanceId, "ReactivateSignal", (fresh, definition) =>
            {
                var waiting = fresh.Tokens
                    .Where(t => Accepts(fresh, t, signalName, correlationKey, broadcast))
                    .ToList();
                if (waiting.Count == 0)
                {
                    LogEnvironment.LogEvent(
                        $"Signal '{signalName}' delivered to instance '{instanceId}', but no token was waiting for it.",
                        LogSeverity.Warning);
                    return new List<string>();
                }

                var ids = new List<string>();
                foreach (Token token in waiting)
                {
                    // Payload in den Scope des empfangenden Zweigs (siehe SignalWorkflow).
                    ApplyPayload(Scope(fresh, token), payloadVariables);
                    fresh.Log("SignalReceived", token.NodeId, signalName);
                    ClearWait(token);
                    token.Status = TokenStatus.Active;
                    if (!MoveAlongSingleOutgoing(fresh, definition, token))
                    {
                        return ids; // gefaulted - der Commit persistiert den Fault.
                    }

                    ids.Add(token.Id);
                }

                fresh.Status = WorkflowStatus.Running;
                return ids;
            });
        }

        /// <summary>
        /// Reaktiviert nebenlaeufigkeits-sicher alle faelligen Timer-Tokens der Instanz (DueUtc &lt;=
        /// <paramref name="nowUtc"/>), OHNE die Zweige selbst voranzutreiben. Liefert die Ids der nun
        /// aktiven Tokens. Retry bei Versionskonflikt.
        /// </summary>
        public IReadOnlyList<string> ReactivateTimers(string instanceId, DateTime nowUtc)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            return ReactivateAndCommit(instanceId, "ReactivateTimers", (fresh, definition) =>
            {
                var due = fresh.Tokens
                    .Where(t => t.Status == TokenStatus.Waiting && t.DueUtc.HasValue && t.DueUtc.Value <= nowUtc)
                    .ToList();
                if (due.Count == 0)
                {
                    return new List<string>();
                }

                var ids = new List<string>();
                foreach (Token token in due)
                {
                    // Fristen-Timer am Schritt: Nebenpfad ausloesen (oder unterbrechen) statt selbst
                    // weiterzuziehen. Liefert die Id des neu aktiven Tokens, oder null (Timer war stale).
                    if (definition.GetNode(token.NodeId) is BoundaryTimerNode boundary)
                    {
                        string spawned = FireBoundaryTimer(fresh, definition, token, boundary, nowUtc);
                        if (fresh.Status == WorkflowStatus.Faulted)
                        {
                            return ids;
                        }

                        if (spawned != null)
                        {
                            ids.Add(spawned);
                        }

                        continue;
                    }

                    fresh.Log("TimerElapsed", token.NodeId);
                    token.DueUtc = null;
                    token.WaitingSignal = null;
                    token.Status = TokenStatus.Active;
                    if (!MoveAlongSingleOutgoing(fresh, definition, token))
                    {
                        return ids;
                    }

                    ids.Add(token.Id);
                }

                fresh.Status = WorkflowStatus.Running;
                return ids;
            });
        }

        /// <summary>
        /// Nimmt nebenlaeufigkeits-sicher alle Zweige der Instanz auf, die auf eines der angegebenen
        /// Ausfuehrungs-Ziele warten (<see cref="TokenStatus.WaitingForTarget"/> mit passendem
        /// <see cref="Token.WaitingTarget"/>): sie werden wieder aktiv - der Token bleibt aber auf seinem
        /// Aktivitaets-Knoten stehen, damit der anschliessende Zweig-Vortrieb GENAU diese Aktivitaet auf
        /// DIESEM Host ausfuehrt. Der Gegenpart zum Parken (<c>ParkForTarget</c>). Liefert die Ids der nun
        /// aktiven Tokens (fuer Zweig-Tasks). Retry bei Versionskonflikt.
        /// </summary>
        public IReadOnlyList<string> ReactivateForTargets(string instanceId, IEnumerable<string> targets)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            var targetSet = new HashSet<string>(targets ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            if (targetSet.Count == 0)
            {
                return Array.Empty<string>();
            }

            return ReactivateAndCommit(instanceId, "ReactivateForTargets", (fresh, _) =>
            {
                var parked = fresh.Tokens
                    .Where(t => t.Status == TokenStatus.WaitingForTarget
                                && t.WaitingTarget != null && targetSet.Contains(t.WaitingTarget))
                    .ToList();
                if (parked.Count == 0)
                {
                    return new List<string>();
                }

                var ids = new List<string>();
                foreach (Token token in parked)
                {
                    fresh.Log("TargetResumed", token.NodeId, token.WaitingTarget);
                    token.WaitingTarget = null;
                    // NICHT bewegen: der Token steht auf dem Ziel-Aktivitaets-Knoten und wird dort ausgefuehrt.
                    token.Status = TokenStatus.Active;
                    ids.Add(token.Id);
                }

                fresh.Status = WorkflowStatus.Running;
                return ids;
            });
        }

        /// <summary>
        /// Faehrt die Reaktivierungs-Logik (Signal/Timer) unter optimistischer Nebenlaeufigkeit: laedt
        /// frisch, wendet die Mutation an, committet mit Versionspruefung; bei Konflikt neu laden und
        /// erneut anwenden. Reaktivierung fuehrt keine Aktivitaet aus und ist daher voll wiederholbar.
        /// </summary>
        private IReadOnlyList<string> ReactivateAndCommit(string instanceId, string opName,
            Func<WorkflowInstance, WorkflowDefinition, List<string>> reactivate)
        {
            const int maxRetries = 100;
            for (int attempt = 0; ; attempt++)
            {
                WorkflowInstance fresh = store.GetInstance(instanceId);
                if (fresh == null)
                {
                    LogEnvironment.LogEvent($"{opName}: instance '{instanceId}' not found - skipped.",
                        LogSeverity.Warning);
                    return Array.Empty<string>();
                }

                int baseVersion = fresh.Version;
                WorkflowDefinition definition = LoadDefinition(fresh);
                List<string> reactivated = reactivate(fresh, definition);

                bool nothingToDo = (reactivated == null || reactivated.Count == 0)
                                   && fresh.Status != WorkflowStatus.Faulted;
                if (nothingToDo)
                {
                    return Array.Empty<string>(); // kein Commit noetig.
                }

                if (store.TryCommitInstance(fresh, baseVersion))
                {
                    return reactivated ?? (IReadOnlyList<string>)Array.Empty<string>();
                }

                if (attempt >= maxRetries)
                {
                    LogEnvironment.LogEvent(
                        $"{opName}: giving up after {maxRetries} version conflicts for instance '{instanceId}'.",
                        LogSeverity.Error);
                    return Array.Empty<string>();
                }
            }
        }

        private void Advance(WorkflowInstance instance, WorkflowDefinition definition)
        {
            if (instance.Status == WorkflowStatus.Completed
                || instance.Status == WorkflowStatus.Faulted
                || instance.Status == WorkflowStatus.Cancelled)
            {
                return;
            }

            // Ein Aktivitaets-Scope je Vortrieb: Schritt-Plugins werden darin on demand geladen und
            // beim Schliessen wieder freigegeben. Der Scope ist bewusst nur fuer diesen Lauf offen -
            // eine wartende Instanz haelt keine Ressourcen. Die Umsetzung ist traege: kostet nichts,
            // wenn dieser Lauf keine Aktivitaet aufloest.
            using IActivityScope activityScope = activities.OpenScope(instance);

            // Zweig fuer Zweig vorantreiben: einen aktiven Token bis zu seiner naechsten Barriere, dann
            // den naechsten. Sind keine aktiven Tokens mehr da, fertige Joins aufloesen (das kann neue
            // aktive Zweige spawnen). Checkpoint je Zweig bzw. je Join-Fire. Das ist die Einheit, die die
            // nebenlaeufige Ausfuehrung (Phase 3a) pro Zweig-Task ausfuehrt; sequenziell hier nacheinander.
            while (instance.Status != WorkflowStatus.Faulted)
            {
                Token token = instance.ActiveTokens.FirstOrDefault();
                if (token != null)
                {
                    if (!AdvanceBranch(instance, definition, token, activityScope))
                    {
                        break; // AdvanceBranch hat auf Faulted gesetzt.
                    }

                    store.SaveInstance(instance);
                    continue;
                }

                // Keine aktiven Tokens: fertige Joins feuern. Feuert nichts, ist der Vortrieb zu Ende.
                if (!ResolveJoins(instance, definition))
                {
                    break;
                }

                store.SaveInstance(instance);
            }

            if (instance.Status == WorkflowStatus.Running)
            {
                UpdateTerminalStatus(instance, definition);
            }

            store.SaveInstance(instance);
        }

        /// <summary>
        /// Treibt EINEN Zweig (Token) bis zu seiner naechsten Barriere voran: ein Wartepunkt
        /// (Signal/Timer), das Ende, oder ein Gateway, an dem der Token verbraucht wird und Kinder
        /// entstehen (Split) bzw. auf Geschwister wartet (Join). Kinder-Tokens bleiben aktiv fuer eine
        /// eigene Zweig-Runde. Reiner In-Memory-Vortrieb ohne Persistenz - das Festhalten (Checkpoint bzw.
        /// nebenlaeufiger Commit) liegt beim Aufrufer, je Zweig-Barriere. Liefert false, wenn die Instanz
        /// dabei auf Faulted gelaufen ist.
        /// </summary>
        private bool AdvanceBranch(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            IActivityScope activityScope)
        {
            int steps = 0;
            while (token.Status == TokenStatus.Active)
            {
                if (++steps > MaxStepsPerAdvance)
                {
                    Fault(instance,
                        $"Aborted after {MaxStepsPerAdvance} steps - the workflow may contain an endless loop.",
                        token.NodeId);
                    return false;
                }

                if (!ProcessActiveToken(instance, definition, token, activityScope))
                {
                    // ProcessActiveToken hat die Instanz auf Faulted gesetzt.
                    return false;
                }
            }

            // Der EINE Ort, an dem ein Token parkt - egal ob Benutzer-Aufgabe, Subworkflow-Aufruf, Signal,
            // Timer oder Handoff. Hier und nur hier werden die Fristen-Timer des Schritts scharf.
            if (token.Status is TokenStatus.Waiting or TokenStatus.WaitingForTarget)
            {
                return ArmBoundaryTimers(instance, definition, token);
            }

            return true;
        }

        /// <summary>
        /// Verarbeitet einen aktiven Token an seinem Knoten. Liefert false, wenn die Instanz dabei
        /// auf Faulted gelaufen ist.
        /// </summary>
        private bool ProcessActiveToken(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            IActivityScope activityScope)
        {
            WorkflowNode node = definition.GetNode(token.NodeId);
            if (node == null)
            {
                Fault(instance, $"Token stands on unknown node '{token.NodeId}'.");
                return false;
            }

            switch (node)
            {
                case StartNode:
                    instance.Log("Entered", node.Id, node.Name, HistorySeverity.Verbose);
                    return MoveAlongSingleOutgoing(instance, definition, token);

                case SidePathEndNode when token.CompensationOwnerTokenId != null:
                    // Ein Rueckabwicklungs-Pfad ist durch: der naechste vorgemerkte Schritt ist dran -
                    // oder, wenn keiner mehr aussteht, laeuft der ausloesende Zweig weiter.
                    return FinishCompensationStep(instance, definition, token);

                case SidePathEndNode:
                    // Ein Nebenpfad laeuft aus: Token weg, sonst nichts. Kein Ergebnis-Re-Base und kein
                    // Beitrag zum Abschluss des Workflows - der Timer wartet auf sein naechstes Intervall.
                    token.Status = TokenStatus.Consumed;
                    instance.Log("SidePathEnded", node.Id, node.Name, HistorySeverity.Verbose);
                    return true;

                case CompensationNode:
                    // Ein Rueckabwicklungs-Pfad wird nie angeflossen - er haengt an seinem Schritt und
                    // startet nur auf Zuruf. Landet hier ein Token, stimmt etwas am Modell nicht.
                    Fault(instance,
                        $"Token stands on compensation handler '{node.Id}' - a handler is triggered by a " +
                        "compensate node, it must not be the target of a connection.", node.Id);
                    return false;

                case CompensateNode compensate:
                    return StartCompensation(instance, definition, token, compensate);

                case BoundaryTimerNode:
                    // Ein Fristen-Timer wird nie als aktives Token verarbeitet - er wartet, bis er faellig
                    // ist. Landet hier trotzdem eines, stimmt etwas am Modell nicht.
                    Fault(instance,
                        $"Token stands on boundary timer '{node.Id}' as an active step - a boundary timer is " +
                        "armed when its step parks and must not be a target of a connection.", node.Id);
                    return false;

                case EndNode when node.ParentNodeId != null:
                    // Das Ende eines eingebetteten Abschnitts beendet den ABSCHNITT, nicht den Workflow.
                    return FinishSubProcess(instance, definition, token, node.ParentNodeId);

                case EndNode:
                    token.Status = TokenStatus.Consumed;
                    instance.Log("Ended", node.Id, node.Name);
                    if (token.Variables != null)
                    {
                        // Der Zweig endet, ohne durch seinen Join gegangen zu sein - sein Scope wird damit
                        // nie zusammengefuehrt und geht verloren. Das ist ein Modellierungsfehler (der
                        // Validator meldet ihn), aber zur Laufzeit kein Grund, die Instanz zu faulten -
                        // still darf es trotzdem nicht bleiben.
                        instance.Log("BranchScopeDiscarded", node.Id,
                            $"branch ended without its join - {token.Variables.Count} branch variable(s) dropped",
                            HistorySeverity.Warning);
                        LogEnvironment.LogEvent(
                            $"Instance '{instance.Id}': a parallel branch reached end node '{node.Id}' without " +
                            "passing its join - its branch variables are dropped. Route every branch through " +
                            "the join.", LogSeverity.Warning);
                    }

                    return true;

                case AutomatedActivityNode activity:
                    // Verteilter Handoff: kann dieser Host das Ziel der Aktivitaet nicht bedienen, parkt der
                    // Zweig hier und wird von einem Runner mit passendem Ziel aufgenommen (nicht ausgefuehrt).
                    if (!CanExecuteHere(activity))
                    {
                        return ParkForTarget(instance, token, activity);
                    }

                    return RunActivity(instance, definition, token, activity, activityScope);

                case ExclusiveGatewayNode gateway:
                    return RouteExclusive(instance, definition, token, gateway);

                case WaitNode wait:
                    return ParkForSignal(instance, token, wait);

                case TerminateEndNode terminate:
                    return Terminate(instance, definition, token, terminate);

                case EventGatewayNode eventGateway:
                    return ProcessEventGateway(instance, definition, token, eventGateway);

                case SubProcessNode subProcess:
                    return EnterSubProcess(instance, definition, token, subProcess);

                case UserActivityNode userTask:
                    return ParkUserTask(instance, token, userTask);

                case TimerNode timer:
                    return ArmTimer(instance, token, timer);

                case CallWorkflowNode call:
                    return ProcessCallWorkflow(instance, definition, token, call);

                case ParallelGatewayNode parallel:
                    return ProcessParallelGateway(instance, definition, token, parallel);

                case InclusiveGatewayNode inclusive:
                    return ProcessInclusiveGateway(instance, definition, token, inclusive);

                default:
                    Fault(instance, $"Unsupported node type '{node.GetType().Name}' (node '{node.Id}').");
                    return false;
            }
        }

        /// <summary>
        /// Kann dieser Host die Aktivitaet ausfuehren? Ja, wenn sie kein Ziel deklariert (laeuft ueberall)
        /// oder ihr Ziel zu den Zielen dieser Engine gehoert. Deckt den nicht-verteilten Standard ohne
        /// Konfiguration ab (kein Ziel gesetzt -> immer true).
        /// </summary>
        private bool CanExecuteHere(AutomatedActivityNode node)
            => string.IsNullOrEmpty(node.ExecutionTarget) || hostTargets.Contains(node.ExecutionTarget);

        /// <summary>
        /// Parkt einen Zweig an einem Aktivitaets-Knoten, dessen Ziel dieser Host nicht bedient: der Token
        /// bleibt auf dem Knoten stehen (damit der Ziel-Runner GENAU diese Aktivitaet ausfuehrt) und geht in
        /// <see cref="TokenStatus.WaitingForTarget"/> mit dem gesuchten Zielnamen. Kein Fehler - ein
        /// definierter Wartezustand des verteilten Ablaufs; er wird ins Protokoll geschrieben und (auf
        /// Report-Ebene) protokolliert, damit ein nie bedientes Ziel diagnostizierbar bleibt.
        /// </summary>
        private static bool ParkForTarget(WorkflowInstance instance, Token token, AutomatedActivityNode node)
        {
            token.Status = TokenStatus.WaitingForTarget;
            token.WaitingTarget = node.ExecutionTarget;
            token.WaitingSignal = null;
            token.DueUtc = null;
            instance.Log("WaitingForTarget", node.Id, node.ExecutionTarget);
            LogEnvironment.LogEvent(
                $"Branch of instance '{instance.Id}' parked at node '{node.Id}' for execution target " +
                $"'{node.ExecutionTarget}' - waiting for a runner that serves this target.", LogSeverity.Report);
            return true;
        }

        /// <summary>
        /// Parkt einen Zweig an einem <b>Wartepunkt</b>: Signalname, Art (gerichtete Nachricht oder
        /// Rundruf) und - falls der Knoten einen Ausdruck dafuer hat - der aufgeloeste
        /// Korrelationsschluessel gehen an das Token.
        /// </summary>
        /// <remarks>
        /// Der Korrelationsschluessel wird JETZT ausgewertet und nicht beim Zustellen: hier steht der
        /// Variablen-Stack des Zweigs zur Verfuegung, und nur hier ist der Wert eindeutig. Ein Fehler im
        /// Ausdruck faultet die Instanz - ein Wartepunkt, dessen Schluessel nicht berechenbar ist, waere
        /// nie erreichbar, und das faende man erst, wenn die Nachricht ausbleibt.
        /// </remarks>
        private bool ParkForSignal(WorkflowInstance instance, Token token, WaitNode node)
        {
            string correlation = null;
            if (!string.IsNullOrWhiteSpace(node.CorrelationExpression))
            {
                try
                {
                    object value = evaluator.Evaluate(node.CorrelationExpression, Scope(instance, token),
                        node.CorrelationExpressionMode);
                    correlation = value?.ToString();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Correlation of wait node '{node.Id}' in instance '{instance.Id}' could not be " +
                        $"resolved: {ex.OutlineException()}", LogSeverity.Error);
                    Fault(instance, $"Correlation of wait node '{node.Id}' failed: {ex.Message}", node.Id);
                    return false;
                }
            }

            token.Status = TokenStatus.Waiting;
            token.WaitingSignal = node.SignalName;
            token.WaitingKind = node.WaitKind;
            token.WaitingCorrelation = string.IsNullOrWhiteSpace(correlation) ? null : correlation;
            instance.Log("Waiting", node.Id,
                token.WaitingCorrelation == null
                    ? node.SignalName
                    : $"{node.SignalName} [{token.WaitingCorrelation}]");
            return true;
        }

        /// <summary>
        /// Beendet die GANZE Instanz ueber einen <see cref="TerminateEndNode"/>: alle Tokens werden
        /// verbraucht, laufende Subworkflows abgebrochen. Den Uebergang auf
        /// <see cref="WorkflowStatus.Completed"/> macht anschliessend <c>UpdateTerminalStatus</c> von
        /// selbst - es findet schlicht kein lebendes Token mehr vor.
        /// </summary>
        /// <remarks>
        /// Der Scope des terminierenden Zweigs wird vorher in den Instanz-Scope veroeffentlicht. Ohne das
        /// zoege die Ergebnis-Abbildung aus einem Stack, der innerhalb einer parallelen Region noch auf
        /// dem Stand des Splits steht - der Zweig, der abbricht, ist aber der einzige, der weiss, warum.
        /// </remarks>
        private bool Terminate(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            TerminateEndNode node)
        {
            if (token.Variables != null)
            {
                instance.Variables.Clear();
                foreach (KeyValuePair<string, object> pair in token.Variables)
                {
                    instance.Variables[pair.Key] = pair.Value;
                }

                token.Variables = null;
            }

            foreach (Token other in instance.Tokens)
            {
                other.Status = TokenStatus.Consumed;
                other.DueUtc = null;
            }

            instance.Log("Terminated", node.Id, node.Name);

            // Kind-Instanzen wuerden sonst verwaist weiterlaufen - sie haben keinen Elternprozess mehr,
            // der ihr Ergebnis abholt. Bewusster Seiteneffekt mitten im Vortrieb; denselben Weg geht
            // ProcessCallWorkflow beim Anlegen.
            foreach (WorkflowInstance child in store.FindChildInstances(instance.Id).ToList())
            {
                if (child.Status is WorkflowStatus.Running or WorkflowStatus.Waiting or WorkflowStatus.Faulted)
                {
                    CancelWorkflow(child.Id);
                }
            }

            return true;
        }

        /// <summary>
        /// Merkt einen erfolgreich vollendeten Schritt zur <b>Rueckabwicklung</b> vor, falls ein
        /// <see cref="CompensationNode"/> an ihm haengt - mit dem Variablen-Stand von JETZT.
        /// </summary>
        /// <remarks>
        /// Der Schnappschuss ist der Punkt: der Rueckabwicklungs-Pfad laeuft spaeter mit dem Stand, den
        /// der Schritt hinterlassen hat. Die Buchungsnummer, die er zum Stornieren braucht, ist bis dahin
        /// laengst ueberschrieben.
        /// </remarks>
        private static void RecordCompensation(WorkflowInstance instance, WorkflowDefinition definition,
            Token token, string nodeId)
        {
            CompensationNode handler = definition.Nodes.OfType<CompensationNode>()
                .FirstOrDefault(c => c.AttachedToNodeId == nodeId);
            if (handler == null)
            {
                return;
            }

            Dictionary<string, object> scope = Scope(instance, token);
            instance.Compensations.Add(new CompensationEntry
            {
                Sequence = instance.Compensations.Count == 0
                    ? 0
                    : instance.Compensations.Max(c => c.Sequence) + 1,
                NodeId = nodeId,
                HandlerNodeId = handler.Id,
                ScopeNodeId = definition.GetNode(nodeId)?.ParentNodeId,
                Variables = new Dictionary<string, object>(scope, StringComparer.Ordinal)
            });
            instance.Log("CompensationArmed", nodeId, handler.Id, HistorySeverity.Verbose);
        }

        /// <summary>
        /// Loest die Rueckabwicklung aus: der Zweig parkt, und der zuletzt erledigte vorgemerkte Schritt
        /// wird als erster zurueckgenommen.
        /// </summary>
        /// <remarks>
        /// Steht nichts aus, laeuft der Zweig einfach weiter - das ist kein Fehler, sondern der Normalfall
        /// eines Ablaufs, der noch nichts getan hat, was rueckzunehmen waere.
        /// </remarks>
        private bool StartCompensation(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            CompensateNode node)
        {
            instance.Log("Entered", node.Id, node.Name, HistorySeverity.Verbose);
            if (NextCompensation(instance, node) == null)
            {
                instance.Log("CompensateNothingPending", node.Id, node.Name, HistorySeverity.Verbose);
                return MoveAlongSingleOutgoing(instance, definition, token);
            }

            token.Status = TokenStatus.Waiting;
            return RunNextCompensation(instance, definition, token, node);
        }

        /// <summary>
        /// Der naechste zurueckzunehmende Schritt fuer diesen Ausloeser - der <b>zuletzt</b> vollendete,
        /// der noch aussteht, oder null.
        /// </summary>
        /// <remarks>
        /// Umgekehrte Reihenfolge, weil die Schritte aufeinander aufbauen: erst die Zahlung stornieren,
        /// dann die Buchung, dann die Reservierung. Ohne Ziel-Angabe zaehlt die EIGENE Ebene des
        /// Ausloesers - ein Ausloeser in einem Abschnitt nimmt zurueck, was in diesem Abschnitt geschehen
        /// ist, nicht den ganzen Prozess.
        /// </remarks>
        private static CompensationEntry NextCompensation(WorkflowInstance instance, CompensateNode node)
        {
            IEnumerable<CompensationEntry> pending = instance.Compensations.Where(c => !c.Compensated);
            pending = !string.IsNullOrEmpty(node.TargetNodeId)
                ? pending.Where(c => c.NodeId == node.TargetNodeId)
                : pending.Where(c => c.ScopeNodeId == node.ParentNodeId);
            return pending.OrderByDescending(c => c.Sequence).FirstOrDefault();
        }

        /// <summary>
        /// Startet die Ruecknahme des naechsten vorgemerkten Schritts - oder weckt den wartenden Zweig,
        /// wenn keiner mehr aussteht.
        /// </summary>
        private bool RunNextCompensation(WorkflowInstance instance, WorkflowDefinition definition, Token owner,
            CompensateNode node)
        {
            CompensationEntry entry = NextCompensation(instance, node);
            if (entry == null)
            {
                owner.Status = TokenStatus.Active;
                instance.Log("Compensated", node.Id, node.Name);
                return MoveAlongSingleOutgoing(instance, definition, owner);
            }

            if (definition.GetNode(entry.HandlerNodeId) is not CompensationNode handler)
            {
                Fault(instance,
                    $"Compensation handler '{entry.HandlerNodeId}' of node '{entry.NodeId}' does not exist.",
                    node.Id);
                return false;
            }

            IReadOnlyList<SequenceFlow> outgoing = definition.OutgoingFlows(handler.Id);
            if (outgoing.Count != 1)
            {
                Fault(instance,
                    $"Compensation handler '{handler.Id}' must have exactly one outgoing flow, but has " +
                    $"{outgoing.Count}.", handler.Id);
                return false;
            }

            // JETZT als erledigt vormerken, nicht erst am Ende des Pfads: sonst faende der naechste
            // Durchgang denselben Eintrag erneut und der Pfad liefe doppelt.
            entry.Compensated = true;

            var runner = new Token
            {
                NodeId = outgoing[0].TargetId,
                Status = TokenStatus.Active,
                // Mit dem Stand von DAMALS - siehe RecordCompensation.
                Variables = new Dictionary<string, object>(entry.Variables, StringComparer.Ordinal),
                SplitTokenId = owner.SplitTokenId,
                SubProcessOwnerTokenId = owner.SubProcessOwnerTokenId,
                CompensationOwnerTokenId = owner.Id,
                ArrivedViaFlowId = outgoing[0].Id
            };
            instance.Tokens.Add(runner);
            instance.Log("Compensating", entry.NodeId, handler.Name ?? handler.Id);
            return true;
        }

        /// <summary>
        /// Ein Rueckabwicklungs-Pfad ist an seinem Ende angekommen: weiter zum naechsten vorgemerkten
        /// Schritt - oder den wartenden Ausloeser wecken.
        /// </summary>
        private bool FinishCompensationStep(WorkflowInstance instance, WorkflowDefinition definition, Token token)
        {
            token.Status = TokenStatus.Consumed;
            string ownerId = token.CompensationOwnerTokenId;
            token.Variables = null;

            Token owner = instance.Tokens.FirstOrDefault(t => t.Id == ownerId);
            if (owner == null)
            {
                // Der ausloesende Zweig ist weg (Abbruch, Frist, Terminate) - dann ist auch die restliche
                // Rueckabwicklung gegenstandslos. Kein Fehler, aber sichtbar.
                LogEnvironment.LogEvent(
                    $"Compensation in instance '{instance.Id}' finished a step, but its triggering token is " +
                    "gone - the rest is abandoned.", LogSeverity.Report);
                return true;
            }

            if (definition.GetNode(owner.NodeId) is not CompensateNode node)
            {
                Fault(instance,
                    $"Token '{owner.Id}' waits for a compensation but does not stand on a compensate node.",
                    owner.NodeId);
                return false;
            }

            return RunNextCompensation(instance, definition, owner, node);
        }

        /// <summary>
        /// Betritt einen <b>eingebetteten Subprozess</b>: das aeussere Token parkt, und im Innenraum
        /// startet ein eigenes Token mit einer Kopie des Scopes.
        /// </summary>
        /// <remarks>
        /// Das aeussere Token bleibt auf dem Subprozess-Knoten stehen und geht auf
        /// <see cref="TokenStatus.Waiting"/> - genau wie beim Subworkflow-Aufruf. Zwei Dinge haengen
        /// daran: der Fristen-Timer am Abschnitt (er wird beim PARKEN scharf, also greift er hier), und
        /// die Erkennung „der Abschnitt ist fertig" ueber
        /// <see cref="Token.SubProcessOwnerTokenId"/>.
        /// <para>
        /// Bewusst KEINE Warte-Anker (Signal, Timer, Ziel): der Abschnitt wartet auf sich selbst, nicht
        /// auf ein Ereignis von aussen. Ein Signal duerfte ihn nicht weiterschieben.
        /// </para></remarks>
        private bool EnterSubProcess(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            SubProcessNode node)
        {
            StartNode start = definition.StartNodeOf(node.Id);
            if (start == null)
            {
                Fault(instance,
                    $"Sub-process '{node.Id}' has no start node inside it - nothing could begin.", node.Id);
                return false;
            }

            instance.Log("Entered", node.Id, node.Name, HistorySeverity.Verbose);

            var inner = new Token
            {
                NodeId = start.Id,
                Status = TokenStatus.Active,
                // Eigener Scope wie bei einem parallelen Zweig: der Abschnitt arbeitet isoliert, und was
                // herauskommt, entscheidet die Ausgabe-Abbildung des Knotens.
                Variables = CopyScope(Scope(instance, token)),
                SplitTokenId = token.SplitTokenId,
                SubProcessOwnerTokenId = token.Id
            };
            instance.Tokens.Add(inner);

            token.Status = TokenStatus.Waiting;
            return true;
        }

        /// <summary>
        /// Schliesst einen eingebetteten Subprozess ab, sobald sein letztes inneres Token das Ende
        /// erreicht: Ergebnis nach aussen abbilden und das aeussere Token weiterziehen.
        /// </summary>
        /// <remarks>
        /// Der Abschnitt kann innen parallel gelaufen sein - deshalb wird erst geprueft, ob noch ein
        /// lebendes Token dazugehoert. Solange ja, laeuft der Abschnitt weiter und das aeussere Token
        /// bleibt geparkt.
        /// </remarks>
        private bool FinishSubProcess(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            string subProcessId)
        {
            token.Status = TokenStatus.Consumed;
            instance.Log("SubProcessEnded", token.NodeId, null, HistorySeverity.Verbose);

            string ownerId = token.SubProcessOwnerTokenId;
            Token owner = instance.Tokens.FirstOrDefault(t => t.Id == ownerId);
            if (owner == null)
            {
                // Der aeussere Zweig ist weg (Abbruch, Frist, Terminate) - dann ist auch dieser
                // Innenraum gegenstandslos. Kein Fehler, aber sichtbar.
                LogEnvironment.LogEvent(
                    $"Sub-process '{subProcessId}' in instance '{instance.Id}' finished, but its outer token " +
                    "is gone - the section was already abandoned.", LogSeverity.Report);
                return true;
            }

            if (instance.Tokens.Any(t => t.SubProcessOwnerTokenId == ownerId
                                         && t.Status != TokenStatus.Consumed))
            {
                return true; // Innen laeuft noch etwas - der Abschnitt ist noch nicht fertig.
            }

            if (definition.GetNode(subProcessId) is not SubProcessNode node)
            {
                Fault(instance, $"Sub-process node '{subProcessId}' does not exist.", subProcessId);
                return false;
            }

            // Das Ergebnis des Abschnitts kommt aus dem Scope des Tokens, das ihn beendet hat.
            Dictionary<string, object> innerScope = token.Variables ?? new Dictionary<string, object>();
            Dictionary<string, object> outerScope = Scope(instance, owner);

            if (node.Outputs is not { Count: > 0 } && node.ScopeMode == ActivityScopeMode.Extend)
            {
                // Ohne Deklaration fliesst ALLES nach aussen - wie bei einem parallelen Zweig ohne
                // Join-Mapping. Ein Abschnitt ist derselbe Prozess, nur gruppiert; muesste man jede
                // Variable einzeln herausdeklarieren, waere die Voreinstellung eine Falle.
                foreach (KeyValuePair<string, object> pair in innerScope)
                {
                    outerScope[pair.Key] = pair.Value;
                }
            }
            else
            {
                ApplyMappedOutputs(instance, outerScope, node.Id, node.Outputs, node.ScopeMode,
                    node.RetainVariables, innerScope);
            }

            token.Variables = null;
            owner.Status = TokenStatus.Active;
            instance.Log("SubProcessCompleted", node.Id, node.Name);
            return MoveAlongSuccessFlow(instance, definition, owner, node.Id, node.ErrorFlowId);
        }

        /// <summary>
        /// Verwirft alle Tokens im Innenraum eines Subprozesses. Aufgerufen, wenn das aeussere Token
        /// seinen Knoten verlaesst - also wenn ein Fristen-Timer den Abschnitt unterbricht oder er ueber
        /// seinen Fehler-Ausgang verlassen wird.
        /// </summary>
        private static void KillSubProcessTokens(WorkflowInstance instance, string ownerTokenId)
        {
            if (string.IsNullOrEmpty(ownerTokenId))
            {
                return;
            }

            foreach (Token t in instance.Tokens.Where(t => t.SubProcessOwnerTokenId == ownerTokenId
                                                           && t.Status != TokenStatus.Consumed)
                         .ToList())
            {
                t.Status = TokenStatus.Consumed;
                t.DueUtc = null;
                t.Variables = null;
                ClearUserTask(t);
                // Der Innenraum kann selbst Fristen und geschachtelte Abschnitte enthalten.
                KillBoundaryTokens(instance, t.Id);
                KillSubProcessTokens(instance, t.Id);
            }
        }

        /// <summary>
        /// Betritt ein <b>ereignisbasiertes Gateway</b>: das Token wird verbraucht und je Ausgang entsteht
        /// ein Kind-Token, das an seinem Wartepunkt parkt. Das erste, das weiterlaeuft, verbraucht seine
        /// Geschwister (siehe <see cref="KillRaceSiblings"/>).
        /// </summary>
        /// <remarks>
        /// Die Kinder bekommen KEINE eigenen Variablen-Kopien wie bei einem parallelen Split: es ueberlebt
        /// genau eines, es gibt also nichts zusammenzufuehren - und eine Kopie je Zweig wuerde nur die
        /// Frage aufwerfen, wessen Stand der Gewinner mitnimmt.
        /// </remarks>
        private bool ProcessEventGateway(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            EventGatewayNode node)
        {
            IReadOnlyList<SequenceFlow> outgoing = definition.OutgoingFlows(node.Id);
            if (outgoing.Count < 2)
            {
                Fault(instance,
                    $"Event gateway '{node.Id}' has {outgoing.Count} outgoing flow(s) - it needs at least two " +
                    "events to race.", node.Id);
                return false;
            }

            token.Status = TokenStatus.Consumed;
            instance.Log("EventGateway", node.Id, $"{outgoing.Count} event(s) racing");
            return SpawnOutgoing(instance, outgoing, token, asSplit: false, raceTokenId: token.Id);
        }

        /// <summary>
        /// Parkt einen Zweig an einer <b>Benutzer-Aufgabe</b>: das Token wartet (wie an einem Wartepunkt),
        /// traegt aber alles, was die Arbeitsliste zum Finden, Filtern und Anzeigen braucht - Aufgabenart,
        /// Permission, Zustaendigkeit, Titel, Entstehungszeit. Der Gegenpart ist
        /// <see cref="CompleteUserTask"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="Token.WaitingSignal"/> bleibt bewusst leer: eine Aufgabe wird nicht per Signal
        /// erledigt (das weckt alle gleichnamig wartenden Tokens und schreibt ohne Versionsvergleich).
        /// Auch <see cref="Token.DueUtc"/> bleibt leer - eine Frist ist Anzeigeinformation
        /// (<see cref="Token.TaskDueUtc"/>) und darf die Aufgabe nicht vom Timer-Aufgriff weiterschieben
        /// lassen.
        /// </remarks>
        private bool ParkUserTask(WorkflowInstance instance, Token token, UserActivityNode node)
        {
            Dictionary<string, object> scope = Scope(instance, token);

            string assignedTo = null;
            if (!string.IsNullOrWhiteSpace(node.Assignment))
            {
                try
                {
                    assignedTo = evaluator.Evaluate(node.Assignment, scope, node.AssignmentMode)?.ToString();
                }
                catch (Exception ex)
                {
                    // Bewusst ein Fault: waere die Auswertung nur eine Warnung, laege die Aufgabe im Pool -
                    // sichtbar fuer JEDEN mit der Permission. Ein Zuweisungsfehler ist damit kein
                    // kosmetisches Problem, sondern eine Sichtbarkeits-Ausweitung.
                    Fault(instance,
                        $"Assignment expression of user task '{node.Id}' failed: {ex.OutlineException()}",
                        node.Id);
                    return false;
                }
            }

            token.Status = TokenStatus.Waiting;
            token.WaitingSignal = null;
            token.WaitingTarget = null;
            token.DueUtc = null;
            token.TaskKey = node.TaskKey;
            // Leer wird zu null normalisiert - sonst waere "" eine Permission, die NIEMAND hat, und die
            // Aufgabe verschwaende aus jeder Arbeitsliste, obwohl der Knoten "keine Permission noetig"
            // meint. Ein leeres Feld aus dem Editor ist genau dieser Fall.
            token.TaskPermission = string.IsNullOrWhiteSpace(node.RequiredPermission)
                ? null
                : node.RequiredPermission;
            token.AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo;
            token.TaskTitle = ResolveTaskTitle(instance, scope, node);
            token.TaskCreatedUtc = DateTime.UtcNow;
            token.TaskDueUtc = node.DueInHours is > 0
                ? DateTime.UtcNow.AddHours(node.DueInHours.Value)
                : null;

            instance.Log("UserTaskCreated", node.Id,
                token.AssignedTo == null ? node.TaskKey : $"{node.TaskKey} -> {token.AssignedTo}");
            return true;
        }

        /// <summary>
        /// Der Titel, unter dem die Aufgabe in der Arbeitsliste steht: <see cref="UserActivityNode.Title"/>
        /// (Klartext oder Kultur-JSON, uebersetzt wird erst beim Anzeigen), formatiert mit dem
        /// <see cref="UserActivityNode.FormatData"/>-Objekt.
        /// </summary>
        /// <remarks>
        /// Einen eigenen Titel-AUSDRUCK gibt es nicht mehr: <see cref="UserActivityNode.Title"/> ist selbst
        /// ein Format-Prototyp und zieht seine Werte aus demselben <see cref="UserActivityNode.FormatData"/>
        /// wie die Beschreibung. Ein zweiter Weg zum selben Ziel haette nur die Frage aufgeworfen, welcher
        /// gewinnt - und der Ausdrucks-Weg konnte zudem nicht mehrsprachig bleiben.
        /// </remarks>
        /// <remarks>
        /// Der statische Titel wird - falls ein <see cref="UserActivityNode.FormatData"/>-Objekt vorliegt -
        /// hier schon mit den Werten des aktuellen Scopes formatiert (siehe <see cref="ApplyTitleFormat"/>).
        /// Das geschieht bewusst BEIM PARKEN und nicht erst beim Anzeigen: nur so steht der fertige Titel
        /// auch in der Arbeitsliste (eine reine Datenbankabfrage, die kein Skript auswertet), und der Titel
        /// bleibt dabei mehrsprachig - je Kultur-Property wird der Prototyp formatiert, statt den Titel auf
        /// die zufaellige Server-Kultur zu reduzieren.
        /// </remarks>
        private string ResolveTaskTitle(WorkflowInstance instance, Dictionary<string, object> scope,
            UserActivityNode node)
        {
            string title = string.IsNullOrWhiteSpace(node.Title) ? node.Name : node.Title;
            return ApplyTitleFormat(instance, scope, node, title);
        }

        /// <summary>
        /// Formatiert den (statischen) Titel mit dem <see cref="UserActivityNode.FormatData"/>-Objekt, sofern
        /// eines deklariert ist. Ist <paramref name="title"/> ein Kultur-JSON-Objekt, wird JEDE Sprach-Property
        /// als Prototyp formatiert und das Objekt neu zusammengesetzt (bleibt mehrsprachig); sonst wird der
        /// Literal-Text formatiert. Ein Fehler laesst den Titel unformatiert (kosmetisch), wird aber
        /// protokolliert.
        /// </summary>
        private string ApplyTitleFormat(WorkflowInstance instance, Dictionary<string, object> scope,
            UserActivityNode node, string title)
        {
            if (string.IsNullOrWhiteSpace(node.FormatData) || string.IsNullOrEmpty(title))
            {
                return title;
            }

            object data;
            try
            {
                data = evaluator.Evaluate(node.FormatData, scope, node.FormatDataMode);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Format data of user task '{node.Id}' in instance '{instance.Id}' could not be evaluated " +
                    $"for the title: {ex.OutlineException()}", LogSeverity.Warning);
                // Auch in die Instanz-Historie: die Folge ist fuer den Endbenutzer sichtbar (im Titel steht
                // der Prototyp statt der Werte), die Ursache stand bisher aber nur im System-Log. Wer den
                // Vorgang im Monitoring aufmacht, soll sie dort finden.
                instance.Log("FormatDataFailed", node.Id,
                    "the format data could not be evaluated - title and description stay unformatted "
                    + "(an object literal needs script mode with 'return')", HistorySeverity.Warning);
                return title;
            }

            if (data == null)
            {
                return title;
            }

            try
            {
                return FormatMaybeCultureJson(title, data);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Title formatting of user task '{node.Id}' in instance '{instance.Id}' failed: " +
                    $"{ex.OutlineException()}", LogSeverity.Warning);
                return title;
            }
        }

        /// <summary>
        /// Wendet das Format-Datenobjekt auf einen Text an. Ist der Text ein Kultur-JSON-Objekt
        /// (<c>{"de":"...","fr":"..."}</c>), wird JEDE (String-)Property als Prototyp formatiert und das Objekt
        /// neu serialisiert - so bleibt der Text mehrsprachig. Sonst wird der Literal-Text formatiert.
        /// </summary>
        private static string FormatMaybeCultureJson(string text, object data)
        {
            string trimmed = text.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var map = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
                        {
                            map[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                                ? data.FormatText(prop.Value.GetString(), TextFormat.DefaultFormatPolicyWithPrimitives)
                                : prop.Value.ToString();
                        }

                        return JsonSerializer.Serialize(map);
                    }
                }
                catch (JsonException)
                {
                    // Sieht aus wie JSON, ist aber keins - als Literal-Text formatieren (unten).
                }
            }

            return data.FormatText(text, TextFormat.DefaultFormatPolicyWithPrimitives);
        }

        // --- Fristen-Timer am Schritt (Boundary-Timer) --------------------------------------------

        /// <summary>
        /// Stellt die an einem Schritt haengenden Fristen-Timer scharf, sobald das Token dort <b>parkt</b>.
        /// Bewusst erst beim Parken und nicht beim Betreten: an einem Schritt, den das Token synchron
        /// durchlaeuft, koennte ein Timer ohnehin nie feuern - er wuerde nur angelegt und sofort wieder
        /// verworfen.
        /// </summary>
        /// <returns>false, wenn die Instanz dabei auf Faulted gelaufen ist</returns>
        private bool ArmBoundaryTimers(WorkflowInstance instance, WorkflowDefinition definition,
            Token owner)
        {
            foreach (BoundaryTimerNode timer in definition.Nodes.OfType<BoundaryTimerNode>()
                         .Where(b => b.AttachedToNodeId == owner.NodeId))
            {
                // Ein Token kann denselben Schritt mehrfach erreichen (Schleife) oder mehrfach geparkt
                // werden (Signal, Handoff) - je Timer darf trotzdem nur EIN wartendes Token existieren.
                bool alreadyArmed = instance.Tokens.Any(t => t.NodeId == timer.Id
                                                             && t.BoundaryOwnerTokenId == owner.Id
                                                             && t.Status == TokenStatus.Waiting);
                if (alreadyArmed)
                {
                    continue;
                }

                DeadlineOutcome outcome = ResolveDeadline(instance, timer, Scope(instance, owner), 0,
                    out DateTime dueUtc, DateTime.UtcNow);
                if (outcome == DeadlineOutcome.Silent)
                {
                    continue; // keine Frist deklariert - der Validator meldet das bereits.
                }

                if (outcome == DeadlineOutcome.Failed)
                {
                    if (!ReportDeadlineFailure(instance, timer))
                    {
                        return false; // unterbrechender Timer: die Instanz ist gefaultet.
                    }

                    continue;
                }

                instance.Tokens.Add(new Token
                {
                    NodeId = timer.Id,
                    Status = TokenStatus.Waiting,
                    DueUtc = dueUtc,
                    BoundaryOwnerTokenId = owner.Id,
                    BoundaryIteration = 0
                });

                instance.Log("BoundaryTimerArmed", timer.Id,
                    $"{owner.NodeId} due {dueUtc:o}", HistorySeverity.Verbose);
            }

            return true;
        }

        /// <summary>Wie die Aufloesung einer Frist ausgegangen ist.</summary>
        private enum DeadlineOutcome
        {
            /// <summary>Es gibt eine Frist und sie steht fest.</summary>
            Armed,

            /// <summary>Keine (weitere) Frist deklariert - der Timer soll schweigen.</summary>
            Silent,

            /// <summary>Der Ausdruck ist gescheitert oder hat nichts Brauchbares geliefert.</summary>
            Failed
        }

        /// <summary>
        /// Wertet die Frist mit der Nummer <paramref name="iteration"/> aus (0 = die erste). Der Ausdruck
        /// darf eine <see cref="TimeSpan"/> (Dauer ab jetzt), einen <see cref="DateTime"/> (absoluter
        /// Zeitpunkt) oder eine Zahl (Dauer in Stunden) liefern - dieselbe Konvention wie beim
        /// gewoehnlichen Timer, um die Kurzform der frueheren Stunden-Liste erweitert.
        /// </summary>
        /// <param name="nowUtc">
        /// der Zeitpunkt, ab dem eine DAUER zaehlt. Beim Scharfstellen ist das jetzt, beim Nachstellen der
        /// Zeitpunkt des Aufgriffs - ein nachgeholter Aufgriff soll die naechste Frist nicht ab "jetzt"
        /// rechnen und die verstrichene Zeit dadurch verschenken.
        /// </param>
        private DeadlineOutcome ResolveDeadline(WorkflowInstance instance, BoundaryTimerNode timer,
            Dictionary<string, object> scope, int iteration, out DateTime dueUtc, DateTime nowUtc)
        {
            dueUtc = default;
            IReadOnlyList<BoundaryDeadline> deadlines = timer.EffectiveDeadlines();
            if (deadlines.Count == 0)
            {
                return DeadlineOutcome.Silent;
            }

            // Liste erschoepft: entweder die letzte Frist endlos wiederholen oder Ruhe geben.
            BoundaryDeadline deadline = iteration < deadlines.Count
                ? deadlines[iteration]
                : (timer.RepeatLast ? deadlines[deadlines.Count - 1] : null);
            if (deadline == null)
            {
                return DeadlineOutcome.Silent;
            }

            if (string.IsNullOrWhiteSpace(deadline.Expression))
            {
                LogEnvironment.LogEvent(
                    $"Boundary timer '{timer.Id}' in instance '{instance.Id}': deadline #{iteration + 1} has no "
                    + "expression.", LogSeverity.Error);
                return DeadlineOutcome.Failed;
            }

            object value;
            try
            {
                value = evaluator.Evaluate(deadline.Expression, scope, deadline.ExpressionMode);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Boundary timer '{timer.Id}' in instance '{instance.Id}': deadline #{iteration + 1} could "
                    + $"not be evaluated: {ex.OutlineException()}", LogSeverity.Error);
                return DeadlineOutcome.Failed;
            }

            switch (value)
            {
                case DateTime dt:
                    // Ohne Zeitzone als UTC lesen - wie beim gewoehnlichen Timer (siehe ArmTimer).
                    dueUtc = dt.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                        : dt.ToUniversalTime();
                    return DeadlineOutcome.Armed;

                case TimeSpan span:
                    if (span <= TimeSpan.Zero)
                    {
                        LogEnvironment.LogEvent(
                            $"Boundary timer '{timer.Id}' in instance '{instance.Id}': deadline #{iteration + 1} "
                            + $"yielded '{span}' - a duration must be greater than zero.", LogSeverity.Error);
                        return DeadlineOutcome.Failed;
                    }

                    dueUtc = nowUtc + span;
                    return DeadlineOutcome.Armed;

                default:
                    if (TryHours(value, out double hours))
                    {
                        if (hours <= 0)
                        {
                            LogEnvironment.LogEvent(
                                $"Boundary timer '{timer.Id}' in instance '{instance.Id}': deadline "
                                + $"#{iteration + 1} yielded {hours} hours - it must be greater than zero.",
                                LogSeverity.Error);
                            return DeadlineOutcome.Failed;
                        }

                        dueUtc = nowUtc.AddHours(hours);
                        return DeadlineOutcome.Armed;
                    }

                    LogEnvironment.LogEvent(
                        $"Boundary timer '{timer.Id}' in instance '{instance.Id}': deadline #{iteration + 1} "
                        + $"yielded '{value ?? "null"}' - a TimeSpan, a DateTime or a number of hours was "
                        + "expected.", LogSeverity.Error);
                    return DeadlineOutcome.Failed;
            }
        }

        /// <summary>
        /// Eine Zahl als Stundenwert lesen. Bewusst NUR echte Zahlen - ein <c>bool</c> ist zwar
        /// konvertierbar, aber als Frist offensichtlich ein Missverstaendnis, und ein Text ("24") liesse
        /// die Kultur entscheiden, wo der Dezimalpunkt steht.
        /// </summary>
        private static bool TryHours(object value, out double hours)
        {
            switch (value)
            {
                case double d: hours = d; return true;
                case int i: hours = i; return true;
                case long l: hours = l; return true;
                case decimal m: hours = (double)m; return true;
                case float f: hours = f; return true;
                case short s: hours = s; return true;
                case byte b: hours = b; return true;
                default: hours = 0; return false;
            }
        }

        /// <summary>
        /// Meldet eine Frist, die sich nicht bestimmen liess. Ein <b>nicht unterbrechender</b> Timer ist
        /// Beiwerk des Schritts: er wird nicht scharf, der Fehler steht im Log UND in der Instanz-Historie,
        /// der Hauptfluss laeuft weiter - eine tadellose Benutzer-Aufgabe wegen eines Tippfehlers in der
        /// Erinnerung abzuschiessen waere schlimmer als die fehlende Erinnerung. Ein <b>unterbrechender</b>
        /// Timer dagegen IST die Ausstiegstuer des Schritts; faellt er stillschweigend aus, stuende der
        /// Schritt fuer immer - deshalb faultet die Instanz.
        /// </summary>
        /// <returns>true, wenn der Vortrieb weiterlaufen darf; false, wenn gefaultet wurde</returns>
        private static bool ReportDeadlineFailure(WorkflowInstance instance, BoundaryTimerNode timer)
        {
            if (timer.Interrupting)
            {
                Fault(instance,
                    $"Deadline of interrupting boundary timer '{timer.Id}' could not be determined - the step "
                    + "would wait forever (see log for the expression error).", timer.Id);
                return false;
            }

            instance.Log("BoundaryTimerFailed", timer.Id,
                "deadline could not be determined - no reminder is armed (see log)", HistorySeverity.Error);
            return true;
        }

        /// <summary>
        /// Loest einen faelligen Fristen-Timer aus. Nicht unterbrechend entsteht ein ZUSAETZLICHES Token
        /// auf dem Nebenpfad (mit einer Kopie des Scopes des Haupt-Tokens), und der Timer stellt sich auf
        /// sein naechstes Intervall; unterbrechend nimmt das HAUPT-Token die Kante und der Schritt gilt als
        /// abgebrochen. Liefert die Id des nun aktiven Tokens (fuer die Zweig-Tasks des Runners), oder null.
        /// </summary>
        /// <param name="nowUtc">
        /// der Zeitpunkt, zu dem der Aufgriff rechnet. Die naechste Frist wird gegen IHN geprueft, nicht
        /// gegen die Systemuhr: sonst beurteilte ein nachgeholter Aufgriff (oder ein Test mit gestellter
        /// Zeit) eine laengst vergangene Frist als "noch in der Zukunft".
        /// </param>
        private string FireBoundaryTimer(WorkflowInstance instance, WorkflowDefinition definition,
            Token timerToken, BoundaryTimerNode timer, DateTime nowUtc)
        {
            Token owner = instance.Tokens.FirstOrDefault(t => t.Id == timerToken.BoundaryOwnerTokenId);
            bool ownerParked = owner is { Status: TokenStatus.Waiting or TokenStatus.WaitingForTarget };
            if (!ownerParked)
            {
                // Das Haupt-Token ist inzwischen weitergelaufen - der Timer ist gegenstandslos. Kein
                // Fehler: der Aufraeumer und dieser Aufgriff koennen sich ueberholen.
                timerToken.Status = TokenStatus.Consumed;
                timerToken.DueUtc = null;
                return null;
            }

            IReadOnlyList<SequenceFlow> outgoing = definition.OutgoingFlows(timer.Id);
            if (outgoing.Count != 1)
            {
                Fault(instance,
                    $"Boundary timer '{timer.Id}' must have exactly one outgoing flow, but has {outgoing.Count}.",
                    timer.Id);
                return null;
            }

            int iteration = (timerToken.BoundaryIteration ?? 0) + 1;

            if (timer.Interrupting)
            {
                // Der Schritt wird abgebrochen: das Haupt-Token nimmt die Kante. Eine wartende Aufgabe
                // verschwindet damit aus der Arbeitsliste - sonst stuende sie dort weiter, obwohl der
                // Prozess laengst woanders ist. Das Aufraeumen der Timer erledigt MoveToken.
                ClearUserTask(owner);
                owner.WaitingSignal = null;
                owner.WaitingTarget = null;
                owner.WaitingForChildInstanceId = null;
                owner.DueUtc = null;
                owner.Status = TokenStatus.Active;

                Dictionary<string, object> ownerScope = Scope(instance, owner);
                if (!string.IsNullOrEmpty(timer.CountVariable))
                {
                    ownerScope[timer.CountVariable] = iteration;
                }

                instance.Log("BoundaryTimerInterrupted", timer.Id,
                    $"{owner.NodeId} after deadline #{iteration}", HistorySeverity.Warning);
                return MoveToken(instance, owner, outgoing[0]) ? owner.Id : null;
            }

            // Nicht unterbrechend: ein eigenes Token fuer den Nebenpfad. Es bekommt eine KOPIE des Scopes
            // des Haupt-Tokens - was die Eskalation schreibt, darf nicht in den Hauptfluss zurueckfliessen.
            var side = new Token
            {
                NodeId = timer.Id,
                Status = TokenStatus.Active,
                BoundaryOwnerTokenId = owner.Id,
                Variables = CopyScope(Scope(instance, owner)) ?? new Dictionary<string, object>(StringComparer.Ordinal)
            };
            if (!string.IsNullOrEmpty(timer.CountVariable))
            {
                side.Variables[timer.CountVariable] = iteration;
            }

            instance.Tokens.Add(side);

            // Den Timer auf seine naechste Frist stellen, BEVOR der Nebenpfad laeuft - so bleibt die Frist
            // auch dann gesetzt, wenn der Nebenpfad gleich faultet.
            timerToken.BoundaryIteration = iteration;
            DeadlineOutcome outcome = ResolveDeadline(instance, timer, Scope(instance, owner), iteration,
                out DateTime nextUtc, nowUtc);

            // Eine naechste Frist, die NICHT in der Zukunft liegt, laesst den Timer verstummen statt in
            // einer Schleife zu feuern. Der Fall entsteht mit "letzte Frist wiederholen" und einem
            // absoluten Zeitpunkt: der bliebe fuer immer derselbe und waere ab sofort vergangen.
            bool inFuture = outcome == DeadlineOutcome.Armed && nextUtc > nowUtc;
            if (inFuture)
            {
                timerToken.DueUtc = nextUtc;
            }
            else
            {
                timerToken.Status = TokenStatus.Consumed;
                timerToken.DueUtc = null;

                if (outcome == DeadlineOutcome.Failed)
                {
                    // Hier immer nur melden: ein unterbrechender Timer kommt nie hierher (er nimmt oben
                    // die Kante und stellt sich nicht neu), und der Schritt selbst laeuft weiter.
                    instance.Log("BoundaryTimerFailed", timer.Id,
                        "the next deadline could not be determined - the timer stops here (see log)",
                        HistorySeverity.Error);
                }
                else if (outcome == DeadlineOutcome.Armed)
                {
                    LogEnvironment.LogEvent(
                        $"Boundary timer '{timer.Id}' in instance '{instance.Id}': the next deadline "
                        + $"({nextUtc:o}) is not in the future - the timer was stopped instead of firing "
                        + "repeatedly. Use a duration rather than an absolute time when repeating.",
                        LogSeverity.Warning);
                    instance.Log("BoundaryTimerStopped", timer.Id,
                        "the next deadline is not in the future", HistorySeverity.Warning);
                }
            }

            instance.Log("BoundaryTimerElapsed", timer.Id,
                $"{owner.NodeId}, escalation #{iteration}", HistorySeverity.Warning);
            return MoveToken(instance, side, outgoing[0]) ? side.Id : null;
        }

        /// <summary>
        /// Verwirft alle Tokens, die zu einem Haupt-Token gehoeren: den wartenden Timer und einen eventuell
        /// laufenden Nebenpfad. Aufgerufen, sobald das Haupt-Token seinen Schritt verlaesst.
        /// </summary>
        private static void KillBoundaryTokens(WorkflowInstance instance, string ownerTokenId)
        {
            if (string.IsNullOrEmpty(ownerTokenId))
            {
                return;
            }

            foreach (Token t in instance.Tokens.Where(t => t.BoundaryOwnerTokenId == ownerTokenId
                                                           && t.Status != TokenStatus.Consumed))
            {
                t.Status = TokenStatus.Consumed;
                t.DueUtc = null;
            }
        }

        /// <summary>
        /// Entscheidet das Rennen an einem ereignisbasierten Gateway: laeuft ein Token weiter, das dort um
        /// die Wette gewartet hat, werden seine Geschwister verbraucht - samt der an IHNEN haengenden
        /// Fristen-Timer und Nebenpfade.
        /// </summary>
        /// <remarks>
        /// Beim Gewinner wird die Zugehoerigkeit geloescht: er ist ab hier ein gewoehnliches Token. Ohne
        /// das wuerde ein spaeterer Durchlauf desselben Gateways (Wiederholungs-Schleife) ihn faelschlich
        /// zu den Geschwistern des neuen Rennens zaehlen - die Gateway-Id ist dieselbe, die Token-Id des
        /// neuen Rennens aber nicht.
        /// </remarks>
        private static void KillRaceSiblings(WorkflowInstance instance, Token winner)
        {
            string raceId = winner.RaceTokenId;
            if (string.IsNullOrEmpty(raceId))
            {
                return;
            }

            foreach (Token sibling in instance.Tokens
                         .Where(t => t.RaceTokenId == raceId && !ReferenceEquals(t, winner)
                                     && t.Status != TokenStatus.Consumed)
                         .ToList())
            {
                sibling.Status = TokenStatus.Consumed;
                sibling.DueUtc = null;
                sibling.WaitingSignal = null;
                sibling.WaitingCorrelation = null;
                sibling.WaitingKind = null;
                ClearUserTask(sibling);
                KillBoundaryTokens(instance, sibling.Id);
            }

            winner.RaceTokenId = null;
        }

        /// <summary>
        /// Sicherheitsnetz: verwirft Timer- und Nebenpfad-Tokens, deren Haupt-Token nicht mehr parkt. Das
        /// eigentliche Aufraeumen macht <see cref="MoveToken"/> beim Weiterziehen; diese Sonde faengt die
        /// Wege ab, auf denen ein Haupt-Token OHNE Bewegung verschwindet (Ende, Join, Abbruch) - sonst
        /// bliebe ein wartendes Timer-Token stehen und die Instanz koennte nie abschliessen.
        /// </summary>
        private static void CollectOrphanedBoundaryTokens(WorkflowInstance instance)
        {
            foreach (Token t in instance.Tokens.Where(t => t.BoundaryOwnerTokenId != null
                                                           && t.Status != TokenStatus.Consumed))
            {
                Token owner = instance.Tokens.FirstOrDefault(o => o.Id == t.BoundaryOwnerTokenId);
                if (owner is not { Status: TokenStatus.Waiting or TokenStatus.WaitingForTarget })
                {
                    t.Status = TokenStatus.Consumed;
                    t.DueUtc = null;
                }
            }
        }

        /// <summary>
        /// Loescht die Aufgaben-Kennzeichen eines Tokens - danach taucht es in keiner Arbeitsliste mehr auf.
        /// </summary>
        private static void ClearUserTask(Token token)
        {
            token.TaskKey = null;
            token.TaskPermission = null;
            token.AssignedTo = null;
            token.TaskTitle = null;
            token.TaskCreatedUtc = null;
            token.TaskDueUtc = null;
        }

        /// <summary>
        /// Schliesst eine <b>Benutzer-Aufgabe</b> ab: uebernimmt das Ergebnis der Maske ueber die
        /// Ausgabe-Bindungen des Knotens in den Scope des Zweigs und laesst den Zweig weiterlaufen. Das
        /// Vorantreiben selbst uebernimmt - wie bei Signal und Timer - der Aufrufer bzw. der Runner ueber
        /// die zurueckgelieferten Token-Ids.
        /// </summary>
        /// <param name="instanceId">die Instanz</param>
        /// <param name="tokenId">das wartende Token (GENAU diese Aufgabe - nicht alle gleichartigen)</param>
        /// <param name="result">die Ergebniswerte der Maske (Schluessel = deklarierte Ausgabeparameter)</param>
        /// <param name="completedBy">wer die Aufgabe erledigt hat (fuer das Protokoll)</param>
        /// <returns>
        /// das Ergebnis inklusive Unterscheidung "erledigt" / "war schon erledigt" - der zweite Klick auf
        /// eine bereits abgeschlossene Aufgabe darf nicht wie ein Erfolg aussehen.
        /// </returns>
        public UserTaskCompletionResult CompleteUserTask(string instanceId, string tokenId,
            IDictionary<string, object> result = null, string completedBy = null)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            if (tokenId == null)
            {
                throw new ArgumentNullException(nameof(tokenId));
            }

            UserTaskCompletionStatus outcome = UserTaskCompletionStatus.NotFound;
            IReadOnlyList<string> activated = ReactivateAndCommit(instanceId, "CompleteUserTask",
                (fresh, definition) =>
                {
                    // Der Delegat kann bei einem Versionskonflikt erneut laufen - der Ausgang wird deshalb
                    // je Versuch neu bestimmt, nicht akkumuliert.
                    outcome = UserTaskCompletionStatus.NotFound;
                    Token token = fresh.Tokens.FirstOrDefault(t => t.Id == tokenId);
                    if (token == null)
                    {
                        LogEnvironment.LogEvent(
                            $"CompleteUserTask: token '{tokenId}' does not exist in instance '{instanceId}'.",
                            LogSeverity.Warning);
                        return new List<string>();
                    }

                    if (token.Status != TokenStatus.Waiting || token.TaskKey == null)
                    {
                        // Der Normalfall des Rennens: ein anderer war schneller. Kein Fehler, aber der
                        // Aufrufer muss es unterscheiden koennen.
                        outcome = UserTaskCompletionStatus.AlreadyCompleted;
                        return new List<string>();
                    }

                    if (definition.GetNode(token.NodeId) is not UserActivityNode node)
                    {
                        LogEnvironment.LogEvent(
                            $"CompleteUserTask: token '{tokenId}' of instance '{instanceId}' stands on node " +
                            $"'{token.NodeId}', which is not a user task.", LogSeverity.Error);
                        return new List<string>();
                    }

                    ApplyMappedOutputs(fresh, Scope(fresh, token), node.Id, node.Outputs, node.ScopeMode,
                        node.RetainVariables,
                        result ?? new Dictionary<string, object>(StringComparer.Ordinal));
                    fresh.Log("UserTaskCompleted", node.Id,
                        completedBy == null ? node.TaskKey : $"{node.TaskKey} by {completedBy}");
                    ClearUserTask(token);
                    token.Status = TokenStatus.Active;
                    // Eine erledigte Aufgabe kann ebenfalls etwas bewirkt haben (Freigabe erteilt,
                    // Bestellung ausgeloest) - sie laeuft nur nicht ueber MoveAlongSuccessFlow.
                    RecordCompensation(fresh, definition, token, node.Id);
                    if (!MoveAlongSingleOutgoing(fresh, definition, token))
                    {
                        outcome = UserTaskCompletionStatus.Faulted;
                        return new List<string>(); // gefaulted - der Commit persistiert den Fault.
                    }

                    outcome = UserTaskCompletionStatus.Completed;
                    fresh.Status = WorkflowStatus.Running;
                    return new List<string> { token.Id };
                });

            return new UserTaskCompletionResult(outcome, activated);
        }

        /// <summary>
        /// Beschreibt EINE wartende Benutzer-Aufgabe fuer die Oberflaeche: loest die Eingabe-Bindungen
        /// gegen den aktuellen Stand des Zweigs auf und liefert sie zusammen mit Titel, Beschreibung und
        /// der Deklaration der generischen Maske.
        /// </summary>
        /// <param name="instanceId">die Instanz</param>
        /// <param name="tokenId">das wartende Token</param>
        /// <returns>die Beschreibung, oder null, wenn es diese Aufgabe (nicht mehr) gibt</returns>
        public UserTaskDescriptor DescribeUserTask(string instanceId, string tokenId)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            WorkflowInstance instance = store.GetInstance(instanceId);
            if (instance == null)
            {
                LogEnvironment.LogEvent($"DescribeUserTask: instance '{instanceId}' not found.",
                    LogSeverity.Warning);
                return null;
            }

            Token token = instance.Tokens.FirstOrDefault(t => t.Id == tokenId);
            if (token == null || token.Status != TokenStatus.Waiting || token.TaskKey == null)
            {
                LogEnvironment.LogEvent(
                    $"DescribeUserTask: token '{tokenId}' of instance '{instanceId}' is not a waiting user task " +
                    "(gone, already completed or never one).", LogSeverity.Report);
                return null;
            }

            if (LoadDefinition(instance).GetNode(token.NodeId) is not UserActivityNode node)
            {
                LogEnvironment.LogEvent(
                    $"DescribeUserTask: node '{token.NodeId}' of instance '{instanceId}' is not a user task - " +
                    "the definition was changed under a waiting task.", LogSeverity.Error);
                return null;
            }

            Dictionary<string, object> taskScope = Scope(instance, token);
            IDictionary<string, object> payload = ResolveInputs(instance, taskScope, node.Inputs, node.Id);

            // Datenobjekt fuer die Formatierung von Titel/Beschreibung (optional). Ein Fehler hier darf die
            // Aufgabe NICHT unanzeigbar machen - Titel/Beschreibung werden dann eben unformatiert gezeigt -,
            // muss aber ins Log, sonst sucht man den fehlenden Wert an der falschen Stelle.
            object formatData = null;
            if (!string.IsNullOrWhiteSpace(node.FormatData))
            {
                try
                {
                    formatData = evaluator.Evaluate(node.FormatData, taskScope, node.FormatDataMode);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"DescribeUserTask: format data of node '{node.Id}' in instance '{instance.Id}' " +
                        $"could not be evaluated: {ex.OutlineException()}", LogSeverity.Warning);
                }
            }

            return new UserTaskDescriptor
            {
                InstanceId = instance.Id,
                TokenId = token.Id,
                DefinitionId = instance.DefinitionId,
                NodeId = node.Id,
                TaskKey = token.TaskKey,
                ViewKey = node.ViewKey,
                RequiredPermission = token.TaskPermission,
                AssignedTo = token.AssignedTo,
                Title = token.TaskTitle,
                Description = node.Description,
                FormatData = formatData,
                CreatedUtc = token.TaskCreatedUtc,
                DueUtc = token.TaskDueUtc,
                Payload = new Dictionary<string, object>(payload, StringComparer.Ordinal),
                FormFields = node.FormFields ?? new List<UserTaskField>()
            };
        }

        private bool RunActivity(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            AutomatedActivityNode node, IActivityScope activityScope)
        {
            instance.Log("Entered", node.Id, node.Name, HistorySeverity.Verbose);
            Dictionary<string, object> scope = Scope(instance, token);

            // Datenfluss hinein: die Eingabe-Bindungen des Knotens aufloesen. Ein Fehler hier (z.B. ein
            // ungueltiger Ausdruck) hat eine andere Ursache als ein Fehler in der Aktivitaet selbst -
            // deshalb ein eigener Zweig mit eigener, unterscheidbarer Log-/Fault-Meldung.
            IDictionary<string, object> inputs;
            try
            {
                inputs = ResolveInputs(instance, scope, node.Inputs, node.Id);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Input binding of node '{node.Id}' (activity '{node.ActivityRef}') in instance " +
                    $"'{instance.Id}' could not be resolved: {ex.OutlineException()}", LogSeverity.Error);
                Fault(instance, $"Input binding of node '{node.Id}' failed: {ex.Message}", node.Id);
                return false;
            }

            var outputs = new Dictionary<string, object>(StringComparer.Ordinal);

            // On-demand aufgeloest; die Lebensdauer der Aktivitaet (bei Plugins: der geladenen Instanz)
            // gehoert dem Scope und endet mit dem Vortrieb. Bewusst EINMAL - auch fuer eine parallele
            // Iteration: ein Plugin wird unter seinem Namen geteilt, 1000 Elemente wuerden es nicht 1000
            // mal neu laden, und ein nebenlaeufiges Resolve waere fuer den Scope selbst eine Zumutung.
            IWorkflowActivity activity;
            try
            {
                activity = activityScope.Resolve(node.ActivityRef);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Activity '{node.ActivityRef}' of node '{node.Id}' in workflow instance '{instance.Id}' " +
                    $"could not be resolved: {ex.OutlineException()}", LogSeverity.Error);
                return HandleActivityFailure(instance, definition, token, node, ex.Message, applyOutputs: false, null);
            }

            if (node.Iteration != null && node.Iteration.IsConfigured)
            {
                return RunActivityIteration(instance, definition, token, node, node.Iteration, activity, inputs,
                    outputs, scope);
            }

            var context = new WorkflowActivityContext(instance, node, inputs, outputs, scope);
            try
            {
                activity.Execute(context);
            }
            catch (Exception ex)
            {
                // Absturz der Aktivitaet: protokollieren (mit Stacktrace). Hat der Knoten einen
                // Fehler-Ausgang, wird dieser genommen; sonst faultet die Instanz. Die (unzuverlaessigen)
                // Ausgaben eines Absturzes werden NICHT uebernommen.
                LogEnvironment.LogEvent(
                    $"Activity '{node.ActivityRef}' of node '{node.Id}' in workflow instance '{instance.Id}' failed: " +
                    $"{ex.OutlineException()}", LogSeverity.Error);
                return HandleActivityFailure(instance, definition, token, node, ex.Message, applyOutputs: false, null);
            }

            // Kontrollierter Fehler (ctx.Fail): Fehler-Ausgang nehmen; die bewusst gesetzten Ausgaben
            // (Zwischenstand) bleiben erhalten.
            if (context.Failed)
            {
                return HandleActivityFailure(instance, definition, token, node, context.FailureMessage,
                    applyOutputs: true, outputs);
            }

            // Erfolg: Datenfluss heraus, Fehlversuchs-Zaehler zuruecksetzen, ueber den Erfolgs-Ausgang weiter.
            ApplyOutputs(instance, scope, node, outputs);
            ResetAttempts(scope, node.AttemptVariable);
            instance.Log("Completed", node.Id, node.Name, HistorySeverity.Verbose);
            return MoveAlongSuccessFlow(instance, definition, token, node.Id, node.ErrorFlowId);
        }

        /// <summary>
        /// Fuehrt eine Aktivitaet <b>je Element einer Sammlung</b> aus - je nach
        /// <see cref="ActivityIteration.MaxParallel"/> nacheinander oder mehrere gleichzeitig - und fasst
        /// die Ergebnisse zu Listen zusammen (je Ausgabeparameter eine Liste in Eingabe-Reihenfolge).
        /// </summary>
        /// <remarks>
        /// Der Zweig bleibt EIN Zweig: keine zusaetzlichen Tokens, keine Zweig-Sperren, ein Commit am
        /// Ende. Fuer die Persistenz ist der Knoten damit derselbe atomare Schritt wie eine gewoehnliche
        /// Aktivitaet - ein Absturz mittendrin wiederholt ihn ganz.
        /// <para>
        /// Jeder Element-Lauf bekommt eigene Ein-/Ausgaben und eine <b>Kopie</b> des Variablen-Scopes.
        /// Schreibzugriffe auf diese Kopie kann die Engine nicht zusammenfuehren (welcher von 1000
        /// Laeufen haette recht?) - sie verwirft sie, protokolliert aber die erkannten Namen als Warnung,
        /// damit ein so gebautes Skript nicht still das Falsche tut.
        /// </para></remarks>
        private bool RunActivityIteration(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            AutomatedActivityNode node, ActivityIteration iteration, IWorkflowActivity activity,
            IDictionary<string, object> inputs, Dictionary<string, object> outputs,
            Dictionary<string, object> scope)
        {
            if (!TryReadCollection(instance, node, inputs, iteration.ItemsInput, out List<object> items))
            {
                return false;
            }

            // Was fruehere Durchlaeufe schon geschafft haben (Wiederholungs-Schleife). Bewusst hier
            // aufgeloest, VOR der Arbeit: eine falsch gebundene Uebernahme soll auffliegen, bevor 1000
            // Elemente laufen - und nicht erst, wenn das Ergebnis zusammengesetzt wird.
            List<object> carriedOver = null;
            if (!string.IsNullOrWhiteSpace(iteration.CarryOverInput)
                && !TryReadCollection(instance, node, inputs, iteration.CarryOverInput, out carriedOver))
            {
                return false;
            }

            int parallelism = iteration.MaxParallel <= 0 ? Environment.ProcessorCount : iteration.MaxParallel;
            parallelism = Math.Max(1, Math.Min(parallelism, Math.Max(1, items.Count)));
            string itemParameter = iteration.EffectiveItemParameter;

            var itemOutputs = new Dictionary<string, object>[items.Count];
            var itemFailures = new IterationFailure[items.Count];
            var attempted = new bool[items.Count];
            var discardedWrites = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

            instance.Log("Iteration", node.Id,
                $"{items.Count} item(s) of '{iteration.ItemsInput}', parallelism {parallelism}",
                HistorySeverity.Verbose);

            if (items.Count > 0)
            {
                var options = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
                Parallel.For(0, items.Count, options, (i, state) =>
                {
                    // Nach einem Abbruch (ContinueOnError = false) werden noch nicht begonnene Elemente
                    // uebersprungen; die bereits laufenden laufen aus - abwuergen kann die Engine sie nicht.
                    if (state.ShouldExitCurrentIteration)
                    {
                        return;
                    }

                    var singleInputs = new Dictionary<string, object>(inputs, StringComparer.Ordinal)
                    {
                        [itemParameter] = items[i]
                    };
                    if (!string.IsNullOrWhiteSpace(iteration.IndexParameter))
                    {
                        singleInputs[iteration.IndexParameter] = i;
                    }

                    var singleOutputs = new Dictionary<string, object>(StringComparer.Ordinal);
                    Dictionary<string, object> singleScope = CopyScope(scope);
                    var context = new WorkflowActivityContext(instance, node, singleInputs, singleOutputs,
                        singleScope);
                    attempted[i] = true;
                    try
                    {
                        activity.Execute(context);
                        if (context.Failed)
                        {
                            // Kontrolliert abgelehnt - kein Absturz. Der Unterschied bleibt sichtbar:
                            // ExceptionType bleibt null.
                            itemFailures[i] = new IterationFailure
                            {
                                Item = items[i],
                                Index = i,
                                Message = context.FailureMessage ?? "(no message)"
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        // Die Ausnahme wird als DATEN mitgenommen (Typ + ausgeschriebener Stacktrace), nicht
                        // als Objekt: die Fehlerliste landet ueber die Ausgabe-Bindung in einer Variable und
                        // damit im JSON des Commits - ein Exception-Objekt liesse den scheitern.
                        itemFailures[i] = new IterationFailure
                        {
                            Item = items[i],
                            Index = i,
                            Message = ex.Message,
                            ExceptionType = ex.GetType().FullName,
                            ExceptionDetail = ex.OutlineException()
                        };
                        LogEnvironment.LogEvent(
                            $"Activity '{node.ActivityRef}' of node '{node.Id}' in workflow instance " +
                            $"'{instance.Id}' failed on item {i}: {ex.OutlineException()}", LogSeverity.Error);
                    }

                    itemOutputs[i] = singleOutputs;
                    CollectDiscardedWrites(scope, singleScope, discardedWrites);

                    if (itemFailures[i] != null && !iteration.ContinueOnError)
                    {
                        state.Stop();
                    }
                });
            }

            // Je Ausgabeparameter, den irgendein Element gesetzt hat, eine Liste in EINGABE-Reihenfolge
            // (nicht in Fertigstellungs-Reihenfolge) - sonst waere das Ergebnis einer parallelen Iteration
            // von Lauf zu Lauf anders sortiert. Elemente ohne Wert stehen als null drin, damit die
            // Positionen zur Eingabeliste passen.
            foreach (string key in DistinctOutputKeys(itemOutputs))
            {
                var values = new List<object>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    values.Add(itemOutputs[i] != null && itemOutputs[i].TryGetValue(key, out object value)
                        ? value
                        : null);
                }

                outputs[key] = AsTypedResult(values);
            }

            // Drei Sichten auf denselben Durchlauf, jede mit einem eigenen Zweck:
            //   failedItems  - was gescheitert ist (Diagnose, paart mit failedMessages)
            //   pendingItems - was NOCH OFFEN ist (gescheitert PLUS nach einem Abbruch nie versucht).
            //                  Das ist der Eingang einer Wiederholung: wer nur die gescheiterten
            //                  wiederholt, verliert nach einem Abbruch die nie versuchten still.
            //   succeededItems - was FERTIG ist, und zwar als Ergebnis (ItemResultOutput), nicht als
            //                  Eingabe. Die beiden Formen sind absichtlich verschieden: Offenes muss
            //                  wieder in die Sammlung passen, Fertiges ist das Resultat.
            var failures = new List<object>();
            var pendingItems = new List<object>();
            var succeededItems = new List<object>(carriedOver ?? Enumerable.Empty<object>());
            int carriedCount = succeededItems.Count;
            int missingResults = 0;
            int skipped = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (itemFailures[i] != null)
                {
                    failures.Add(itemFailures[i]);
                    pendingItems.Add(items[i]);
                }
                else if (attempted[i])
                {
                    succeededItems.Add(ResultOf(iteration, items, itemOutputs, i, ref missingResults));
                }
                else
                {
                    skipped++;
                    pendingItems.Add(items[i]);
                }
            }

            int succeeded = succeededItems.Count - carriedCount;

            if (missingResults > 0)
            {
                // Der Knoten sagt, das fertige Element stehe unter diesem Namen - dann muss es da auch
                // stehen. Sonst enthaelt die Erfolgsliste Luecken, die spaeter niemand mehr zuordnen kann.
                string detail =
                    $"{missingResults} of {succeeded} successful item(s) did not write the declared result " +
                    $"parameter '{iteration.ItemResultOutput}' - they are null in " +
                    $"'{iteration.SucceededItemsOutput}'.";
                instance.Log("IterationResultMissing", node.Id, detail, HistorySeverity.Warning);
                LogEnvironment.LogEvent(
                    $"Activity '{node.ActivityRef}' of node '{node.Id}' in workflow instance " +
                    $"'{instance.Id}': {detail}", LogSeverity.Warning);
            }

            if (!string.IsNullOrWhiteSpace(iteration.FailedItemsOutput))
            {
                outputs[iteration.FailedItemsOutput] = AsTypedResult(failures);
            }

            if (!string.IsNullOrWhiteSpace(iteration.PendingItemsOutput))
            {
                outputs[iteration.PendingItemsOutput] = AsTypedResult(pendingItems);
            }

            if (!string.IsNullOrWhiteSpace(iteration.SucceededItemsOutput))
            {
                outputs[iteration.SucceededItemsOutput] = AsTypedResult(succeededItems);
            }

            if (!string.IsNullOrWhiteSpace(iteration.SucceededCountOutput))
            {
                // Bewusst NUR dieser Durchlauf, ohne die Uebernahme - sonst waere die Zahl beim zweiten
                // Versuch nicht mehr die Zahl der hier erledigten Elemente.
                outputs[iteration.SucceededCountOutput] = succeeded;
            }

            if (!discardedWrites.IsEmpty)
            {
                string names = string.Join(", ", discardedWrites.Keys.OrderBy(k => k, StringComparer.Ordinal));
                instance.Log("IterationVariablesDiscarded", node.Id, names, HistorySeverity.Warning);
                LogEnvironment.LogEvent(
                    $"Activity '{node.ActivityRef}' of node '{node.Id}' in workflow instance '{instance.Id}' " +
                    $"wrote to the variable scope during an iteration; those writes were discarded because every " +
                    $"item runs on its own copy of the scope: {names}. Return values through the declared " +
                    "outputs instead - they are collected per item.", LogSeverity.Warning);
            }

            if (failures.Count == 0)
            {
                instance.Log("IterationCompleted", node.Id,
                    carriedCount == 0
                        ? $"{succeeded} item(s) succeeded"
                        : $"{succeeded} item(s) succeeded ({carriedCount} carried over from earlier attempts, " +
                          $"{succeededItems.Count} in total)",
                    HistorySeverity.Verbose);
                ApplyOutputs(instance, scope, node, outputs);
                ResetAttempts(scope, node.AttemptVariable);
                instance.Log("Completed", node.Id, node.Name, HistorySeverity.Verbose);
                return MoveAlongSuccessFlow(instance, definition, token, node.Id, node.ErrorFlowId);
            }

            // Die erste Ursache steht in der Meldung - sie ist es, die den Abbruch ausgeloest hat, und sie
            // erspart beim Lesen des Protokolls den Umweg ueber die Fehlerliste.
            var first = (IterationFailure)failures[0];
            string message = skipped == 0
                ? $"{failures.Count} of {items.Count} item(s) failed, first at index {first.Index}: " +
                  $"{Shorten(first.Message)}"
                : $"{failures.Count} of {items.Count} item(s) failed ({skipped} not attempted after the " +
                  $"abort), first at index {first.Index}: {Shorten(first.Message)}";

            // Die Teilergebnisse werden bewusst uebernommen - auch beim Abbruch. Sie sind das, was den
            // Fehler-Ausgang brauchbar macht: welche Elemente durch sind und welche nicht.
            instance.Log("IterationFailed", node.Id, message, HistorySeverity.Warning);
            return HandleActivityFailure(instance, definition, token, node, message, applyOutputs: true, outputs);
        }

        /// <summary>
        /// Liest einen Eingabeparameter als Sammlung. Dieselben Regeln fuer die Iterations-Sammlung und
        /// die Uebernahme frueherer Durchlaeufe - eine Zeichenkette ist auch hier ein Fehler und keine
        /// Folge von Zeichen.
        /// </summary>
        /// <remarks>
        /// Ein <b>fehlender Schluessel</b> ist immer ein Modellierungsfehler, auch bei der Uebernahme:
        /// <c>ResolveInputs</c> legt fuer JEDE Bindung einen Eintrag an - eine gebundene, aber noch nicht
        /// gesetzte Variable steht als <c>null</c> drin. „Nicht da" heisst also nicht „erster Durchlauf",
        /// sondern „gar nicht gebunden". Den Unterschied still zu verwischen waere teuer: eine vertippte
        /// Uebernahme verloere bei jedem Versuch das Ergebnis aller vorigen.
        /// </remarks>
        /// <returns>false, wenn die Instanz dabei gefaultet ist</returns>
        private static bool TryReadCollection(WorkflowInstance instance, AutomatedActivityNode node,
            IDictionary<string, object> inputs, string parameter, out List<object> result)
        {
            result = new List<object>();
            if (!inputs.TryGetValue(parameter, out object bound))
            {
                Fault(instance,
                    $"Iteration of node '{node.Id}' reads a collection from input parameter " +
                    $"'{parameter}', but the node does not bind that parameter.", node.Id);
                return false;
            }

            // Hat der Zweig zwischendurch geparkt (Benutzer-Aufgabe, Timer, Signal), ist die Sammlung
            // durch den JSON-Round-Trip der Variablen gegangen und kaeme als JsonElement zurueck - das
            // ist KEIN IEnumerable und liesse ausgerechnet den zweiten Durchlauf einer
            // Wiederholungs-Schleife auflaufen. Materialize macht daraus wieder Liste/Dictionary/Primitive.
            object raw = WorkflowJson.Materialize(bound);

            switch (raw)
            {
                case null:
                    // Leer ist ein Normalfall: keine Datei zu signieren - oder, bei der Uebernahme, der
                    // erste Durchlauf einer Wiederholungs-Schleife, der noch nichts geschafft hat.
                    return true;
                case string text:
                    Fault(instance,
                        $"Iteration of node '{node.Id}': input parameter '{parameter}' is a string " +
                        $"('{Shorten(text)}'), not a collection. Iterating it character by character is almost " +
                        "certainly not what was meant - bind a list instead.", node.Id);
                    return false;
                case IEnumerable enumerable:
                    result = enumerable.Cast<object>().ToList();
                    return true;
                default:
                    Fault(instance,
                        $"Iteration of node '{node.Id}': input parameter '{parameter}' is of type " +
                        $"'{raw.GetType().FullName}', which is not enumerable.", node.Id);
                    return false;
            }
        }

        /// <summary>
        /// Das <b>fertige</b> Element eines erfolgreichen Durchlaufs: der Wert des deklarierten
        /// Ergebnis-Parameters, ersatzweise (wenn keiner deklariert ist) das unveraenderte
        /// Eingabe-Element. Hat der Lauf den deklarierten Parameter nicht geschrieben, wird das
        /// mitgezaehlt - der Aufrufer meldet es gesammelt statt einmal je Element.
        /// </summary>
        private static object ResultOf(ActivityIteration iteration, List<object> items,
            Dictionary<string, object>[] itemOutputs, int index, ref int missingResults)
        {
            if (string.IsNullOrWhiteSpace(iteration.ItemResultOutput))
            {
                return items[index];
            }

            Dictionary<string, object> single = itemOutputs[index];
            if (single != null && single.TryGetValue(iteration.ItemResultOutput, out object value))
            {
                return value;
            }

            missingResults++;
            return null;
        }

        /// <summary>
        /// Macht aus einer Ergebnis-Liste ein <b>Array</b> - typisiert, wenn alle Elemente denselben
        /// Laufzeittyp haben, sonst <c>object[]</c>. Eine Iterations-Ausgabe ist damit immer ein Array.
        /// </summary>
        /// <remarks>
        /// Das entscheidet, ob das Ergebnis eine Park-Grenze typtreu uebersteht: ein
        /// <c>List&lt;object&gt;</c> traegt keinen brauchbaren Elementtyp, ein <c>SignItem[]</c> schon -
        /// und dessen Kurzname ist ueber <c>WorkflowJson.RegisterVariableType</c> anmeldbar. Ohne diesen
        /// Schritt waere jede Iterations-Ausgabe nach dem naechsten Wartepunkt untypisiert, egal was
        /// angemeldet ist.
        /// <para>
        /// Immer ein Array - nicht mal Array, mal Liste: die Form der Ausgabe soll nicht davon abhaengen,
        /// wie einheitlich die Daten zufaellig ausgefallen sind. Nullwerte (dort ist nichts fertig
        /// geworden) zwingen bei Werttypen auf <c>object[]</c>, weil ein <c>int[]</c> kein null aufnimmt.
        /// </para></remarks>
        private static Array AsTypedResult(List<object> values)
        {
            Type common = null;
            bool hasNull = false;
            bool mixed = false;
            foreach (object value in values)
            {
                if (value == null)
                {
                    hasNull = true;
                    continue;
                }

                Type type = value.GetType();
                if (common == null)
                {
                    common = type;
                }
                else if (common != type)
                {
                    mixed = true;
                    break;
                }
            }

            Type elementType = mixed || common == null || (hasNull && common.IsValueType)
                ? typeof(object)
                : common;

            var array = Array.CreateInstance(elementType, values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                array.SetValue(values[i], i);
            }

            return array;
        }

        /// <summary>
        /// Die Ausgabeparameter, die irgendein Element-Lauf gesetzt hat - in der Reihenfolge ihres ersten
        /// Auftretens, damit das Ergebnis reproduzierbar bleibt.
        /// </summary>
        private static IEnumerable<string> DistinctOutputKeys(Dictionary<string, object>[] itemOutputs)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var keys = new List<string>();
            foreach (Dictionary<string, object> single in itemOutputs)
            {
                if (single == null)
                {
                    continue;
                }

                foreach (string key in single.Keys)
                {
                    if (seen.Add(key))
                    {
                        keys.Add(key);
                    }
                }
            }

            return keys;
        }

        /// <summary>
        /// Sammelt die Variablen-Namen, die ein Element-Lauf auf seiner Scope-Kopie geaendert oder neu
        /// angelegt hat (und die deshalb verworfen werden). Best effort: eine in sich veraenderte
        /// Referenz (z.B. eine Liste, an die angehaengt wurde) sieht das nicht.
        /// </summary>
        private static void CollectDiscardedWrites(Dictionary<string, object> baseScope,
            Dictionary<string, object> itemScope, ConcurrentDictionary<string, byte> sink)
        {
            if (baseScope == null || itemScope == null)
            {
                return;
            }

            // Nur Lesezugriffe auf baseScope - waehrend der Iteration schreibt die Engine dort nicht, und
            // gleichzeitiges Lesen eines Dictionary ist zulaessig.
            foreach (KeyValuePair<string, object> pair in itemScope)
            {
                if (!baseScope.TryGetValue(pair.Key, out object original) || !Equals(original, pair.Value))
                {
                    sink.TryAdd(pair.Key, 0);
                }
            }
        }

        /// <summary>Kuerzt einen Text fuer eine Fehlermeldung/ein Protokoll-Detail.</summary>
        private static string Shorten(string text, int max = 120)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text;
            }

            return text.Substring(0, max) + "...";
        }

        /// <summary>
        /// Behandelt einen Aktivitaets-Fehler: ohne Fehler-Ausgang faultet die Instanz (wie bisher). Mit
        /// Fehler-Ausgang wird - optional der Zwischenstand uebernommen, dann - der Fehlerkontext
        /// bereitgestellt (<see cref="AutomatedActivityNode.ErrorVariable"/> = Meldung,
        /// <see cref="AutomatedActivityNode.AttemptVariable"/> += 1) und der Token ueber die Fehler-Kante
        /// bewegt.
        /// </summary>
        private bool HandleActivityFailure(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            AutomatedActivityNode node, string message, bool applyOutputs, IDictionary<string, object> outputs)
        {
            if (string.IsNullOrEmpty(node.ErrorFlowId))
            {
                Fault(instance, $"Activity '{node.ActivityRef}' failed: {message}", node.Id);
                return false;
            }

            SequenceFlow errorFlow = definition.OutgoingFlows(node.Id).FirstOrDefault(f => f.Id == node.ErrorFlowId);
            if (errorFlow == null)
            {
                Fault(instance, $"Error flow '{node.ErrorFlowId}' of node '{node.Id}' does not exist.", node.Id);
                return false;
            }

            Dictionary<string, object> scope = Scope(instance, token);
            if (applyOutputs && outputs != null)
            {
                ApplyOutputs(instance, scope, node, outputs);
            }

            if (!string.IsNullOrEmpty(node.ErrorVariable))
            {
                scope[node.ErrorVariable] = message;
            }

            int attempts = 0;
            if (!string.IsNullOrEmpty(node.AttemptVariable))
            {
                scope.TryGetValue(node.AttemptVariable, out object current);
                attempts = (current is int i ? i : 0) + 1;
                scope[node.AttemptVariable] = attempts;
            }

            instance.Log("ActivityError", node.Id, message, HistorySeverity.Warning);
            return MoveToken(instance, token, errorFlow);
        }

        /// <summary>
        /// Bewegt einen Token nach Erfolg weiter: ohne Fehler-Ausgang ueber die einzige ausgehende Kante;
        /// mit Fehler-Ausgang ueber die einzige NICHT-Fehler-Kante (die Erfolgs-Kante). Fuer Aktivitaets- und
        /// Subworkflow-Knoten gleichermassen.
        /// </summary>
        private bool MoveAlongSuccessFlow(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            string nodeId, string errorFlowId)
        {
            // Der gemeinsame Erfolgs-Punkt von Aktivitaet, Subworkflow, Abschnitt und Iteration - und
            // damit die eine Stelle, an der ein Schritt zur Ruecknahme vorgemerkt wird. Ueber den
            // FEHLER-Ausgang laeuft es bewusst nicht: was gescheitert ist, hat nichts hinterlassen, das
            // zurueckzunehmen waere.
            RecordCompensation(instance, definition, token, nodeId);

            if (string.IsNullOrEmpty(errorFlowId))
            {
                return MoveAlongSingleOutgoing(instance, definition, token);
            }

            var success = definition.OutgoingFlows(nodeId).Where(f => f.Id != errorFlowId).ToList();
            if (success.Count != 1)
            {
                Fault(instance,
                    $"Node '{nodeId}' with an error flow must have exactly one success flow, but has {success.Count}.",
                    nodeId);
                return false;
            }

            return MoveToken(instance, token, success[0]);
        }

        /// <summary>Setzt den Fehlversuchs-Zaehler bei Erfolg zurueck (falls die Variable konfiguriert ist).</summary>
        private static void ResetAttempts(Dictionary<string, object> scope, string attemptVariable)
        {
            if (!string.IsNullOrEmpty(attemptVariable))
            {
                scope[attemptVariable] = 0;
            }
        }

        /// <summary>
        /// Der Variablen-Scope, in dem ein Token arbeitet: sein <b>Zweig-Scope</b>
        /// (<see cref="Token.Variables"/>), wenn es innerhalb einer parallelen Region laeuft, sonst der
        /// Instanz-Scope. EINE Stelle - Lesen, Schreiben, Bedingungen und Ausdruecke sehen damit
        /// zwangslaeufig denselben Stack.
        /// </summary>
        private static Dictionary<string, object> Scope(WorkflowInstance instance, Token token)
            => token?.Variables ?? instance.Variables;

        /// <summary>Eine flache Kopie eines Scopes (der Zweig-Scope eines neuen Strangs), oder null.</summary>
        private static Dictionary<string, object> CopyScope(Dictionary<string, object> scope)
            => scope == null ? null : new Dictionary<string, object>(scope, StringComparer.Ordinal);

        /// <summary>Uebernimmt die (optionalen) Payload-Variablen eines Signals in den angegebenen Scope.</summary>
        private static void ApplyPayload(Dictionary<string, object> scope,
            IDictionary<string, object> payloadVariables)
        {
            if (payloadVariables == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in payloadVariables)
            {
                scope[pair.Key] = pair.Value;
            }
        }

        /// <summary>
        /// Loest die Eingabe-Bindungen eines Aktivitaets-Knotens gegen den aktuellen Instanzzustand
        /// auf. Eine fehlende Variable (bei <see cref="ParameterBindingKind.Variable"/>) ist ein
        /// definierter Normalfall (der Wert ist dann null) - kein Fehler, aber protokolliert, damit er
        /// nachvollziehbar bleibt. Ein Ausdrucksfehler wird an den Aufrufer geworfen (der faultet).
        /// </summary>
        private IDictionary<string, object> ResolveInputs(WorkflowInstance instance,
            Dictionary<string, object> scope, List<ActivityInputBinding> inputs, string nodeId)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (inputs == null)
            {
                return result;
            }

            foreach (ActivityInputBinding binding in inputs)
            {
                if (binding == null || string.IsNullOrEmpty(binding.Parameter))
                {
                    continue;
                }

                switch (binding.Kind)
                {
                    case ParameterBindingKind.Literal:
                        result[binding.Parameter] = binding.Literal;
                        break;

                    case ParameterBindingKind.Variable:
                        if (!string.IsNullOrEmpty(binding.Source)
                            && scope.TryGetValue(binding.Source, out object value))
                        {
                            result[binding.Parameter] = value;
                        }
                        else
                        {
                            result[binding.Parameter] = null;
                            LogEnvironment.LogEvent(
                                $"Input '{binding.Parameter}' of node '{nodeId}' in instance '{instance.Id}' " +
                                $"is bound to variable '{binding.Source}', which is not set - resolved to null.",
                                LogSeverity.Report);
                        }

                        break;

                    case ParameterBindingKind.Expression:
                        result[binding.Parameter] = evaluator.Evaluate(binding.Source, scope, binding.SourceMode);
                        break;

                    default:
                        LogEnvironment.LogEvent(
                            $"Input '{binding.Parameter}' of node '{nodeId}' uses an unsupported binding " +
                            $"kind '{binding.Kind}' - resolved to null.", LogSeverity.Warning);
                        result[binding.Parameter] = null;
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Bildet die deklarierten Ausgaben eines Aktivitaets-Knotens auf Instanz-Variablen ab. Der
        /// Wert wird auch dann geschrieben, wenn die Aktivitaet den Ausgabeparameter nicht gesetzt hat
        /// (dann null) - das ist ein bewusstes, beobachtbares Ergebnis.
        /// </summary>
        /// <remarks>
        /// Bei <see cref="ActivityScopeMode.Replace"/> (Konsolidierung) wird der Scope frisch aufgebaut:
        /// er besteht danach genau aus der Erhaltungs-Whitelist (<see cref="AutomatedActivityNode.RetainVariables"/>,
        /// soweit vorhanden) und den Ausgaben - alle uebrigen Variablen werden abgeraeumt.
        /// </remarks>
        private static void ApplyOutputs(WorkflowInstance instance, Dictionary<string, object> scope,
            AutomatedActivityNode node, IDictionary<string, object> outputs)
        {
            ApplyMappedOutputs(instance, scope, node.Id, node.Outputs, node.ScopeMode, node.RetainVariables,
                outputs);
        }

        /// <summary>
        /// Der gemeinsame Kern von <see cref="ApplyOutputs"/> (Aktivitaet) und
        /// <see cref="ApplyCallOutputs"/> (Subworkflow): bildet <paramref name="source"/> ueber die
        /// Bindungen auf Ziel-Variablen ab und bringt sie je nach <paramref name="mode"/> additiv oder
        /// konsolidierend in den Scope ein. Bewusst EINE Implementierung - die beiden Wege duerfen
        /// nicht auseinanderlaufen.
        /// </summary>
        /// <remarks>
        /// <paramref name="scope"/> ist der Scope, in dem der Zweig arbeitet: der Instanz-Scope oder -
        /// innerhalb einer parallelen Region - der Zweig-Scope des Tokens (<see cref="Token.Variables"/>).
        /// Auch die Konsolidierung (<see cref="ActivityScopeMode.Replace"/>) wirkt genau dort und damit
        /// zweig-lokal.
        /// </remarks>
        private static void ApplyMappedOutputs(WorkflowInstance instance, Dictionary<string, object> scope,
            string nodeId, IEnumerable<ActivityOutputBinding> bindings, ActivityScopeMode mode,
            IEnumerable<string> retainVariables, IDictionary<string, object> source)
        {
            // Zuerst die Ziel-Variablen aus den Output-Bindungen bestimmen (unabhaengig vom Scope-Modus).
            var mapped = new Dictionary<string, object>(StringComparer.Ordinal);
            if (bindings != null)
            {
                foreach (ActivityOutputBinding binding in bindings)
                {
                    if (binding == null || string.IsNullOrEmpty(binding.Parameter)
                        || string.IsNullOrEmpty(binding.Variable))
                    {
                        continue;
                    }

                    source.TryGetValue(binding.Parameter, out object value);
                    mapped[binding.Variable] = value;
                }
            }

            if (mode == ActivityScopeMode.Replace)
            {
                // Konsolidierung: neuen Scope aus Retain-Whitelist + Ausgaben bauen, Rest verwerfen.
                var fresh = new Dictionary<string, object>();
                if (retainVariables != null)
                {
                    foreach (string keep in retainVariables)
                    {
                        if (!string.IsNullOrEmpty(keep) && scope.TryGetValue(keep, out object v))
                        {
                            fresh[keep] = v;
                        }
                    }
                }

                foreach (KeyValuePair<string, object> pair in mapped)
                {
                    fresh[pair.Key] = pair.Value; // Ausgaben gewinnen bei Kollision mit der Whitelist.
                }

                scope.Clear();
                foreach (KeyValuePair<string, object> pair in fresh)
                {
                    scope[pair.Key] = pair.Value;
                }

                instance.Log("Consolidated", nodeId, $"scope reduced to {fresh.Count} variable(s)");
            }
            else
            {
                foreach (KeyValuePair<string, object> pair in mapped)
                {
                    scope[pair.Key] = pair.Value;
                }
            }
        }

        /// <summary>
        /// Wendet die deklarierte <b>Signatur</b> der Definition (<see cref="StartNode.Inputs"/>) auf den
        /// frischen Variablen-Stack an: die Bindungen werden gegen die bereits uebergebenen Startwerte
        /// aufgeloest und danach - je nach <see cref="StartNode.ScopeMode"/> - additiv gemergt oder als
        /// strikte Signatur eingesetzt (der Stack besteht dann genau aus den deklarierten Parametern plus
        /// <see cref="StartNode.RetainVariables"/>).
        /// </summary>
        /// <remarks>
        /// Ohne deklarierte Parameter passiert nichts - bestehende Definitionen verhalten sich unveraendert.
        /// Ein Ausdrucksfehler wird an den Aufrufer geworfen (der entscheidet, ob das ein nicht
        /// entstehender Start oder ein Fault des Aufrufers ist).
        /// </remarks>
        private void ApplyStartInputs(WorkflowInstance instance, WorkflowDefinition definition,
            List<StartNode> startNodes)
        {
            StartNode signature = SelectDeclaring(
                startNodes?.Where(s => s?.Inputs is { Count: > 0 }).ToList(),
                "start parameters", $"Definition '{definition.Id}' v{definition.Version}");
            if (signature == null)
            {
                return;
            }

            // Der Start liegt immer im Instanz-Scope (vor jedem Split).
            IDictionary<string, object> resolved =
                ResolveInputs(instance, instance.Variables, signature.Inputs, signature.Id);

            // Die aufgeloesten Parameter tragen bereits ihren Ziel-Namen - deshalb Identitaets-Bindungen.
            // Das nutzt bewusst denselben Kern wie Aktivitaet und Subworkflow-Aufruf: Extend/Replace duerfen
            // an drei Stellen nicht dreimal verschieden bedeuten.
            ApplyMappedOutputs(instance, instance.Variables, signature.Id,
                resolved.Keys.Select(k => new ActivityOutputBinding { Parameter = k, Variable = k }).ToList(),
                signature.ScopeMode, signature.RetainVariables, resolved);

            instance.Log("Parameters", signature.Id,
                $"{resolved.Count} start parameter(s), {signature.ScopeMode.ToString().ToLowerInvariant()}",
                HistorySeverity.Verbose);
        }

        /// <summary>
        /// Setzt den Variablen-Stack beim Abschluss auf das deklarierte <b>Ergebnis</b> des erreichten
        /// End-Knotens (<see cref="EndNode.Outputs"/>) zurueck. Ohne deklariertes Ergebnis passiert nichts -
        /// dann bleibt wie bisher der komplette Stack das Ergebnis.
        /// </summary>
        /// <remarks>
        /// Bewusst beim UEBERGANG auf <see cref="WorkflowStatus.Completed"/> und nicht beim Verbrauch des
        /// Tokens: bei parallelen Zweigen wuerde sonst der erste ankommende Zweig den Stack abraeumen, den
        /// die uebrigen noch brauchen.
        /// </remarks>
        private static void ApplyEndOutputs(WorkflowInstance instance, WorkflowDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            // Nur die tatsaechlich erreichten Ergebnis-Knoten zaehlen (ein nie durchlaufener
            // Alternativausgang darf das Ergebnis nicht bestimmen). Ueber IResultNode zaehlt der
            // Terminate-Knoten gleichberechtigt mit - sonst endete ein Abbruch ohne jede Aussage darueber,
            // warum.
            List<IResultNode> reached = instance.Tokens
                .Where(t => t.Status == TokenStatus.Consumed)
                .Select(t => definition.GetNode(t.NodeId))
                // Ein Ende INNERHALB eines Subprozesses beendet den Abschnitt, nicht den Workflow - sein
                // Ergebnis gehoert dem Abschnitt und darf nicht das der Instanz bestimmen.
                .Where(n => n != null && n.ParentNodeId == null)
                .Select(n => n as IResultNode)
                .Where(e => e?.Outputs is { Count: > 0 })
                .GroupBy(e => e.Id, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            IResultNode end = SelectDeclaring(reached, "workflow results", $"Instance '{instance.Id}'");
            if (end == null)
            {
                return;
            }

            // Quelle ist der Stack selbst - erst kopieren, damit das Abraeumen in ApplyMappedOutputs nicht
            // die Quelle mit abraeumt, aus der es gerade liest.
            var source = new Dictionary<string, object>(instance.Variables, StringComparer.Ordinal);
            ApplyMappedOutputs(instance, instance.Variables, end.Id, end.Outputs, ActivityScopeMode.Replace,
                end.RetainVariables, source);
        }

        /// <summary>
        /// Meldet den impliziten Parallelstart einer Definition mit mehreren Start-Knoten. Die Engine startet
        /// sie weiterhin alle (Altdefinitionen bleiben lauffaehig), aber es ist ein Modellierungsfehler: der
        /// Validator meldet ihn seit der Ein-Start-Regel als Fehler, und die Signatur der Definition waere
        /// mehrdeutig. Gewollter Parallelstart gehoert hinter EINEN Start als AND-Split.
        /// </summary>
        private static void WarnOnMultipleStarts(WorkflowDefinition definition, List<StartNode> startNodes)
        {
            if (startNodes.Count <= 1)
            {
                return;
            }

            LogEnvironment.LogEvent(
                $"Definition '{definition.Id}' v{definition.Version} has {startNodes.Count} start nodes " +
                $"({string.Join(", ", startNodes.Select(s => $"'{s.Id}'"))}) - each gets a token (implicit " +
                "parallel start). Use exactly one start node followed by an AND split instead.",
                LogSeverity.Warning);
        }

        /// <summary>
        /// Waehlt aus den Knoten, die eine Deklaration tragen, den einen gueltigen aus. Genau einer ist der
        /// Normalfall (der Validator meldet mehrere als Fehler); gibt es doch mehrere, gewinnt deterministisch
        /// der mit der kleinsten Id - mit Log-Zeile, damit die Mehrdeutigkeit nicht still bleibt.
        /// </summary>
        private static T SelectDeclaring<T>(List<T> declaring, string what, string owner)
            where T : class, INodeIdentity
        {
            if (declaring == null || declaring.Count == 0)
            {
                return null;
            }

            if (declaring.Count == 1)
            {
                return declaring[0];
            }

            T chosen = declaring.OrderBy(n => n.Id, StringComparer.Ordinal).First();
            LogEnvironment.LogEvent(
                $"{owner} declares {what} on {declaring.Count} nodes " +
                $"({string.Join(", ", declaring.Select(n => $"'{n.Id}'"))}) - using '{chosen.Id}'. " +
                "Declare them on exactly one node.", LogSeverity.Warning);
            return chosen;
        }

        /// <summary>
        /// Verarbeitet einen <see cref="CallWorkflowNode"/>: startet den Subworkflow als eigene Instanz
        /// (idempotent - deterministische Id je aufrufendem Token) und parkt den Eltern-Zweig, bis der
        /// Subworkflow endet. Ist der Subworkflow bereits fertig (z.B. nach einem Wiederanlauf), wird sein
        /// Ergebnis sofort uebernommen, ohne zu parken (Selbstheilung).
        /// </summary>
        private bool ProcessCallWorkflow(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            CallWorkflowNode node)
        {
            if (string.IsNullOrEmpty(node.SubDefinitionId))
            {
                Fault(instance, $"Call node '{node.Id}' has no sub-workflow definition id.", node.Id);
                return false;
            }

            // Der Versuchs-Zaehler geht in die (deterministische) Kind-Id ein: so bekommt jeder Wiederholungs-
            // Lauf eine EIGENE Kind-Instanz (echte Retry-Schleife ueber die Fehlerkante), waehrend innerhalb
            // EINES Versuchs die Id stabil bleibt (idempotent bei Wiederanlauf).
            Dictionary<string, object> scope = Scope(instance, token);
            int attempt = CurrentAttempt(scope, node.AttemptVariable);
            string childId = ChildInstanceId(instance.Id, token.Id, attempt);
            WorkflowInstance child = store.GetInstance(childId);

            if (child != null && child.Status == WorkflowStatus.Completed)
            {
                return CompleteCall(instance, definition, token, node, child);
            }

            if (child != null && (child.Status == WorkflowStatus.Faulted || child.Status == WorkflowStatus.Cancelled))
            {
                return HandleSubworkflowFailure(instance, definition, token, node, child);
            }

            if (child == null)
            {
                IDictionary<string, object> childVars;
                try
                {
                    childVars = ResolveInputs(instance, scope, node.Inputs, node.Id);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Input binding of call node '{node.Id}' in instance '{instance.Id}' could not be " +
                        $"resolved: {ex.OutlineException()}", LogSeverity.Error);
                    Fault(instance, $"Input binding of call node '{node.Id}' failed: {ex.Message}", node.Id);
                    return false;
                }

                if (instance.CallDepth + 1 > MaxCallDepth)
                {
                    Fault(instance,
                        $"Sub-workflow nesting exceeded {MaxCallDepth} levels at node '{node.Id}' - possible recursion.",
                        node.Id);
                    return false;
                }

                WorkflowDefinition subDef = store.GetDefinition(node.SubDefinitionId, node.SubDefinitionVersion);
                if (subDef == null)
                {
                    Fault(instance,
                        $"Call node '{node.Id}' references unknown sub-workflow '{node.SubDefinitionId}'.", node.Id);
                    return false;
                }

                var startNodes = subDef.StartNodes().ToList();
                if (startNodes.Count == 0)
                {
                    Fault(instance, $"Sub-workflow '{subDef.Id}' has no start node.", node.Id);
                    return false;
                }

                WarnOnMultipleStarts(subDef, startNodes);

                var now = DateTime.UtcNow;
                child = new WorkflowInstance
                {
                    Id = childId,
                    // Wie beim Start: der Verweis ist die technische Kennung der aufgeloesten Zeile.
                    DefinitionKey = subDef.Key,
                    DefinitionId = subDef.Id,
                    DefinitionVersion = subDef.Version,
                    TenantId = instance.TenantId,
                    ParentInstanceId = instance.Id,
                    ParentTokenId = token.Id,
                    RootInstanceId = instance.EffectiveRootInstanceId,
                    CallDepth = instance.CallDepth + 1,
                    Status = WorkflowStatus.Running,
                    // Der Subworkflow erbt die Dringlichkeit seines Aufrufers: er ist ein Stueck von
                    // dessen Arbeit, und der Aufrufer wartet auf ihn. Eine eigene Vorgabe der
                    // Sub-Definition wuerde genau das verkehren - ein dringender Prozess wuerde an
                    // seinem eigenen Unterschritt haengen bleiben.
                    Priority = instance.Priority,
                    HistoryFilter = FilterFor(subDef),
                    CreatedUtc = now,
                    UpdatedUtc = now
                };
                foreach (KeyValuePair<string, object> pair in childVars)
                {
                    child.Variables[pair.Key] = pair.Value;
                }

                // Die Signatur des Subworkflows gilt auch hier: dieser Pfad baut die Kind-Instanz bewusst
                // inline (eigene Id, Eltern-Verknuepfung, Tiefe) statt ueber CreateInstance - die
                // Start-Parameter duerfen deshalb NICHT nur dort haengen, sonst gaelte die Signatur je nach
                // Starter unterschiedlich.
                try
                {
                    ApplyStartInputs(child, subDef, startNodes);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Start parameters of sub-workflow '{subDef.Id}' called from node '{node.Id}' in " +
                        $"instance '{instance.Id}' could not be resolved: {ex.OutlineException()}",
                        LogSeverity.Error);
                    Fault(instance,
                        $"Start parameters of sub-workflow '{subDef.Id}' failed: {ex.Message}", node.Id);
                    return false;
                }

                foreach (StartNode start in startNodes)
                {
                    child.Tokens.Add(new Token { NodeId = start.Id, Status = TokenStatus.Active });
                }

                child.Log("Started", detail: $"sub-workflow of {instance.Id}");
                // Bewusster (idempotenter) Seiteneffekt waehrend des In-Memory-Vortriebs: die Kind-Instanz
                // wird sofort persistiert, damit der Runner sie aufnimmt. Bei einem Wiederanlauf verhindert
                // der GetInstance-Guard oben ein zweites Anlegen.
                store.SaveInstance(child);
            }

            token.Status = TokenStatus.Waiting;
            token.WaitingForChildInstanceId = childId;
            instance.Log("CallWorkflow", node.Id, node.SubDefinitionId);
            return true;
        }

        /// <summary>
        /// Uebernimmt das Ergebnis eines fertigen Subworkflows in den Elternprozess: Ausgaben abbilden,
        /// Eltern-Token reaktivieren und ueber die einzige ausgehende Kante weiterbewegen.
        /// </summary>
        private bool CompleteCall(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            CallWorkflowNode node, WorkflowInstance child)
        {
            Dictionary<string, object> scope = Scope(instance, token);
            ApplyCallOutputs(instance, scope, node, child.Variables);
            ResetAttempts(scope, node.AttemptVariable);
            token.WaitingForChildInstanceId = null;
            token.Status = TokenStatus.Active;
            instance.Log("SubworkflowCompleted", node.Id, child.DefinitionId);
            return MoveAlongSuccessFlow(instance, definition, token, node.Id, node.ErrorFlowId);
        }

        /// <summary>
        /// Behandelt einen gescheiterten Subworkflow (Faulted/Cancelled): ohne Fehler-Ausgang faultet der
        /// aufrufende Knoten (Standard-Propagation). Mit Fehler-Ausgang wird der Fehlerkontext bereitgestellt
        /// (<see cref="CallWorkflowNode.ErrorVariable"/> = Meldung, <see cref="CallWorkflowNode.AttemptVariable"/>
        /// += 1) und der Token ueber die Fehler-Kante bewegt - fuehrt sie zum selben Knoten zurueck, wird der
        /// Subworkflow (mit erhoehtem Zaehler = neuer Kind-Id) erneut versucht.
        /// </summary>
        private bool HandleSubworkflowFailure(WorkflowInstance parent, WorkflowDefinition definition, Token token,
            CallWorkflowNode node, WorkflowInstance child)
        {
            string message = $"Sub-workflow '{child.DefinitionId}' ({child.Id}) " +
                             $"{child.Status.ToString().ToLowerInvariant()}: {child.FaultMessage}";
            if (string.IsNullOrEmpty(node.ErrorFlowId))
            {
                Fault(parent, message, node.Id);
                return false;
            }

            SequenceFlow errorFlow = definition.OutgoingFlows(node.Id).FirstOrDefault(f => f.Id == node.ErrorFlowId);
            if (errorFlow == null)
            {
                Fault(parent, $"Error flow '{node.ErrorFlowId}' of node '{node.Id}' does not exist.", node.Id);
                return false;
            }

            Dictionary<string, object> scope = Scope(parent, token);
            if (!string.IsNullOrEmpty(node.ErrorVariable))
            {
                scope[node.ErrorVariable] = child.FaultMessage ?? message;
            }

            if (!string.IsNullOrEmpty(node.AttemptVariable))
            {
                scope.TryGetValue(node.AttemptVariable, out object current);
                scope[node.AttemptVariable] = (current is int i ? i : 0) + 1;
            }

            token.WaitingForChildInstanceId = null;
            token.Status = TokenStatus.Active;
            parent.Log("SubworkflowError", node.Id, message, HistorySeverity.Warning);
            return MoveToken(parent, token, errorFlow);
        }

        /// <summary>Liest den aktuellen Fehlversuchs-Zaehler (0, wenn nicht gesetzt oder nicht konfiguriert).</summary>
        private static int CurrentAttempt(Dictionary<string, object> scope, string attemptVariable)
        {
            if (!string.IsNullOrEmpty(attemptVariable)
                && scope.TryGetValue(attemptVariable, out object v) && v is int i)
            {
                return i;
            }

            return 0;
        }

        /// <summary>
        /// Bildet die End-Variablen des Subworkflows ueber die Output-Bindungen auf Eltern-Variablen ab.
        /// Bei <see cref="ActivityScopeMode.Replace"/> wirkt der Aufruf als Konsolidierung: der
        /// Eltern-Scope besteht danach genau aus diesen Ausgaben plus
        /// <see cref="CallWorkflowNode.RetainVariables"/>.
        /// </summary>
        private static void ApplyCallOutputs(WorkflowInstance parent, Dictionary<string, object> scope,
            CallWorkflowNode node, IDictionary<string, object> childVariables)
        {
            ApplyMappedOutputs(parent, scope, node.Id, node.Outputs, node.ScopeMode, node.RetainVariables,
                childVariables);
        }

        /// <summary>
        /// Liefert das Ergebnis eines beendeten Subworkflows an den wartenden Eltern-Zweig: bei Erfolg
        /// werden die Ausgaben uebernommen und der Zweig laeuft weiter; bei Fault/Abbruch faultet der
        /// aufrufende Knoten (Standard-Fehlerpropagation). Optimistischer Commit auf der Eltern-Instanz mit
        /// Retry. No-op, wenn der Eltern-Zweig nicht (mehr) auf dieses Kind wartet (schon geliefert/entfallen).
        /// </summary>
        public void DeliverChildCompletion(string childInstanceId)
        {
            if (childInstanceId == null)
            {
                throw new ArgumentNullException(nameof(childInstanceId));
            }

            WorkflowInstance child = store.GetInstance(childInstanceId);
            if (child == null || string.IsNullOrEmpty(child.ParentInstanceId))
            {
                return;
            }

            const int maxRetries = 100;
            for (int attempt = 0; ; attempt++)
            {
                WorkflowInstance parent = store.GetInstance(child.ParentInstanceId);
                if (parent == null)
                {
                    LogEnvironment.LogEvent(
                        $"DeliverChildCompletion: parent '{child.ParentInstanceId}' of sub-workflow " +
                        $"'{childInstanceId}' not found - skipped.", LogSeverity.Warning);
                    return;
                }

                int baseVersion = parent.Version;
                Token token = parent.Tokens.FirstOrDefault(t =>
                    t.Status == TokenStatus.Waiting && t.WaitingForChildInstanceId == childInstanceId);
                if (token == null)
                {
                    return; // schon geliefert oder Eltern-Zweig entfallen - nichts zu tun.
                }

                WorkflowDefinition parentDef = LoadDefinition(parent);
                if (parentDef.GetNode(token.NodeId) is not CallWorkflowNode node)
                {
                    Fault(parent,
                        $"Waiting call node '{token.NodeId}' for sub-workflow '{childInstanceId}' is missing " +
                        "or is no longer a call node.", token.NodeId);
                }
                else if (child.Status == WorkflowStatus.Completed)
                {
                    CompleteCall(parent, parentDef, token, node, child);
                }
                else
                {
                    // Faulted/Cancelled: Fehler-Ausgang nehmen (oder faulten, wenn keiner konfiguriert ist).
                    HandleSubworkflowFailure(parent, parentDef, token, node, child);
                }

                if (parent.Status != WorkflowStatus.Faulted && parent.Status != WorkflowStatus.Cancelled)
                {
                    UpdateTerminalStatus(parent, parentDef);
                }

                if (store.TryCommitInstance(parent, baseVersion))
                {
                    return;
                }

                if (attempt >= maxRetries)
                {
                    LogEnvironment.LogEvent(
                        $"DeliverChildCompletion: giving up after {maxRetries} version conflicts on parent " +
                        $"'{child.ParentInstanceId}' of sub-workflow '{childInstanceId}'.", LogSeverity.Error);
                    return;
                }
            }
        }

        /// <summary>
        /// Ist die Instanz gerade fertig geworden (Completed/Faulted) UND ein Subworkflow, liefert sie ihr
        /// Ergebnis an den wartenden Elternprozess. Sonst ohne Wirkung.
        /// </summary>
        private void NotifyParentIfFinished(WorkflowInstance instance)
        {
            if ((instance.Status == WorkflowStatus.Completed || instance.Status == WorkflowStatus.Faulted)
                && !string.IsNullOrEmpty(instance.ParentInstanceId))
            {
                DeliverChildCompletion(instance.Id);
            }
        }

        /// <summary>
        /// Deterministische Id der Kind-Instanz je aufrufendem (Eltern-)Token und Versuch - macht das
        /// Anlegen idempotent (stabile Id je Versuch) und ermoeglicht zugleich echte Wiederholung (neuer
        /// Versuch = neue Id).
        /// </summary>
        private static string ChildInstanceId(string parentInstanceId, string tokenId, int attempt)
            => $"{parentInstanceId}:{tokenId}:{attempt}";

        private bool RouteExclusive(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            ExclusiveGatewayNode gateway)
        {
            instance.Log("Entered", gateway.Id, gateway.Name, HistorySeverity.Verbose);
            IReadOnlyList<SequenceFlow> outgoing = definition.OutgoingFlows(gateway.Id);

            foreach (SequenceFlow flow in outgoing)
            {
                if (string.IsNullOrWhiteSpace(flow.Condition))
                {
                    continue;
                }

                bool matched;
                try
                {
                    matched = evaluator.EvaluateCondition(flow.Condition, Scope(instance, token), flow.ConditionMode);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Condition of flow '{flow.Id}' at gateway '{gateway.Id}' in instance '{instance.Id}' " +
                        $"could not be evaluated: {ex.OutlineException()}", LogSeverity.Error);
                    Fault(instance, $"Condition of flow '{flow.Id}' failed: {ex.Message}", gateway.Id);
                    return false;
                }

                if (matched)
                {
                    return MoveToken(instance, token, flow);
                }
            }

            if (gateway.DefaultFlowId != null)
            {
                SequenceFlow defaultFlow = outgoing.FirstOrDefault(f => f.Id == gateway.DefaultFlowId);
                if (defaultFlow == null)
                {
                    Fault(instance,
                        $"Default flow '{gateway.DefaultFlowId}' of gateway '{gateway.Id}' does not exist.");
                    return false;
                }

                return MoveToken(instance, token, defaultFlow);
            }

            Fault(instance,
                $"No condition matched at exclusive gateway '{gateway.Id}' and no default flow is set.");
            return false;
        }

        private bool ArmTimer(WorkflowInstance instance, Token token, TimerNode node)
        {
            DateTime dueUtc;
            try
            {
                object due = evaluator.Evaluate(node.DueExpression, Scope(instance, token), node.DueExpressionMode);
                switch (due)
                {
                    case DateTime dt:
                        dueUtc = dt.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                            : dt.ToUniversalTime();
                        break;
                    case TimeSpan span:
                        dueUtc = DateTime.UtcNow + span;
                        break;
                    default:
                        Fault(instance,
                            $"Timer '{node.Id}' expression yielded '{due}' - a DateTime or TimeSpan was expected.",
                            node.Id);
                        return false;
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Timer expression of node '{node.Id}' in instance '{instance.Id}' could not be evaluated: " +
                    $"{ex.OutlineException()}", LogSeverity.Error);
                Fault(instance, $"Timer '{node.Id}' expression failed: {ex.Message}", node.Id);
                return false;
            }

            token.Status = TokenStatus.Waiting;
            token.DueUtc = dueUtc;
            instance.Log("Waiting", node.Id, $"due {dueUtc:o}");
            return true;
        }

        /// <summary>
        /// Verarbeitet ein paralleles Gateway (AND). Mit hoechstens einer eingehenden Kante wirkt es
        /// als Split (ein Token je Ausgang); mit mehreren eingehenden Kanten als Join (feuert erst,
        /// wenn auf jeder eingehenden Kante ein Token angekommen ist).
        /// </summary>
        /// <remarks>
        /// Der Join zaehlt die am Knoten geparkten Tokens gegen die Zahl der eingehenden Kanten. Das
        /// deckt die uebliche, strukturierte Split-/Join-Klammer ab (ein Split, ein zugehoeriger
        /// Join). Ein Zaehlen je einzelner eingehender Kante - noetig bei unbalancierten Graphen mit
        /// Schleifen ueber denselben Join - ist bewusst noch nicht umgesetzt.
        /// </remarks>
        private bool ProcessParallelGateway(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            ParallelGatewayNode node)
        {
            IReadOnlyList<SequenceFlow> incoming = definition.IncomingFlows(node.Id);
            IReadOnlyList<SequenceFlow> outgoing = definition.OutgoingFlows(node.Id);

            if (outgoing.Count == 0)
            {
                Fault(instance, $"Parallel gateway '{node.Id}' has no outgoing flow.");
                return false;
            }

            // Split (oder Durchreiche): hoechstens eine eingehende Kante. Ein Token je Ausgang.
            if (incoming.Count <= 1)
            {
                token.Status = TokenStatus.Consumed;
                instance.Log(outgoing.Count > 1 ? "ParallelSplit" : "Entered", node.Id, node.Name,
                    outgoing.Count > 1 ? HistorySeverity.Info : HistorySeverity.Verbose);
                return SpawnOutgoing(instance, outgoing, token);
            }

            // Join: dieses Token kommt an und parkt als Joining. Die Fire-Entscheidung faellt bewusst
            // NICHT hier, sondern zentral in ResolveJoins - im sequenziellen Fall, nachdem alle Zweige
            // geparkt sind; im nebenlaeufigen Fall im serialisierten Commit gegen frischen Stand (atomar).
            token.Status = TokenStatus.Joining;
            instance.Log("Joining", node.Id, node.Name, HistorySeverity.Verbose);
            return true;
        }

        /// <summary>
        /// Verarbeitet ein <b>inklusives Gateway</b> (OR): als Split werden alle zutreffenden Ausgaenge
        /// genommen, als Join parkt das Token wie beim AND.
        /// </summary>
        /// <remarks>
        /// Der Split stempelt die ANZAHL der aktivierten Zweige auf seine Tokens - das ist der ganze
        /// Trick des strukturierten OR (siehe <see cref="InclusiveGatewayNode"/>). Bewusst auch dann als
        /// Split behandelt, wenn nur EIN Zweig zutrifft: sonst behielte das Token die Zweig-Herkunft
        /// seiner umgebenden Ebene, und der Join zaehlte es der falschen Region zu.
        /// </remarks>
        private bool ProcessInclusiveGateway(WorkflowInstance instance, WorkflowDefinition definition,
            Token token, InclusiveGatewayNode node)
        {
            IReadOnlyList<SequenceFlow> incoming = definition.IncomingFlows(node.Id);
            IReadOnlyList<SequenceFlow> outgoing = definition.OutgoingFlows(node.Id);

            if (outgoing.Count == 0)
            {
                Fault(instance, $"Inclusive gateway '{node.Id}' has no outgoing flow.");
                return false;
            }

            if (incoming.Count > 1)
            {
                // Join: die Entscheidung faellt zentral in ResolveJoins - wie beim AND, und aus demselben
                // Grund (ein Stand, gegen den ausgewertet wird).
                token.Status = TokenStatus.Joining;
                instance.Log("Joining", node.Id, node.Name, HistorySeverity.Verbose);
                return true;
            }

            var selected = new List<SequenceFlow>(outgoing.Count);
            foreach (SequenceFlow flow in outgoing)
            {
                if (flow.Id == node.DefaultFlowId)
                {
                    // Die Standard-Kante ist ausdruecklich fuer den Fall gedacht, dass sonst nichts
                    // zutrifft - sie nimmt an der Auswahl nicht teil (auch nicht mit einer Bedingung).
                    continue;
                }

                if (string.IsNullOrWhiteSpace(flow.Condition))
                {
                    // Ohne Bedingung heisst: immer. Genau darin unterscheidet sich das OR vom XOR, das
                    // hier die erste passende Kante nimmt und aufhoert.
                    selected.Add(flow);
                    continue;
                }

                bool matched;
                try
                {
                    matched = evaluator.EvaluateCondition(flow.Condition, Scope(instance, token),
                        flow.ConditionMode);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Condition of flow '{flow.Id}' at inclusive gateway '{node.Id}' in instance " +
                        $"'{instance.Id}' could not be evaluated: {ex.OutlineException()}", LogSeverity.Error);
                    Fault(instance, $"Condition of flow '{flow.Id}' failed: {ex.Message}", node.Id);
                    return false;
                }

                if (matched)
                {
                    selected.Add(flow);
                }
            }

            if (selected.Count == 0)
            {
                if (node.DefaultFlowId == null)
                {
                    Fault(instance,
                        $"No condition matched at inclusive gateway '{node.Id}' and no default flow is set.");
                    return false;
                }

                SequenceFlow defaultFlow = outgoing.FirstOrDefault(f => f.Id == node.DefaultFlowId);
                if (defaultFlow == null)
                {
                    Fault(instance,
                        $"Default flow '{node.DefaultFlowId}' of gateway '{node.Id}' does not exist.");
                    return false;
                }

                selected.Add(defaultFlow);
            }

            token.Status = TokenStatus.Consumed;
            instance.Log("InclusiveSplit", node.Id,
                $"{selected.Count} of {outgoing.Count} branch(es): "
                + string.Join(", ", selected.Select(f => f.Id)));
            return SpawnOutgoing(instance, selected, token, asSplit: true,
                splitBranchCount: selected.Count);
        }

        /// <summary>
        /// Waehlt die Tokens aus, mit denen ein <b>OR-Join</b> feuert: alle Zweige EINES Splits, sobald
        /// ihre angemeldete Zahl beisammen ist - oder null, wenn noch welche unterwegs sind.
        /// </summary>
        /// <remarks>
        /// Der Join beantwortet damit nicht die (ueber Bedingungen und Schleifen hinweg nicht
        /// entscheidbare) Frage, ob ihn noch jemand erreichen kann, sondern zaehlt gegen die Zahl, die
        /// sein Split angemeldet hat.
        /// <para>
        /// Ein Token OHNE diese Anmeldung kann hier nie mitgezaehlt werden - es kaeme aus einem anderen
        /// Gateway oder aus einer Kante, die jemand direkt auf den Join gezogen hat. Das faultet
        /// SOFORT statt still zu haengen: die Instanz wuerde sonst ewig warten, und die Ursache stuende
        /// nirgends.
        /// </para>
        /// </remarks>
        private List<Token> SelectInclusiveJoinSet(WorkflowInstance instance, IMergingGateway node,
            List<Token> parked)
        {
            Token orphan = parked.FirstOrDefault(t => t.SplitBranchCount == null || t.SplitTokenId == null);
            if (orphan != null)
            {
                Fault(instance,
                    $"Token '{orphan.Id}' arrived at inclusive join '{node.Id}' without a branch count - it "
                    + "did not come from the matching inclusive split, so it could never be counted. Check "
                    + "the connections leading into this gateway.", node.Id);
                return null;
            }

            foreach (IGrouping<string, Token> group in parked.GroupBy(t => t.SplitTokenId, StringComparer.Ordinal))
            {
                int expected = group.First().SplitBranchCount.Value;
                if (group.Any(t => t.SplitBranchCount != expected))
                {
                    // Kann nur passieren, wenn zwei Aktivierungen dieselbe Split-Id traegen - dann ist die
                    // Erwartung mehrdeutig. Laut, statt die groessere Zahl zu raten.
                    Fault(instance,
                        $"Tokens of split '{group.Key}' at inclusive join '{node.Id}' declare different "
                        + "branch counts - the expectation is ambiguous.", node.Id);
                    return null;
                }

                if (group.Count() >= expected)
                {
                    return group.Take(expected).ToList();
                }
            }

            return null;
        }

        /// <summary>
        /// Loest fertige AND-Joins auf: fuer jedes parallele Gateway mit mehreren Eingaengen, an dem
        /// genug Tokens geparkt sind (Zahl geparkter Joining-Tokens &gt;= Zahl eingehender Kanten), werden
        /// diese verbraucht und die Ausgaenge gespawnt (ein neuer aktiver Zweig je Ausgang). Liefert true,
        /// wenn mindestens ein Join gefeuert hat. Bewusst getrennt von der Ankunft (ProcessParallelGateway),
        /// damit die Entscheidung an EINER Stelle gegen den aktuellen Token-Stand faellt - Voraussetzung
        /// fuer den atomaren Join unter Nebenlaeufigkeit (Auswertung im serialisierten Commit).
        /// </summary>
        /// <summary>
        /// Waehlt die Tokens aus, mit denen ein Join feuert - <b>genau eines je eingehender Kante</b> -
        /// oder null, wenn noch nicht jede Kante geliefert hat.
        /// </summary>
        /// <remarks>
        /// Die blosse ANZAHL wartender Tokens gegen die Zahl der Kanten zu pruefen (so lief es frueher)
        /// haelt nur bei balancierten Graphen. Laufen ueber EINE Kante zwei Tokens ein, waehrend eine
        /// andere leer bleibt - moeglich, sobald eine Schleife ueber denselben Join zurueckfuehrt -, dann
        /// stimmt die Summe, und der Join feuert mit halber Mannschaft. Der Fehler ist im Ergebnis
        /// sichtbar (ein Zweig fehlt im Merge), aber nicht in der Ursache.
        /// <para>
        /// Je Kante wird das <b>aelteste</b> wartende Token genommen (Reihenfolge der Token-Liste =
        /// Entstehungsreihenfolge). Bleiben Tokens uebrig, gehoeren sie zur naechsten Runde und warten
        /// weiter - der Fixpunkt-Durchlauf greift sie beim naechsten Mal.
        /// </para>
        /// <para>
        /// <b>Rueckfall fuer laufende Instanzen:</b> Tokens, die vor der Einfuehrung von
        /// <see cref="Token.ArrivedViaFlowId"/> geparkt wurden, kennen ihre Kante nicht. Fuer die gilt
        /// weiterhin die alte Zaehlung - sonst wuerde ein Join, an dem beim Deployment gerade jemand
        /// wartet, nie mehr feuern und die Instanz haenge fuer immer.
        /// </para></remarks>
        private static List<Token> SelectJoinSet(WorkflowInstance instance, WorkflowNode node,
            IReadOnlyList<SequenceFlow> incoming, List<Token> parked)
        {
            if (parked.Count < incoming.Count)
            {
                return null;
            }

            if (parked.Any(t => t.ArrivedViaFlowId == null))
            {
                LogEnvironment.LogEvent(
                    $"Join '{node.Id}' in instance '{instance.Id}' has tokens without a recorded arrival " +
                    "flow (parked before that was tracked) - falling back to counting. Once these have " +
                    "passed through, the per-edge rule applies again.", LogSeverity.Report);
                return parked.Take(incoming.Count).ToList();
            }

            var joined = new List<Token>(incoming.Count);
            foreach (SequenceFlow flow in incoming)
            {
                Token first = parked.FirstOrDefault(t => t.ArrivedViaFlowId == flow.Id
                                                         && !joined.Contains(t));
                if (first == null)
                {
                    return null; // Diese Kante hat noch nicht geliefert.
                }

                joined.Add(first);
            }

            return joined;
        }

        private bool ResolveJoins(WorkflowInstance instance, WorkflowDefinition definition)
        {
            bool firedAny = false;
            bool progress = true;
            while (progress && instance.Status != WorkflowStatus.Faulted)
            {
                progress = false;
                foreach (WorkflowNode node in definition.Nodes)
                {
                    if (node is not IMergingGateway gateway)
                    {
                        continue;
                    }

                    IReadOnlyList<SequenceFlow> incoming = definition.IncomingFlows(node.Id);
                    if (incoming.Count <= 1)
                    {
                        continue; // Split, kein Join.
                    }

                    var parked = instance.Tokens
                        .Where(t => t.NodeId == node.Id && t.Status == TokenStatus.Joining)
                        .ToList();

                    // Zwei Regeln, eine Stelle: der AND-Join fragt je eingehender KANTE, der OR-Join
                    // zaehlt die Zweige, die sein Split angemeldet hat.
                    List<Token> joined = node is InclusiveGatewayNode
                        ? SelectInclusiveJoinSet(instance, gateway, parked)
                        : SelectJoinSet(instance, node, incoming, parked);
                    if (instance.Status == WorkflowStatus.Faulted)
                    {
                        return firedAny; // Die Auswahl hat einen Modellfehler gemeldet.
                    }

                    if (joined == null)
                    {
                        continue;
                    }

                    foreach (Token p in joined)
                    {
                        p.Status = TokenStatus.Consumed;
                    }

                    // Die Zweig-Scopes zusammenfuehren. Das Ergebnis haengt am ersten der verbrauchten
                    // Tokens: es traegt als "Traeger" den zusammengefuehrten Stand und die Ebene, auf der
                    // es weitergeht - so bleibt die Zweig-Herkunft auch bei verschachtelten Splits intakt.
                    Token carrier = MergeBranches(instance, gateway, joined);

                    instance.Log(node is InclusiveGatewayNode ? "InclusiveJoin" : "ParallelJoin",
                        node.Id, node.Name);
                    if (!SpawnOutgoing(instance, definition.OutgoingFlows(node.Id), carrier))
                    {
                        return firedAny; // SpawnOutgoing hat auf Faulted gesetzt.
                    }

                    // Die Fortsetzung hat ihre Kopie - der Traeger wird frei, sofern der Join nicht
                    // zugleich gesplittet hat (dann haengen die frischen Zweige an ihm).
                    ReleaseBranchScope(instance, carrier);

                    firedAny = true;
                    progress = true;
                }
            }

            return firedAny;
        }

        /// <summary>
        /// Fuehrt die Zweig-Scopes der an einem Join eingetroffenen Tokens wieder zusammen und liefert den
        /// <b>Traeger</b> des Ergebnisses: eines der verbrauchten Tokens, das den zusammengefuehrten Stand
        /// und die Ebene traegt, auf der es weitergeht. Aus ihm spawnt der Join seine Ausgaenge.
        /// </summary>
        /// <remarks>
        /// Grundlage ist der Scope, aus dem gesplittet wurde (ueber <see cref="Token.SplitTokenId"/>
        /// gefunden) - er ist seit dem Split unveraendert, weil alle Schreibzugriffe der Region in den
        /// Zweig-Kopien gelandet sind. Was ein Zweig gegenueber diesem Stand geaendert hat, ist damit
        /// genau sein Beitrag. Ohne deklariertes <see cref="ParallelGatewayNode.Outputs"/> fliessen alle
        /// Beitraege nach oben (Verhalten wie bisher); mit Deklaration kommt genau das Deklarierte heraus.
        /// <para>
        /// Bewusst NICHT uebernommen werden Loeschungen in einem Zweig (eine Konsolidierung mit
        /// <see cref="ActivityScopeMode.Replace"/> innerhalb eines Zweigs raeumt nur DESSEN Kopie ab) -
        /// das Abraeumen fuer die Region ist Sache des Joins.
        /// </para>
        /// </remarks>
        private static Token MergeBranches(WorkflowInstance instance, IMergingGateway node, List<Token> joined)
        {
            Token carrier = joined[0];
            Token parent = FindSplitParent(instance, node, joined);
            Dictionary<string, object> baseScope = parent?.Variables ?? instance.Variables;

            // 1. Beitraege der Zweige gegen den Stand vom Split sammeln.
            var merged = new Dictionary<string, object>(baseScope, StringComparer.Ordinal);
            var writtenBy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Token branch in joined)
            {
                if (branch.Variables == null)
                {
                    // Zweig ohne eigene Kopie (Ankunft ohne Split / Instanz aus der Zeit vor den
                    // Zweig-Scopes): er hat direkt im Basis-Scope geschrieben, dort steht sein Beitrag schon.
                    continue;
                }

                foreach (KeyValuePair<string, object> kv in branch.Variables)
                {
                    if (baseScope.TryGetValue(kv.Key, out object atSplit) && Equals(atSplit, kv.Value))
                    {
                        continue; // unveraendert - kein Beitrag dieses Zweigs.
                    }

                    if (writtenBy.TryGetValue(kv.Key, out string firstBranch)
                        && merged.TryGetValue(kv.Key, out object previous) && !Equals(previous, kv.Value))
                    {
                        // Zwei Zweige haben dieselbe Variable auf VERSCHIEDENE Werte gesetzt. Es gewinnt
                        // deterministisch der spaeter gespawnte Zweig - aber nicht still: das gehoert per
                        // Join-Mapping entschieden (je Zweig ein eigener Ergebnisname).
                        string detail =
                            $"variable '{kv.Key}' was set by more than one branch (tokens '{firstBranch}' " +
                            $"and '{branch.Id}') with different values - the later branch wins";
                        instance.Log("BranchMergeConflict", node.Id, detail, HistorySeverity.Warning);
                        LogEnvironment.LogEvent(
                            $"Join '{node.Id}' in instance '{instance.Id}': {detail}. Decide it with a join " +
                            "mapping (one result name per branch).", LogSeverity.Warning);
                    }

                    merged[kv.Key] = kv.Value;
                    writtenBy[kv.Key] = branch.Id;
                }
            }

            // 2. Das Ergebnis der Region bestimmen: ohne Deklaration alles, mit Deklaration genau das
            //    Abgebildete (dieselbe Extend/Replace-Semantik wie ueberall sonst).
            Dictionary<string, object> target;
            if (node.Outputs is { Count: > 0 } || node.ScopeMode == ActivityScopeMode.Replace)
            {
                target = new Dictionary<string, object>(baseScope, StringComparer.Ordinal);
                ApplyMappedOutputs(instance, target, node.Id, node.Outputs, node.ScopeMode,
                    node.RetainVariables, merged);
            }
            else
            {
                target = merged;
            }

            // 3. Auf der Ebene veroeffentlichen, auf der es weitergeht: innerhalb einer aeusseren Region im
            //    Zweig-Scope, sonst im Instanz-Scope.
            if (parent?.Variables != null)
            {
                carrier.Variables = target;
            }
            else
            {
                carrier.Variables = null;
                instance.Variables.Clear();
                foreach (KeyValuePair<string, object> kv in target)
                {
                    instance.Variables[kv.Key] = kv.Value;
                }
            }

            carrier.SplitTokenId = parent?.SplitTokenId;

            // Die Kopien der uebrigen Zweige sind aufgegangen - ihr Inhalt steht jetzt oben. Sie werden
            // freigegeben, damit nicht jede Zweig-Aktivierung eine vollstaendige Stack-Kopie behaelt.
            foreach (Token branch in joined)
            {
                if (!ReferenceEquals(branch, carrier))
                {
                    branch.Variables = null;
                }
            }

            // Der Scope des SPLIT-Tokens war die Basis dieser Ebene und wird jetzt ggf. frei.
            ReleaseBranchScope(instance, parent);

            instance.Log("BranchesMerged", node.Id,
                $"{joined.Count} branch(es), {writtenBy.Count} variable(s) from the branches",
                HistorySeverity.Verbose);
            return carrier;
        }

        /// <summary>
        /// Gibt den Zweig-Scope eines verbrauchten Tokens frei, sobald ihn niemand mehr als Basis braucht -
        /// also kein lebendes Token mehr aus ihm hervorgegangen ist. Ohne das behielte jede Aktivierung
        /// einer parallelen Region dauerhaft eine vollstaendige Kopie des Stacks.
        /// </summary>
        /// <remarks>
        /// Die Lebendigkeits-Pruefung ist noetig, weil ein unbalancierter Graph einen weiteren Zweig
        /// desselben Splits noch unterwegs haben kann - und weil ein Join, der zugleich splittet, seine
        /// frischen Zweige an genau diesem Token verankert.
        /// </remarks>
        private static void ReleaseBranchScope(WorkflowInstance instance, Token token)
        {
            if (token?.Variables == null || token.Status != TokenStatus.Consumed)
            {
                return;
            }

            if (!instance.Tokens.Any(t => t.SplitTokenId == token.Id && t.Status != TokenStatus.Consumed))
            {
                token.Variables = null;
            }
        }

        /// <summary>
        /// Sucht den Split, aus dem die an einem Join eingetroffenen Tokens hervorgegangen sind. Null =
        /// kein Zweig-Scope im Spiel (Instanz-Scope ist die Basis) - das ist der Normalfall fuer Instanzen
        /// aus der Zeit vor den Zweig-Scopes und deshalb kein Fehler, sondern der vertraegliche Rueckfall.
        /// </summary>
        private static Token FindSplitParent(WorkflowInstance instance, IMergingGateway node,
            List<Token> joined)
        {
            string splitId = joined[0].SplitTokenId;
            if (splitId == null)
            {
                return null;
            }

            if (joined.Any(t => t.SplitTokenId != splitId))
            {
                LogEnvironment.LogEvent(
                    $"Join '{node.Id}' in instance '{instance.Id}' merges branches that come from different " +
                    $"splits ({string.Join(", ", joined.Select(t => $"'{t.SplitTokenId ?? "-"}'").Distinct())}) - " +
                    $"the merge uses the scope of '{splitId}'. The graph is not properly nested.",
                    LogSeverity.Warning);
            }

            Token parent = instance.Tokens.FirstOrDefault(t => t.Id == splitId);
            if (parent == null)
            {
                LogEnvironment.LogEvent(
                    $"Join '{node.Id}' in instance '{instance.Id}': the split token '{splitId}' is no longer " +
                    "in the instance - the merge falls back to the instance scope.", LogSeverity.Warning);
            }

            return parent;
        }

        /// <summary>
        /// Erzeugt die Folge-Tokens eines Knotens, der seine Ausgaenge alle gleichzeitig nimmt (AND-Split
        /// bzw. die Fortsetzung hinter einem Join). Der <paramref name="source"/>-Token ist der Strang, aus
        /// dem gespawnt wird - er liefert Scope und Zweig-Herkunft.
        /// </summary>
        /// <remarks>
        /// Bei mehr als einem Ausgang ist das ein <b>Split</b>: jeder Strang bekommt eine eigene KOPIE des
        /// Scopes und merkt sich in <see cref="Token.SplitTokenId"/>, woraus er hervorgegangen ist. Damit
        /// arbeiten parallele Zweige ab hier isoliert; der zugehoerige Join fuehrt die Kopien wieder
        /// zusammen. Bei genau einem Ausgang ist es eine Durchreiche - der Strang bleibt auf seiner Ebene.
        /// </remarks>
        /// <param name="asSplit">
        /// null = nach der Zahl der Ausgaenge entscheiden (der Normalfall). false erzwingt die
        /// Durchreiche trotz mehrerer Ausgaenge - das braucht das <b>Rennen</b> am ereignisbasierten
        /// Gateway: dort ueberlebt genau ein Strang, eigene Scope-Kopien je Zweig waeren also nur eine
        /// Frage danach, wessen Stand der Gewinner mitnimmt.
        /// </param>
        /// <param name="raceTokenId">
        /// gesetzt fuer die Kinder eines ereignisbasierten Gateways: die Id des Gateway-Tokens, die sie
        /// als Geschwister desselben Rennens ausweist
        /// </param>
        private bool SpawnOutgoing(WorkflowInstance instance, IReadOnlyList<SequenceFlow> outgoing, Token source,
            bool? asSplit = null, string raceTokenId = null, int? splitBranchCount = null)
        {
            bool split = asSplit ?? outgoing.Count > 1;
            Dictionary<string, object> sourceScope = Scope(instance, source);

            // Erst alle Kanten pruefen und ihr Mapping anwenden, dann die Tokens setzen: scheitert eine
            // Kante, entsteht so kein halb gespawnter Zweig. (Auch der gespawnte Zweig durchlaeuft das
            // Mapping seiner Kante - sonst wuerde es je nach Quellknoten verschieden gelten.)
            var spawned = new List<Token>(outgoing.Count);
            foreach (SequenceFlow flow in outgoing)
            {
                if (flow.TargetId == null)
                {
                    Fault(instance, $"Flow '{flow.Id}' has no target.");
                    return false;
                }

                var token = new Token
                {
                    NodeId = flow.TargetId,
                    Status = TokenStatus.Active,
                    Variables = split ? CopyScope(sourceScope) : CopyScope(source?.Variables),
                    SplitTokenId = split ? source?.Id : source?.SplitTokenId,
                    SplitBranchCount = split ? splitBranchCount : source?.SplitBranchCount,
                    RaceTokenId = raceTokenId,
                    ArrivedViaFlowId = flow.Id,
                    SubProcessOwnerTokenId = source?.SubProcessOwnerTokenId
                };

                if (!ApplyFlowInputs(instance, token, flow))
                {
                    return false;
                }

                spawned.Add(token);
            }

            instance.Tokens.AddRange(spawned);
            return true;
        }

        /// <summary>Bewegt einen Token ueber die einzige ausgehende Kante seines aktuellen Knotens.</summary>
        private bool MoveAlongSingleOutgoing(WorkflowInstance instance, WorkflowDefinition definition, Token token)
        {
            IReadOnlyList<SequenceFlow> outgoing = definition.OutgoingFlows(token.NodeId);
            if (outgoing.Count != 1)
            {
                Fault(instance,
                    $"Node '{token.NodeId}' must have exactly one outgoing flow, but has {outgoing.Count}.");
                return false;
            }

            return MoveToken(instance, token, outgoing[0]);
        }

        private bool MoveToken(WorkflowInstance instance, Token token, SequenceFlow flow)
        {
            if (flow.TargetId == null)
            {
                Fault(instance, $"Flow '{flow.Id}' has no target.");
                return false;
            }

            // Erst das Mapping der Kante, dann ankommen: der Zielknoten soll den Stack schon so sehen, wie
            // ihn die Kante normalisiert hat. Scheitert es, bleibt das Token stehen (die Instanz ist ohnehin
            // gefaultet) - so zeigt der Monitor, an welcher Stelle der Lauf haengengeblieben ist.
            if (!ApplyFlowInputs(instance, token, flow))
            {
                return false;
            }

            // Das Haupt-Token verlaesst seinen Schritt: die dort haengenden Fristen-Timer und ein
            // eventuell laufender Nebenpfad sind damit gegenstandslos. Der EINE Durchgang, durch den
            // jedes Token einen Knoten verlaesst - deshalb hier und nicht an jeder Aufrufstelle.
            KillBoundaryTokens(instance, token.Id);

            // Aus demselben Grund hier: laeuft ein Token weiter, das an einem ereignisbasierten Gateway um
            // die Wette gewartet hat, ist das Rennen entschieden.
            KillRaceSiblings(instance, token);

            // Und ebenso: verlaesst das aeussere Token einen Subprozess-Knoten, ohne dass der Abschnitt
            // fertig geworden waere (Frist abgelaufen, Fehler-Ausgang), ist sein Innenraum gegenstandslos.
            KillSubProcessTokens(instance, token.Id);

            token.NodeId = flow.TargetId;
            token.Status = TokenStatus.Active;
            token.ArrivedViaFlowId = flow.Id;
            return true;
        }

        /// <summary>
        /// Wendet das optionale Mapping einer Kante (<see cref="SequenceFlow.Inputs"/>) auf den
        /// Variablen-Stack an - die Antwort auf "wie sieht der Stack aus, wenn ich hier ankomme".
        /// Liefert false, wenn eine Bindung nicht aufloesbar war (die Instanz ist dann gefaultet).
        /// </summary>
        /// <remarks>
        /// Nutzt bewusst denselben Kern wie Aktivitaet, Subworkflow-Aufruf und Start-Signatur
        /// (<see cref="ApplyMappedOutputs"/>): Extend/Replace duerfen an keiner der Stellen etwas anderes
        /// bedeuten. Ohne Bindungen und ohne Konsolidierung passiert nichts - bestehende Definitionen
        /// verhalten sich unveraendert.
        /// </remarks>
        private bool ApplyFlowInputs(WorkflowInstance instance, Token token, SequenceFlow flow)
        {
            bool hasInputs = flow?.Inputs is { Count: > 0 };
            if (flow == null || (!hasInputs && flow.ScopeMode != ActivityScopeMode.Replace))
            {
                return true;
            }

            // Das Mapping laeuft im Scope des Tokens, das die Kante nimmt: an einem Split hat jeder Strang
            // dabei schon seine eigene Kopie - die Mappings der Zweig-Kanten koennen einander also nicht
            // mehr ueberschreiben.
            Dictionary<string, object> scope = Scope(instance, token);
            IDictionary<string, object> resolved;
            try
            {
                resolved = ResolveInputs(instance, scope, flow.Inputs, flow.Id);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Mapping of connection '{flow.Id}' in instance '{instance.Id}' could not be resolved: " +
                    $"{ex.OutlineException()}", LogSeverity.Error);
                Fault(instance, $"Mapping of connection '{flow.Id}' failed: {ex.Message}", flow.SourceId);
                return false;
            }

            // Die aufgeloesten Werte tragen bereits ihren Ziel-Namen - deshalb Identitaets-Bindungen. Die
            // Historie haengt am ZIEL-Knoten (eine Kante ist im Monitor kein anspringbarer Eintrag).
            ApplyMappedOutputs(instance, scope, flow.TargetId,
                resolved.Keys.Select(k => new ActivityOutputBinding { Parameter = k, Variable = k }).ToList(),
                flow.ScopeMode, flow.RetainVariables, resolved);

            instance.Log("Mapped", flow.TargetId,
                $"connection '{flow.Id}': {resolved.Count} variable(s), " +
                $"{flow.ScopeMode.ToString().ToLowerInvariant()}", HistorySeverity.Verbose);
            return true;
        }

        private static void UpdateTerminalStatus(WorkflowInstance instance, WorkflowDefinition definition)
        {
            // Erst aufraeumen, dann urteilen: ein verwaistes Timer-Token wuerde die Instanz sonst ewig
            // als "wartend" fuehren, obwohl sein Schritt laengst vorbei ist.
            CollectOrphanedBoundaryTokens(instance);

            if (instance.Tokens.Any(t => t.Status == TokenStatus.Active))
            {
                instance.Status = WorkflowStatus.Running;
            }
            else if (instance.Tokens.Any(t => t.Status == TokenStatus.Waiting
                                              || t.Status == TokenStatus.WaitingForTarget))
            {
                // Ein wartender Zweig (aeusseres Ereignis ODER anderer Host) kann noch aktiv werden und an
                // einen offenen Join liefern - die Instanz ruht, bis Signal/Timer/Ziel-Runner sie aufnimmt.
                instance.Status = WorkflowStatus.Waiting;
            }
            else if (instance.Tokens.Any(t => t.Status == TokenStatus.Joining))
            {
                // Tokens haengen an einem AND-Join, aber es gibt weder aktive noch wartende Zweige,
                // die noch liefern koennten - der Join kann nie feuern.
                Fault(instance,
                    "Deadlock: a parallel join is missing tokens and no branch can still deliver one.");
            }
            else
            {
                // Genau hier - und nur hier - entsteht das Ergebnis der Instanz.
                ApplyEndOutputs(instance, definition);
                instance.Status = WorkflowStatus.Completed;
                instance.Log("Completed");
            }
        }

        /// <summary>
        /// Sucht einen nebenlaeufigen Schreibkonflikt: eine Variable, die DIESER Zweig schreibt und die seit
        /// seinem Fork bereits ein Geschwister-Zweig auf einen ANDEREN Wert gesetzt hat. Liefert den
        /// Variablennamen, oder null (kein Konflikt). Gleicher Zielwert gilt bewusst NICHT als Konflikt (das
        /// Ergebnis ist ordnungs-unabhaengig und deterministisch) - der Fault greift nur bei divergierenden
        /// Werten ("kein stiller last-writer"). Basiert nur auf den geschriebenen Werten (kein Read-Tracking);
        /// den strukturellen Fall deckt zusaetzlich die Design-Zeit-Warnung des Validators ab.
        /// </summary>
        private static string FindWriteConflict(BranchSnapshot snapshot, BranchDelta delta, WorkflowInstance fresh)
        {
            foreach (KeyValuePair<string, object> write in delta.VariableWrites)
            {
                bool baseHas = snapshot.TryGetBaseVariable(write.Key, out object baseValue);
                bool freshHas = fresh.Variables.TryGetValue(write.Key, out object freshValue);

                // Hat seit unserem Fork jemand anders diese Variable veraendert (Wert weicht vom Fork-Stand ab,
                // oder sie wurde entfernt bzw. neu angelegt)?
                bool changedByOther = baseHas ? (!freshHas || !Equals(freshValue, baseValue)) : freshHas;
                if (changedByOther && !(freshHas && Equals(freshValue, write.Value)))
                {
                    return write.Key;
                }
            }

            return null;
        }

        private static void Fault(WorkflowInstance instance, string message, string nodeId = null)
        {
            instance.Status = WorkflowStatus.Faulted;
            instance.FaultMessage = message;
            instance.Log("Faulted", nodeId, message, HistorySeverity.Error);
        }

        private WorkflowDefinition LoadDefinition(WorkflowInstance instance)
        {
            // Ueber die technische Kennung, nicht ueber den Namen: eine laufende Instanz bleibt damit an
            // GENAU dem Graphen, mit dem sie gestartet wurde. Ueber den Namen wuerde sie in dem Moment
            // still auf einen anderen wechseln, in dem jemand eine mandanteneigene Fassung derselben
            // Id und Version anlegt.
            WorkflowDefinition definition = store.GetDefinition(instance.DefinitionKey)
                ?? throw new InvalidOperationException(
                    $"No definition with key {instance.DefinitionKey} ('{instance.DefinitionId}' " +
                    $"v{instance.DefinitionVersion}) for instance '{instance.Id}'.");

            // Die eine Stelle, an der die Engine jede Instanz in die Hand bekommt (jeder Vortrieb laedt
            // seine Definition) - damit auch die eine Stelle, an der der Protokoll-Filter haengt. Eine
            // frisch aus dem Store geladene Instanz traegt ihn sonst nicht.
            instance.HistoryFilter = FilterFor(definition);
            return definition;
        }

        /// <summary>
        /// Der fuer eine Definition geltende Protokoll-Filter: der Filter der Engine (ersatzweise der
        /// prozessweite Standard), ueberschrieben von der Mindest-Stufe der Definition, falls sie eine
        /// setzt.
        /// </summary>
        private IWorkflowHistoryFilter FilterFor(WorkflowDefinition definition)
        {
            IWorkflowHistoryFilter filter = HistoryFilter ?? WorkflowHistoryFilter.Default;
            if (definition?.MinHistorySeverity == null)
            {
                return filter;
            }

            return WorkflowHistoryFilter.OverrideMinSeverity(filter, definition.MinHistorySeverity.Value);
        }

        /// <summary>
        /// Ein Schnappschuss des Instanzzustands bei Zweig-Start. Diff dagegen liefert das kleine
        /// Zweig-Delta (nur was DIESER Zweig geaendert hat), das auf einen frischen Stand gemergt wird.
        /// </summary>
        private sealed class BranchSnapshot
        {
            private readonly Dictionary<string, object> variables;
            private readonly Dictionary<string, Token> tokens;
            private readonly int historyCount;
            private readonly int compensationCount;

            public BranchSnapshot(WorkflowInstance instance)
            {
                variables = new Dictionary<string, object>(instance.Variables);
                tokens = instance.Tokens.ToDictionary(t => t.Id, t => t.CloneState());
                historyCount = instance.History.Count;
                compensationCount = instance.Compensations.Count;
            }

            public bool HasToken(string id) => tokens.ContainsKey(id);

            /// <summary>Liefert den Wert einer Variablen zum Fork-Zeitpunkt dieses Zweigs (fuer die Konfliktpruefung).</summary>
            public bool TryGetBaseVariable(string key, out object value) => variables.TryGetValue(key, out value);

            public BranchDelta DiffTo(WorkflowInstance instance)
            {
                var delta = new BranchDelta();

                foreach (KeyValuePair<string, object> kv in instance.Variables)
                {
                    if (!variables.TryGetValue(kv.Key, out object old) || !Equals(old, kv.Value))
                    {
                        delta.VariableWrites[kv.Key] = kv.Value;
                    }
                }

                foreach (string key in variables.Keys)
                {
                    if (!instance.Variables.ContainsKey(key))
                    {
                        delta.RemovedVariableKeys.Add(key);
                    }
                }

                var currentIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (Token t in instance.Tokens)
                {
                    currentIds.Add(t.Id);
                    if (!tokens.TryGetValue(t.Id, out Token old) || !Token.SameState(old, t))
                    {
                        delta.TokenUpserts.Add(t.CloneState());
                    }
                }

                foreach (string id in tokens.Keys)
                {
                    if (!currentIds.Contains(id))
                    {
                        delta.RemovedTokenIds.Add(id);
                    }
                }

                for (int i = historyCount; i < instance.History.Count; i++)
                {
                    delta.HistoryAppends.Add(instance.History[i]);
                }

                // Vormerkungen wie das Protokoll append-only. Ein bereits vorhandener Eintrag kann sich
                // aber noch AENDERN (er wird als zurueckgenommen markiert) - deshalb zusaetzlich die Ids
                // der inzwischen abgehakten.
                for (int i = compensationCount; i < instance.Compensations.Count; i++)
                {
                    delta.CompensationAppends.Add(instance.Compensations[i]);
                }

                for (int i = 0; i < compensationCount && i < instance.Compensations.Count; i++)
                {
                    if (instance.Compensations[i].Compensated)
                    {
                        delta.CompensatedIds.Add(instance.Compensations[i].Id);
                    }
                }

                if (instance.Status == WorkflowStatus.Faulted)
                {
                    delta.Faulted = true;
                    delta.FaultMessage = instance.FaultMessage;
                }

                return delta;
            }

        }

        /// <summary>
        /// Das Delta eines Zweig-Vortriebs (nur die Aenderungen dieses Zweigs). Wird beim Commit auf einen
        /// frisch geladenen Stand angewandt - so ueberschreibt ein Zweig nicht die Aenderungen seiner
        /// Geschwister, sondern mergt nur seinen kleinen Beitrag.
        /// </summary>
        private sealed class BranchDelta
        {
            public Dictionary<string, object> VariableWrites { get; } =
                new Dictionary<string, object>(StringComparer.Ordinal);

            public HashSet<string> RemovedVariableKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

            public List<Token> TokenUpserts { get; } = new List<Token>();

            public HashSet<string> RemovedTokenIds { get; } = new HashSet<string>(StringComparer.Ordinal);

            public List<HistoryEntry> HistoryAppends { get; } = new List<HistoryEntry>();

            public List<CompensationEntry> CompensationAppends { get; } = new List<CompensationEntry>();

            public HashSet<string> CompensatedIds { get; } = new HashSet<string>(StringComparer.Ordinal);

            public bool Faulted { get; set; }

            public string FaultMessage { get; set; }

            public void ApplyTo(WorkflowInstance fresh)
            {
                foreach (KeyValuePair<string, object> kv in VariableWrites)
                {
                    fresh.Variables[kv.Key] = kv.Value;
                }

                foreach (string key in RemovedVariableKeys)
                {
                    fresh.Variables.Remove(key);
                }

                // Der Merge uebertraegt den GANZEN Zustand - ueber dieselbe Feldliste, die auch
                // Schnappschuss und Diff benutzen (Token.CopyStateFrom). Eine eigene Auswahl hier hiesse:
                // der Diff meldet eine Aenderung, die der Merge nicht mitnimmt - das Feld bliebe still auf
                // dem alten Stand. Der Zweig-Scope wird dabei ganz ersetzt, nicht gemergt: nur DIESER Zweig
                // schreibt ihn (parallele Geschwister haben ihre eigene Kopie).
                foreach (Token t in TokenUpserts)
                {
                    Token existing = fresh.Tokens.FirstOrDefault(x => x.Id == t.Id);
                    if (existing == null)
                    {
                        fresh.Tokens.Add(t.CloneState());
                    }
                    else
                    {
                        existing.CopyStateFrom(t);
                    }
                }

                foreach (string id in RemovedTokenIds)
                {
                    fresh.Tokens.RemoveAll(x => x.Id == id);
                }

                fresh.History.AddRange(HistoryAppends);
                fresh.Compensations.AddRange(CompensationAppends);
                foreach (CompensationEntry entry in fresh.Compensations
                             .Where(c => CompensatedIds.Contains(c.Id)))
                {
                    entry.Compensated = true;
                }

                if (Faulted)
                {
                    fresh.Status = WorkflowStatus.Faulted;
                    fresh.FaultMessage = FaultMessage;
                }
            }
        }
    }
}
