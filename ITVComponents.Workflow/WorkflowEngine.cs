using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ITVComponents.Formatting;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Expressions;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Runtime;
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
        public WorkflowEngine(IWorkflowStore store, IActivityHost activities,
            IExpressionEvaluator evaluator = null, IEnumerable<string> hostTargets = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.activities = activities ?? throw new ArgumentNullException(nameof(activities));
            this.evaluator = evaluator ?? new CScriptExpressionEvaluator();
            this.hostTargets = new HashSet<string>(
                hostTargets ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            Runtime = new WorkflowRuntimeContext();
        }

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
        public WorkflowInstance CreateInstance(string definitionId,
            IDictionary<string, object> initialVariables = null, string correlationKey = null)
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
                DefinitionId = definition.Id,
                DefinitionVersion = definition.Version,
                Status = WorkflowStatus.Running,
                CorrelationKey = correlationKey,
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
        /// <returns>die gestartete Instanz</returns>
        public WorkflowInstance StartWorkflow(string definitionId,
            IDictionary<string, object> initialVariables = null, string correlationKey = null)
        {
            WorkflowInstance instance = CreateInstance(definitionId, initialVariables, correlationKey);
            Advance(instance, LoadDefinition(instance));
            return instance;
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
            IDictionary<string, object> payloadVariables = null)
        {
            WorkflowInstance instance = store.GetInstance(instanceId)
                ?? throw new InvalidOperationException($"No instance found for '{instanceId}'.");

            var waiting = instance.Tokens
                .Where(t => t.Status == TokenStatus.Waiting && t.WaitingSignal == signalName)
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
                token.WaitingSignal = null;
                token.DueUtc = null;
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
        /// Liefert ein Signal ueber Korrelation an alle passenden wartenden Instanzen des Stores.
        /// </summary>
        /// <param name="signalName">der Signalname</param>
        /// <param name="correlationKey">der Korrelationsschluessel (oder eine Instanz-Id)</param>
        /// <param name="payloadVariables">optionale Variablen, die vor dem Weiterlauf gesetzt werden</param>
        /// <returns>die Anzahl der Instanzen, die weitergelaufen sind</returns>
        public int DeliverSignal(string signalName, string correlationKey = null,
            IDictionary<string, object> payloadVariables = null)
        {
            int count = 0;
            foreach (WorkflowInstance instance in store.FindWaitingForSignal(signalName, correlationKey).ToList())
            {
                if (SignalWorkflow(instance.Id, signalName, payloadVariables))
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
        public IReadOnlyList<string> ReactivateSignal(string instanceId, string signalName,
            IDictionary<string, object> payloadVariables = null)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            return ReactivateAndCommit(instanceId, "ReactivateSignal", (fresh, definition) =>
            {
                var waiting = fresh.Tokens
                    .Where(t => t.Status == TokenStatus.Waiting && t.WaitingSignal == signalName)
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
                    token.WaitingSignal = null;
                    token.DueUtc = null;
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
                    token.Status = TokenStatus.Waiting;
                    token.WaitingSignal = wait.SignalName;
                    instance.Log("Waiting", node.Id, wait.SignalName);
                    return true;

                case UserActivityNode userTask:
                    return ParkUserTask(instance, token, userTask);

                case TimerNode timer:
                    return ArmTimer(instance, token, timer);

                case CallWorkflowNode call:
                    return ProcessCallWorkflow(instance, definition, token, call);

                case ParallelGatewayNode parallel:
                    return ProcessParallelGateway(instance, definition, token, parallel);

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
                    assignedTo = evaluator.Evaluate(node.Assignment, scope)?.ToString();
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
            token.TaskPermission = node.RequiredPermission;
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
        /// Der Titel, unter dem die Aufgabe in der Arbeitsliste steht. Ein
        /// <see cref="UserActivityNode.TitleExpression"/> gewinnt (dann ist der Titel Klartext), sonst
        /// bleibt <see cref="UserActivityNode.Title"/> stehen - Kultur-JSON wird erst beim Anzeigen
        /// uebersetzt.
        /// </summary>
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
            if (!string.IsNullOrWhiteSpace(node.TitleExpression))
            {
                try
                {
                    string computed = evaluator.Evaluate(node.TitleExpression, scope)?.ToString();
                    if (!string.IsNullOrWhiteSpace(computed))
                    {
                        return computed;
                    }

                    LogEnvironment.LogEvent(
                        $"Title expression of user task '{node.Id}' in instance '{instance.Id}' produced no " +
                        "text - falling back to the static title.", LogSeverity.Warning);
                }
                catch (Exception ex)
                {
                    // Anders als bei der Zuweisung nur eine Meldung: ein fehlender Titel ist kosmetisch,
                    // und eine unerledigbare Aufgabe waere die teurere Folge.
                    LogEnvironment.LogEvent(
                        $"Title expression of user task '{node.Id}' in instance '{instance.Id}' failed - " +
                        $"falling back to the static title: {ex.OutlineException()}", LogSeverity.Error);
                }
            }

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
                data = evaluator.Evaluate(node.FormatData, scope);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Format data of user task '{node.Id}' in instance '{instance.Id}' could not be evaluated " +
                    $"for the title: {ex.OutlineException()}", LogSeverity.Warning);
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
                    formatData = evaluator.Evaluate(node.FormatData, taskScope);
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
            var context = new WorkflowActivityContext(instance, node, inputs, outputs, scope);
            try
            {
                // On-demand aufgeloest; die Lebensdauer der Aktivitaet (bei Plugins: der geladenen
                // Instanz) gehoert dem Scope und endet mit dem Vortrieb.
                IWorkflowActivity activity = activityScope.Resolve(node.ActivityRef);
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
                        result[binding.Parameter] = evaluator.Evaluate(binding.Source, scope);
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

            // Nur die tatsaechlich erreichten End-Knoten zaehlen (ein nie durchlaufener Alternativausgang
            // darf das Ergebnis nicht bestimmen).
            List<EndNode> reached = instance.Tokens
                .Where(t => t.Status == TokenStatus.Consumed)
                .Select(t => definition.GetNode(t.NodeId) as EndNode)
                .Where(e => e?.Outputs is { Count: > 0 })
                .GroupBy(e => e.Id, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            EndNode end = SelectDeclaring(reached, "workflow results", $"Instance '{instance.Id}'");
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
        private static T SelectDeclaring<T>(List<T> declaring, string what, string owner) where T : WorkflowNode
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
                    DefinitionId = subDef.Id,
                    DefinitionVersion = subDef.Version,
                    TenantId = instance.TenantId,
                    ParentInstanceId = instance.Id,
                    ParentTokenId = token.Id,
                    RootInstanceId = instance.EffectiveRootInstanceId,
                    CallDepth = instance.CallDepth + 1,
                    Status = WorkflowStatus.Running,
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
                    matched = evaluator.EvaluateCondition(flow.Condition, Scope(instance, token));
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
                object due = evaluator.Evaluate(node.DueExpression, Scope(instance, token));
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
        /// Loest fertige AND-Joins auf: fuer jedes parallele Gateway mit mehreren Eingaengen, an dem
        /// genug Tokens geparkt sind (Zahl geparkter Joining-Tokens &gt;= Zahl eingehender Kanten), werden
        /// diese verbraucht und die Ausgaenge gespawnt (ein neuer aktiver Zweig je Ausgang). Liefert true,
        /// wenn mindestens ein Join gefeuert hat. Bewusst getrennt von der Ankunft (ProcessParallelGateway),
        /// damit die Entscheidung an EINER Stelle gegen den aktuellen Token-Stand faellt - Voraussetzung
        /// fuer den atomaren Join unter Nebenlaeufigkeit (Auswertung im serialisierten Commit).
        /// </summary>
        private bool ResolveJoins(WorkflowInstance instance, WorkflowDefinition definition)
        {
            bool firedAny = false;
            bool progress = true;
            while (progress && instance.Status != WorkflowStatus.Faulted)
            {
                progress = false;
                foreach (WorkflowNode node in definition.Nodes)
                {
                    if (node is not ParallelGatewayNode gateway)
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
                    if (parked.Count < incoming.Count)
                    {
                        continue;
                    }

                    var joined = parked.Take(incoming.Count).ToList();
                    foreach (Token p in joined)
                    {
                        p.Status = TokenStatus.Consumed;
                    }

                    // Die Zweig-Scopes zusammenfuehren. Das Ergebnis haengt am ersten der verbrauchten
                    // Tokens: es traegt als "Traeger" den zusammengefuehrten Stand und die Ebene, auf der
                    // es weitergeht - so bleibt die Zweig-Herkunft auch bei verschachtelten Splits intakt.
                    Token carrier = MergeBranches(instance, gateway, joined);

                    instance.Log("ParallelJoin", node.Id, node.Name);
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
        private static Token MergeBranches(WorkflowInstance instance, ParallelGatewayNode node, List<Token> joined)
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
        private static Token FindSplitParent(WorkflowInstance instance, ParallelGatewayNode node,
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
        private bool SpawnOutgoing(WorkflowInstance instance, IReadOnlyList<SequenceFlow> outgoing, Token source)
        {
            bool split = outgoing.Count > 1;
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
                    SplitTokenId = split ? source?.Id : source?.SplitTokenId
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

            token.NodeId = flow.TargetId;
            token.Status = TokenStatus.Active;
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
            return store.GetDefinition(instance.DefinitionId, instance.DefinitionVersion)
                ?? throw new InvalidOperationException(
                    $"No definition '{instance.DefinitionId}' v{instance.DefinitionVersion} for instance '{instance.Id}'.");
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

            public BranchSnapshot(WorkflowInstance instance)
            {
                variables = new Dictionary<string, object>(instance.Variables);
                tokens = instance.Tokens.ToDictionary(t => t.Id, CopyToken);
                historyCount = instance.History.Count;
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
                    if (!tokens.TryGetValue(t.Id, out Token old) || !SameState(old, t))
                    {
                        delta.TokenUpserts.Add(CopyToken(t));
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

                if (instance.Status == WorkflowStatus.Faulted)
                {
                    delta.Faulted = true;
                    delta.FaultMessage = instance.FaultMessage;
                }

                return delta;
            }

            // Der Zweig-Scope wird MIT kopiert (eigene Dictionary-Instanz) - sonst zeigte der Schnappschuss
            // auf denselben Stack, den der Zweig gerade veraendert, und das Delta waere leer.
            private static Token CopyToken(Token t) => new Token
            {
                Id = t.Id, NodeId = t.NodeId, Status = t.Status, WaitingSignal = t.WaitingSignal,
                DueUtc = t.DueUtc, WaitingTarget = t.WaitingTarget,
                WaitingForChildInstanceId = t.WaitingForChildInstanceId,
                Variables = CopyScope(t.Variables), SplitTokenId = t.SplitTokenId,
                // Der Aufgaben-Stempel gehoert in den Vergleich: entsteht eine Aufgabe im Zweig-Vortrieb,
                // ist sie sonst nicht Teil des Deltas und das Token kaeme ohne Aufgabenart in die
                // Datenbank - unsichtbar fuer jede Arbeitsliste.
                TaskKey = t.TaskKey, TaskPermission = t.TaskPermission, AssignedTo = t.AssignedTo,
                TaskTitle = t.TaskTitle, TaskCreatedUtc = t.TaskCreatedUtc, TaskDueUtc = t.TaskDueUtc
            };

            private static bool SameState(Token a, Token b)
                => a.NodeId == b.NodeId && a.Status == b.Status && a.WaitingSignal == b.WaitingSignal
                   && Nullable.Equals(a.DueUtc, b.DueUtc) && a.WaitingTarget == b.WaitingTarget
                   && a.WaitingForChildInstanceId == b.WaitingForChildInstanceId
                   && a.SplitTokenId == b.SplitTokenId && a.TaskKey == b.TaskKey
                   && a.TaskPermission == b.TaskPermission && a.AssignedTo == b.AssignedTo
                   && a.TaskTitle == b.TaskTitle && Nullable.Equals(a.TaskCreatedUtc, b.TaskCreatedUtc)
                   && Nullable.Equals(a.TaskDueUtc, b.TaskDueUtc) && SameScope(a.Variables, b.Variables);

            private static bool SameScope(Dictionary<string, object> a, Dictionary<string, object> b)
            {
                if (ReferenceEquals(a, b))
                {
                    return true;
                }

                if (a == null || b == null || a.Count != b.Count)
                {
                    return false;
                }

                return a.All(kv => b.TryGetValue(kv.Key, out object other) && Equals(kv.Value, other));
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

                foreach (Token t in TokenUpserts)
                {
                    Token existing = fresh.Tokens.FirstOrDefault(x => x.Id == t.Id);
                    if (existing == null)
                    {
                        fresh.Tokens.Add(new Token
                        {
                            Id = t.Id, NodeId = t.NodeId, Status = t.Status,
                            WaitingSignal = t.WaitingSignal, DueUtc = t.DueUtc, WaitingTarget = t.WaitingTarget,
                            WaitingForChildInstanceId = t.WaitingForChildInstanceId,
                            Variables = CopyScope(t.Variables), SplitTokenId = t.SplitTokenId
                        });
                    }
                    else
                    {
                        existing.NodeId = t.NodeId;
                        existing.Status = t.Status;
                        existing.WaitingSignal = t.WaitingSignal;
                        existing.DueUtc = t.DueUtc;
                        existing.WaitingTarget = t.WaitingTarget;
                        existing.WaitingForChildInstanceId = t.WaitingForChildInstanceId;
                        // Der Zweig-Scope gehoert dem Zweig: er wird ganz ersetzt, nicht gemergt - nur
                        // DIESER Zweig schreibt ihn (parallele Geschwister haben ihre eigene Kopie).
                        existing.Variables = CopyScope(t.Variables);
                        existing.SplitTokenId = t.SplitTokenId;
                    }
                }

                foreach (string id in RemovedTokenIds)
                {
                    fresh.Tokens.RemoveAll(x => x.Id == id);
                }

                fresh.History.AddRange(HistoryAppends);

                if (Faulted)
                {
                    fresh.Status = WorkflowStatus.Faulted;
                    fresh.FaultMessage = FaultMessage;
                }
            }
        }
    }
}
