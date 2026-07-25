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

        private readonly IWorkflowStore store;
        private readonly IActivityHost activities;
        private readonly IExpressionEvaluator evaluator;

        /// <summary>
        /// Initialisiert die Engine.
        /// </summary>
        /// <param name="store">der Persistenz-Store</param>
        /// <param name="activities">der Host, der je Vortrieb einen Aktivitaets-Scope vergibt</param>
        /// <param name="evaluator">der Ausdrucks-Auswerter, oder null fuer den CScript-Standard</param>
        public WorkflowEngine(IWorkflowStore store, IActivityHost activities,
            IExpressionEvaluator evaluator = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.activities = activities ?? throw new ArgumentNullException(nameof(activities));
            this.evaluator = evaluator ?? new CScriptExpressionEvaluator();
            Runtime = new WorkflowRuntimeContext();
        }

        /// <summary>
        /// Die geteilte Laufzeit-Umgebung dieser Engine (u.a. das <see cref="InstanceGate"/>). Wird an
        /// mitwirkende Dienste (etwa die Worker der Ausfuehrungsschicht) als Property weitergereicht -
        /// siehe <see cref="IWorkflowRuntimeAware"/>.
        /// </summary>
        public WorkflowRuntimeContext Runtime { get; }

        /// <summary>
        /// Startet eine neue Instanz der angegebenen Definition und treibt sie bis zum ersten
        /// Wartepunkt (oder bis zum Ende) voran.
        /// </summary>
        /// <param name="definitionId">die Id der Definition</param>
        /// <param name="initialVariables">Startvariablen, oder null</param>
        /// <param name="correlationKey">optionaler Korrelationsschluessel fuer Signale</param>
        /// <returns>die gestartete Instanz</returns>
        public WorkflowInstance StartWorkflow(string definitionId,
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

            Advance(instance, definition);
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
            instance.Log("Cancelled");
            store.SaveInstance(instance);
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
            // feuert NICHT - der Fire faellt gleich im serialisierten Commit gegen frischen Stand.
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

                if (store.TryCommitInstance(fresh, baseVersion))
                {
                    // Neu entstandene aktive Tokens (Ids, die es beim Laden noch nicht gab) = neue Zweige.
                    return fresh.ActiveTokens
                        .Where(t => !snapshot.HasToken(t.Id))
                        .Select(t => t.Id)
                        .ToList();
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
                    instance.Log("Entered", node.Id, node.Name);
                    return MoveAlongSingleOutgoing(instance, definition, token);

                case EndNode:
                    token.Status = TokenStatus.Consumed;
                    instance.Log("Ended", node.Id, node.Name);
                    return true;

                case AutomatedActivityNode activity:
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

                case ParallelGatewayNode parallel:
                    return ProcessParallelGateway(instance, definition, token, parallel);

                default:
                    Fault(instance, $"Unsupported node type '{node.GetType().Name}' (node '{node.Id}').");
                    return false;
            }
        }

        private bool RunActivity(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            AutomatedActivityNode node, IActivityScope activityScope)
        {
            instance.Log("Entered", node.Id, node.Name);

            // Datenfluss hinein: die Eingabe-Bindungen des Knotens aufloesen. Ein Fehler hier (z.B. ein
            // ungueltiger Ausdruck) hat eine andere Ursache als ein Fehler in der Aktivitaet selbst -
            // deshalb ein eigener Zweig mit eigener, unterscheidbarer Log-/Fault-Meldung.
            IDictionary<string, object> inputs;
            try
            {
                inputs = ResolveInputs(instance, node);
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
            try
            {
                // On-demand aufgeloest; die Lebensdauer der Aktivitaet (bei Plugins: der geladenen
                // Instanz) gehoert dem Scope und endet mit dem Vortrieb.
                IWorkflowActivity activity = activityScope.Resolve(node.ActivityRef);
                activity.Execute(new WorkflowActivityContext(instance, node, inputs, outputs));
            }
            catch (Exception ex)
            {
                // Die Aktivitaet ist gescheitert. Das darf die Instanz nicht stumm verschlucken:
                // der Fehler wird protokolliert (mit Stacktrace) und die Instanz faellt auf Faulted.
                LogEnvironment.LogEvent(
                    $"Activity '{node.ActivityRef}' of node '{node.Id}' in workflow instance '{instance.Id}' failed: " +
                    $"{ex.OutlineException()}", LogSeverity.Error);
                Fault(instance, $"Activity '{node.ActivityRef}' failed: {ex.Message}", node.Id);
                return false;
            }

            // Datenfluss heraus: die deklarierten Ausgaben auf die konfigurierten Variablen abbilden.
            ApplyOutputs(instance, node, outputs);

            instance.Log("Completed", node.Id, node.Name);
            return MoveAlongSingleOutgoing(instance, definition, token);
        }

        /// <summary>
        /// Loest die Eingabe-Bindungen eines Aktivitaets-Knotens gegen den aktuellen Instanzzustand
        /// auf. Eine fehlende Variable (bei <see cref="ParameterBindingKind.Variable"/>) ist ein
        /// definierter Normalfall (der Wert ist dann null) - kein Fehler, aber protokolliert, damit er
        /// nachvollziehbar bleibt. Ein Ausdrucksfehler wird an den Aufrufer geworfen (der faultet).
        /// </summary>
        private IDictionary<string, object> ResolveInputs(WorkflowInstance instance, AutomatedActivityNode node)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (node.Inputs == null)
            {
                return result;
            }

            foreach (ActivityInputBinding binding in node.Inputs)
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
                                $"Input '{binding.Parameter}' of node '{node.Id}' in instance '{instance.Id}' " +
                                $"is bound to variable '{binding.Source}', which is not set - resolved to null.",
                                LogSeverity.Report);
                        }

                        break;

                    case ParameterBindingKind.Expression:
                        result[binding.Parameter] = evaluator.Evaluate(binding.Source, instance.Variables);
                        break;

                    default:
                        LogEnvironment.LogEvent(
                            $"Input '{binding.Parameter}' of node '{node.Id}' uses an unsupported binding " +
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

        private bool RouteExclusive(WorkflowInstance instance, WorkflowDefinition definition, Token token,
            ExclusiveGatewayNode gateway)
        {
            instance.Log("Entered", gateway.Id, gateway.Name);
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
                instance.Log(outgoing.Count > 1 ? "ParallelSplit" : "Entered", node.Id, node.Name);
                return SpawnOutgoing(instance, outgoing);
            }

            // Join: dieses Token kommt an und parkt als Joining. Die Fire-Entscheidung faellt bewusst
            // NICHT hier, sondern zentral in ResolveJoins - im sequenziellen Fall, nachdem alle Zweige
            // geparkt sind; im nebenlaeufigen Fall im serialisierten Commit gegen frischen Stand (atomar).
            token.Status = TokenStatus.Joining;
            instance.Log("Joining", node.Id, node.Name);
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
            else if (instance.Tokens.Any(t => t.Status == TokenStatus.Waiting))
            {
                // Ein wartender Zweig kann noch an einen offenen Join liefern - die Instanz ruht.
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

        private static void Fault(WorkflowInstance instance, string message, string nodeId = null)
        {
            instance.Status = WorkflowStatus.Faulted;
            instance.FaultMessage = message;
            instance.Log("Faulted", nodeId, message);
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
                Id = t.Id, NodeId = t.NodeId, Status = t.Status, WaitingSignal = t.WaitingSignal, DueUtc = t.DueUtc
            };

            private static bool SameState(Token a, Token b)
                => a.NodeId == b.NodeId && a.Status == b.Status && a.WaitingSignal == b.WaitingSignal
                   && Nullable.Equals(a.DueUtc, b.DueUtc);
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
                            WaitingSignal = t.WaitingSignal, DueUtc = t.DueUtc
                        });
                    }
                    else
                    {
                        existing.NodeId = t.NodeId;
                        existing.Status = t.Status;
                        existing.WaitingSignal = t.WaitingSignal;
                        existing.DueUtc = t.DueUtc;
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
