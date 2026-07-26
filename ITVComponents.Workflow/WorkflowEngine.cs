using System;
using System.Collections.Generic;
using System.Linq;
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

            var startNodes = definition.StartNodes().ToList();
            if (startNodes.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Definition '{definitionId}' has no start node.");
            }

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

            if (payloadVariables != null)
            {
                foreach (KeyValuePair<string, object> pair in payloadVariables)
                {
                    instance.Variables[pair.Key] = pair.Value;
                }
            }

            foreach (Token token in waiting)
            {
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
        /// <see cref="WorkflowStatus.Cancelled"/>. Bereits beendete Instanzen bleiben unveraendert.
        /// </summary>
        /// <param name="instanceId">die Instanz-Id</param>
        /// <returns>true, wenn die Instanz abgebrochen wurde</returns>
        public bool CancelWorkflow(string instanceId)
        {
            WorkflowInstance instance = store.GetInstance(instanceId);
            if (instance == null
                || instance.Status == WorkflowStatus.Completed
                || instance.Status == WorkflowStatus.Faulted
                || instance.Status == WorkflowStatus.Cancelled)
            {
                return false;
            }

            foreach (Token token in instance.Tokens)
            {
                token.Status = TokenStatus.Consumed;
            }

            instance.Status = WorkflowStatus.Cancelled;
            instance.Log("Cancelled", severity: HistorySeverity.Warning);
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
                        UpdateTerminalStatus(fresh);
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

                if (payloadVariables != null)
                {
                    foreach (KeyValuePair<string, object> pair in payloadVariables)
                    {
                        fresh.Variables[pair.Key] = pair.Value;
                    }
                }

                var ids = new List<string>();
                foreach (Token token in waiting)
                {
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
                UpdateTerminalStatus(instance);
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

        private bool RunActivity(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            AutomatedActivityNode node, IActivityScope activityScope)
        {
            instance.Log("Entered", node.Id, node.Name, HistorySeverity.Verbose);

            // Datenfluss hinein: die Eingabe-Bindungen des Knotens aufloesen. Ein Fehler hier (z.B. ein
            // ungueltiger Ausdruck) hat eine andere Ursache als ein Fehler in der Aktivitaet selbst -
            // deshalb ein eigener Zweig mit eigener, unterscheidbarer Log-/Fault-Meldung.
            IDictionary<string, object> inputs;
            try
            {
                inputs = ResolveInputs(instance, node.Inputs, node.Id);
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
            var context = new WorkflowActivityContext(instance, node, inputs, outputs);
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
            ApplyOutputs(instance, node, outputs);
            ResetAttempts(instance, node);
            instance.Log("Completed", node.Id, node.Name, HistorySeverity.Verbose);
            return MoveAlongSuccessFlow(instance, definition, token, node);
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

            if (applyOutputs && outputs != null)
            {
                ApplyOutputs(instance, node, outputs);
            }

            if (!string.IsNullOrEmpty(node.ErrorVariable))
            {
                instance.Variables[node.ErrorVariable] = message;
            }

            int attempts = 0;
            if (!string.IsNullOrEmpty(node.AttemptVariable))
            {
                instance.Variables.TryGetValue(node.AttemptVariable, out object current);
                attempts = (current is int i ? i : 0) + 1;
                instance.Variables[node.AttemptVariable] = attempts;
            }

            instance.Log("ActivityError", node.Id, message, HistorySeverity.Warning);
            return MoveToken(instance, token, errorFlow);
        }

        /// <summary>
        /// Bewegt einen Token nach erfolgreicher Aktivitaet weiter: ohne Fehler-Ausgang ueber die einzige
        /// ausgehende Kante; mit Fehler-Ausgang ueber die einzige NICHT-Fehler-Kante (die Erfolgs-Kante).
        /// </summary>
        private bool MoveAlongSuccessFlow(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            AutomatedActivityNode node)
        {
            if (string.IsNullOrEmpty(node.ErrorFlowId))
            {
                return MoveAlongSingleOutgoing(instance, definition, token);
            }

            var success = definition.OutgoingFlows(node.Id).Where(f => f.Id != node.ErrorFlowId).ToList();
            if (success.Count != 1)
            {
                Fault(instance,
                    $"Activity '{node.Id}' with an error flow must have exactly one success flow, but has {success.Count}.",
                    node.Id);
                return false;
            }

            return MoveToken(instance, token, success[0]);
        }

        /// <summary>Setzt den Fehlversuchs-Zaehler des Knotens bei Erfolg zurueck (falls konfiguriert).</summary>
        private static void ResetAttempts(WorkflowInstance instance, AutomatedActivityNode node)
        {
            if (!string.IsNullOrEmpty(node.AttemptVariable))
            {
                instance.Variables[node.AttemptVariable] = 0;
            }
        }

        /// <summary>
        /// Loest die Eingabe-Bindungen eines Aktivitaets-Knotens gegen den aktuellen Instanzzustand
        /// auf. Eine fehlende Variable (bei <see cref="ParameterBindingKind.Variable"/>) ist ein
        /// definierter Normalfall (der Wert ist dann null) - kein Fehler, aber protokolliert, damit er
        /// nachvollziehbar bleibt. Ein Ausdrucksfehler wird an den Aufrufer geworfen (der faultet).
        /// </summary>
        private IDictionary<string, object> ResolveInputs(WorkflowInstance instance,
            List<ActivityInputBinding> inputs, string nodeId)
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
                            && instance.Variables.TryGetValue(binding.Source, out object value))
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
                        result[binding.Parameter] = evaluator.Evaluate(binding.Source, instance.Variables);
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
        private static void ApplyOutputs(WorkflowInstance instance, AutomatedActivityNode node,
            IDictionary<string, object> outputs)
        {
            // Zuerst die Ziel-Variablen aus den Output-Bindungen bestimmen (unabhaengig vom Scope-Modus).
            var mapped = new Dictionary<string, object>(StringComparer.Ordinal);
            if (node.Outputs != null)
            {
                foreach (ActivityOutputBinding binding in node.Outputs)
                {
                    if (binding == null || string.IsNullOrEmpty(binding.Parameter)
                        || string.IsNullOrEmpty(binding.Variable))
                    {
                        continue;
                    }

                    outputs.TryGetValue(binding.Parameter, out object value);
                    mapped[binding.Variable] = value;
                }
            }

            if (node.ScopeMode == ActivityScopeMode.Replace)
            {
                // Konsolidierung: neuen Scope aus Retain-Whitelist + Ausgaben bauen, Rest verwerfen.
                var fresh = new Dictionary<string, object>();
                if (node.RetainVariables != null)
                {
                    foreach (string keep in node.RetainVariables)
                    {
                        if (!string.IsNullOrEmpty(keep) && instance.Variables.TryGetValue(keep, out object v))
                        {
                            fresh[keep] = v;
                        }
                    }
                }

                foreach (KeyValuePair<string, object> pair in mapped)
                {
                    fresh[pair.Key] = pair.Value; // Ausgaben gewinnen bei Kollision mit der Whitelist.
                }

                instance.Variables.Clear();
                foreach (KeyValuePair<string, object> pair in fresh)
                {
                    instance.Variables[pair.Key] = pair.Value;
                }

                instance.Log("Consolidated", node.Id, $"scope reduced to {fresh.Count} variable(s)");
            }
            else
            {
                foreach (KeyValuePair<string, object> pair in mapped)
                {
                    instance.Variables[pair.Key] = pair.Value;
                }
            }
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

            string childId = ChildInstanceId(instance.Id, token.Id);
            WorkflowInstance child = store.GetInstance(childId);

            if (child != null && child.Status == WorkflowStatus.Completed)
            {
                return CompleteCall(instance, definition, token, node, child);
            }

            if (child != null && (child.Status == WorkflowStatus.Faulted || child.Status == WorkflowStatus.Cancelled))
            {
                Fault(instance,
                    $"Sub-workflow '{child.DefinitionId}' ({childId}) {child.Status.ToString().ToLowerInvariant()}: " +
                    $"{child.FaultMessage}", node.Id);
                return false;
            }

            if (child == null)
            {
                IDictionary<string, object> childVars;
                try
                {
                    childVars = ResolveInputs(instance, node.Inputs, node.Id);
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
            ApplyCallOutputs(instance, node, child.Variables);
            token.WaitingForChildInstanceId = null;
            token.Status = TokenStatus.Active;
            instance.Log("SubworkflowCompleted", node.Id, child.DefinitionId);
            return MoveAlongSingleOutgoing(instance, definition, token);
        }

        /// <summary>Bildet die End-Variablen des Subworkflows ueber die Output-Bindungen auf Eltern-Variablen ab.</summary>
        private static void ApplyCallOutputs(WorkflowInstance parent, CallWorkflowNode node,
            IDictionary<string, object> childVariables)
        {
            if (node.Outputs == null)
            {
                return;
            }

            foreach (ActivityOutputBinding binding in node.Outputs)
            {
                if (binding == null || string.IsNullOrEmpty(binding.Parameter)
                    || string.IsNullOrEmpty(binding.Variable))
                {
                    continue;
                }

                childVariables.TryGetValue(binding.Parameter, out object value);
                parent.Variables[binding.Variable] = value;
            }
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
                if (child.Status == WorkflowStatus.Completed
                    && parentDef.GetNode(token.NodeId) is CallWorkflowNode node)
                {
                    CompleteCall(parent, parentDef, token, node, child);
                }
                else if (child.Status == WorkflowStatus.Completed)
                {
                    Fault(parent,
                        $"Waiting call node '{token.NodeId}' no longer exists for completed sub-workflow " +
                        $"'{childInstanceId}'.", token.NodeId);
                }
                else
                {
                    Fault(parent,
                        $"Sub-workflow '{child.DefinitionId}' ({childInstanceId}) " +
                        $"{child.Status.ToString().ToLowerInvariant()}: {child.FaultMessage}", token.NodeId);
                }

                if (parent.Status != WorkflowStatus.Faulted && parent.Status != WorkflowStatus.Cancelled)
                {
                    UpdateTerminalStatus(parent);
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

        /// <summary>Deterministische Id der Kind-Instanz je aufrufendem (Eltern-)Token - macht das Anlegen idempotent.</summary>
        private static string ChildInstanceId(string parentInstanceId, string tokenId)
            => $"{parentInstanceId}:{tokenId}";

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
                    matched = evaluator.EvaluateCondition(flow.Condition, instance.Variables);
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
                object due = evaluator.Evaluate(node.DueExpression, instance.Variables);
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
                return SpawnOutgoing(instance, outgoing);
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
                    if (node.Kind != NodeKind.ParallelGateway)
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

                    foreach (Token p in parked.Take(incoming.Count))
                    {
                        p.Status = TokenStatus.Consumed;
                    }

                    instance.Log("ParallelJoin", node.Id, node.Name);
                    if (!SpawnOutgoing(instance, definition.OutgoingFlows(node.Id)))
                    {
                        return firedAny; // SpawnOutgoing hat auf Faulted gesetzt.
                    }

                    firedAny = true;
                    progress = true;
                }
            }

            return firedAny;
        }

        private bool SpawnOutgoing(WorkflowInstance instance, IReadOnlyList<SequenceFlow> outgoing)
        {
            foreach (SequenceFlow flow in outgoing)
            {
                if (flow.TargetId == null)
                {
                    Fault(instance, $"Flow '{flow.Id}' has no target.");
                    return false;
                }
            }

            foreach (SequenceFlow flow in outgoing)
            {
                instance.Tokens.Add(new Token { NodeId = flow.TargetId, Status = TokenStatus.Active });
            }

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

            token.NodeId = flow.TargetId;
            token.Status = TokenStatus.Active;
            return true;
        }

        private static void UpdateTerminalStatus(WorkflowInstance instance)
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

            private static Token CopyToken(Token t) => new Token
            {
                Id = t.Id, NodeId = t.NodeId, Status = t.Status, WaitingSignal = t.WaitingSignal,
                DueUtc = t.DueUtc, WaitingTarget = t.WaitingTarget,
                WaitingForChildInstanceId = t.WaitingForChildInstanceId
            };

            private static bool SameState(Token a, Token b)
                => a.NodeId == b.NodeId && a.Status == b.Status && a.WaitingSignal == b.WaitingSignal
                   && Nullable.Equals(a.DueUtc, b.DueUtc) && a.WaitingTarget == b.WaitingTarget
                   && a.WaitingForChildInstanceId == b.WaitingForChildInstanceId;
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
                            WaitingForChildInstanceId = t.WaitingForChildInstanceId
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
