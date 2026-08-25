using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Scheduling;
using ITVComponents.Workflow.Model;

namespace ITVComponents.Workflow.Validation
{
    /// <summary>Der Schweregrad eines Validierungsbefunds.</summary>
    public enum ValidationSeverity
    {
        /// <summary>Verhindert ein sinnvolles Speichern/Ausfuehren.</summary>
        Error,

        /// <summary>Auffaellig, aber nicht zwingend falsch.</summary>
        Warning
    }

    /// <summary>Ein einzelner Befund der Definition-Pruefung.</summary>
    public sealed class ValidationIssue
    {
        /// <summary>Der Schweregrad.</summary>
        public ValidationSeverity Severity { get; init; }

        /// <summary>Der betroffene Knoten bzw. die betroffene Kante, oder null (definitionsweit).</summary>
        public string NodeId { get; init; }

        /// <summary>Die Meldung.</summary>
        public string Message { get; init; } = "";
    }

    /// <summary>
    /// Prueft eine <see cref="WorkflowDefinition"/> auf statische Fehler, bevor sie gespeichert oder
    /// ausgefuehrt wird - damit Fehler im Editor sichtbar werden und nicht erst zur Laufzeit als
    /// <c>Faulted</c> auffallen. Bewusst reine, blazor-freie Berechnung, damit sie testbar und auch
    /// ausserhalb des Editors (z.B. im Design-Handler) nutzbar ist.
    /// </summary>
    public static class WorkflowDefinitionValidator
    {
        /// <summary>Prueft die Definition und liefert alle Befunde (Fehler zuerst).</summary>
        public static IReadOnlyList<ValidationIssue> Validate(WorkflowDefinition definition)
        {
            var issues = new List<ValidationIssue>();
            if (definition == null)
            {
                issues.Add(Error(null, "Definition is null."));
                return issues;
            }

            List<WorkflowNode> nodes = definition.Nodes ?? new List<WorkflowNode>();
            List<SequenceFlow> flows = definition.Flows ?? new List<SequenceFlow>();

            // Knoten-Ids: vorhanden und eindeutig.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorkflowNode n in nodes)
            {
                if (string.IsNullOrWhiteSpace(n?.Id))
                {
                    issues.Add(Error(null, "A node has no id."));
                    continue;
                }

                if (!seen.Add(n.Id))
                {
                    issues.Add(Error(n.Id, $"Duplicate node id '{n.Id}'."));
                }
            }

            var byId = nodes.Where(n => !string.IsNullOrWhiteSpace(n?.Id))
                .GroupBy(n => n.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            // Genau EIN Start und EIN Ende: nur so hat die Definition eine eindeutige Signatur (Start-
            // Parameter) und ein eindeutiges Ergebnis (End-Mapping). Mehrere Start-Knoten waren bisher ein
            // impliziter Parallelstart - das leistet ein AND-Split hinter dem einen Start, und zwar sichtbar.
            // Die Regel gilt JE EBENE: die Definition selbst und jeder eingebettete Abschnitt haben je
            // genau einen Start und ein Ende. Ohne die Trennung nach Behaelter meldete der erste
            // Subprozess sofort "zwei Start-Knoten".
            List<WorkflowNode> starts = nodes.Where(n => n?.Kind == NodeKind.Start && n.ParentNodeId == null)
                .ToList();
            List<WorkflowNode> ends = nodes.Where(n => n?.Kind == NodeKind.End && n.ParentNodeId == null)
                .ToList();

            issues.AddRange(ContainerIssues(starts, ends, null, byId));

            foreach (WorkflowNode container in nodes.Where(n => n is SubProcessNode))
            {
                issues.AddRange(ContainerIssues(
                    nodes.Where(n => n?.Kind == NodeKind.Start && n.ParentNodeId == container.Id).ToList(),
                    nodes.Where(n => n?.Kind == NodeKind.End && n.ParentNodeId == container.Id).ToList(),
                    container, byId));
            }

            // Signatur und Ergebnis sind Eigenschaften der DEFINITION, nicht eines Knotens: sie duerfen
            // deshalb nur an einer Stelle stehen. Mehrfach deklariert waere zur Laufzeit mehrdeutig (die
            // Engine waehlt dann deterministisch, aber willkuerlich, die kleinste Id). Seit der
            // Ein-Start/Ein-End-Regel kann das strukturell nicht mehr entstehen - die Pruefung bleibt als
            // die praezisere Meldung stehen (und als Netz, falls die Regel je gelockert wird), meldet aber
            // nur, was oben nicht ohnehin schon gemeldet wurde.
            List<StartNode> declaringStarts = nodes.OfType<StartNode>()
                .Where(s => s.Inputs is { Count: > 0 }).ToList();
            if (declaringStarts.Count > 1 && starts.Count <= 1)
            {
                issues.Add(Error(null,
                    "Start parameters are declared on more than one start node " +
                    $"({string.Join(", ", declaringStarts.Select(s => $"'{Label(s)}'"))}) - the workflow " +
                    "signature would be ambiguous. Declare them on exactly one start node."));
            }

            List<EndNode> declaringEnds = nodes.OfType<EndNode>()
                .Where(e => e.Outputs is { Count: > 0 }).ToList();
            if (declaringEnds.Count > 1 && ends.Count <= 1)
            {
                issues.Add(Error(null,
                    $"A workflow result is declared on more than one end node " +
                    $"({string.Join(", ", declaringEnds.Select(e => $"'{Label(e)}'"))}) - the result would " +
                    "depend on which end is reached. Declare it on exactly one end node."));
            }

            // Kanten muessen existierende Knoten referenzieren.
            foreach (SequenceFlow f in flows)
            {
                if (f.SourceId == null || !byId.ContainsKey(f.SourceId))
                {
                    issues.Add(Error(f.Id, $"Connection '{f.Id}' has an unknown source node."));
                }

                if (f.TargetId == null || !byId.ContainsKey(f.TargetId))
                {
                    issues.Add(Error(f.Id, $"Connection '{f.Id}' has an unknown target node."));
                }
            }

            Dictionary<string, int> outCount = flows.Where(f => f.SourceId != null)
                .GroupBy(f => f.SourceId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            Dictionary<string, int> inCount = flows.Where(f => f.TargetId != null)
                .GroupBy(f => f.TargetId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

            foreach (WorkflowNode n in nodes)
            {
                if (string.IsNullOrWhiteSpace(n?.Id))
                {
                    continue;
                }

                int outs = outCount.TryGetValue(n.Id, out int o) ? o : 0;
                int ins = inCount.TryGetValue(n.Id, out int i) ? i : 0;

                // Nebenpfad-Ende und Abbruch verbrauchen ihr Token wie das End - der eine ohne den
                // Workflow zu beenden, der andere indem er ihn ganz beendet.
                bool terminatesBranch = n.Kind is NodeKind.End or NodeKind.SidePathEnd or NodeKind.TerminateEnd;
                if (!terminatesBranch && outs == 0)
                {
                    issues.Add(Error(n.Id, $"Node '{Label(n)}' has no outgoing connection - a token would get stuck."));
                }

                if (terminatesBranch && outs > 0)
                {
                    issues.Add(Warn(n.Id, $"End node '{Label(n)}' has outgoing connections - they are ignored."));
                }

                // Ein Fristen-Timer und ein Rueckabwicklungs-Pfad haengen an ihrem Schritt, statt
                // angeflossen zu werden - sie haben bewusst keine eingehende Kante.
                if (n.Kind != NodeKind.Start && n.Kind != NodeKind.BoundaryTimer
                    && n.Kind != NodeKind.Compensation && ins == 0)
                {
                    issues.Add(Warn(n.Id, $"Node '{Label(n)}' has no incoming connection - it is unreachable."));
                }

                if (n.Kind == NodeKind.BoundaryTimer && ins > 0)
                {
                    issues.Add(Error(n.Id,
                        $"Boundary timer '{Label(n)}' has an incoming connection - it is armed by the step it " +
                        "is attached to, not reached by a connection."));
                }

                if (n.Kind == NodeKind.Start && ins > 0)
                {
                    issues.Add(Warn(n.Id, $"Start node '{Label(n)}' has incoming connections."));
                }

                switch (n)
                {
                    case AutomatedActivityNode act when string.IsNullOrWhiteSpace(act.ActivityRef):
                        issues.Add(Error(n.Id, $"Activity '{Label(n)}' has no activity reference."));
                        break;
                    case WaitNode w when string.IsNullOrWhiteSpace(w.SignalName):
                        issues.Add(Error(n.Id, $"Wait node '{Label(n)}' has no signal name."));
                        break;
                    case SendMessageNode sm when string.IsNullOrWhiteSpace(sm.SignalName):
                        issues.Add(Error(n.Id, $"Send node '{Label(n)}' has no signal name."));
                        break;
                    case SendMessageNode sm2 when sm2.WaitKind == WaitKind.Message
                                                  && string.IsNullOrWhiteSpace(sm2.CorrelationExpression):
                        // Ohne Schluessel gibt es keine gerichtete Zustellung - die Nachricht wuerde zum
                        // Rundruf und jeden gleichnamigen Wartepunkt wecken. Als Fehler und nicht als
                        // Warnung: im Bild sieht man dem Knoten das nicht an.
                        issues.Add(Error(n.Id,
                            $"Send node '{Label(n)}' sends a message but has no correlation - it would " +
                            "degrade to a broadcast and wake every wait point with that name. Give it a " +
                            "correlation, or switch it to broadcast to say so."));
                        break;
                    case SendMessageNode when outs > 1:
                        issues.Add(Error(n.Id,
                            $"Send node '{Label(n)}' has {outs} outgoing connections - it needs exactly " +
                            "one. Sending does not split the flow."));
                        break;
                    case TimerNode t when string.IsNullOrWhiteSpace(t.DueExpression):
                        issues.Add(Error(n.Id, $"Timer node '{Label(n)}' has no due expression."));
                        break;
                    case ExclusiveGatewayNode x when outs > 0 && string.IsNullOrWhiteSpace(x.DefaultFlowId):
                        issues.Add(Warn(n.Id,
                            $"Exclusive gateway '{Label(n)}' has no default flow - the instance faults if no condition matches."));
                        break;

                    // Namenlose Bindungen ueberspringt die Engine still - hier sichtbar machen, statt den
                    // Nutzer raten zu lassen, warum sein Parameter nicht ankommt. Kein Fehler: eine halb
                    // getippte Zeile soll das Speichern nicht blockieren.
                    case StartNode st when st.Inputs != null
                                           && st.Inputs.Any(b => b == null || string.IsNullOrWhiteSpace(b.Parameter)):
                        issues.Add(Warn(n.Id,
                            $"Start node '{Label(n)}' has a parameter without a name - it is ignored."));
                        break;
                    case StartNode st2 when st2.ScopeMode == ActivityScopeMode.Replace
                                            && (st2.Inputs == null || st2.Inputs.Count == 0):
                        // Ohne deklarierte Parameter greift die Signatur gar nicht - der Schalter waere
                        // sonst ein stiller Nicht-Effekt (und NICHT "die Instanz startet leer").
                        issues.Add(Warn(n.Id,
                            $"Start node '{Label(n)}' is marked as a strict signature but declares no " +
                            "parameters - the setting has no effect."));
                        break;
                    case EndNode en when en.Outputs != null
                                         && en.Outputs.Any(b => b == null || string.IsNullOrWhiteSpace(b.Parameter)
                                                                || string.IsNullOrWhiteSpace(b.Variable)):
                        issues.Add(Warn(n.Id,
                            $"End node '{Label(n)}' has a result mapping without a source variable or a result " +
                            "name - it is ignored."));
                        break;
                }

                if (n is AutomatedActivityNode iterating && iterating.Iteration != null)
                {
                    issues.AddRange(IterationIssues(iterating));
                }

                if (n is EventGatewayNode)
                {
                    issues.AddRange(EventGatewayIssues(n, flows, byId, outs));
                }

                if (n is InclusiveGatewayNode inclusive)
                {
                    issues.AddRange(InclusiveGatewayIssues(inclusive, definition, flows, ins, outs));
                }

                if (n is CompensationNode handler)
                {
                    issues.AddRange(CompensationIssues(handler, byId, outs, ins));
                }

                if (n is CompensateNode compensate && !string.IsNullOrWhiteSpace(compensate.TargetNodeId)
                    && !byId.ContainsKey(compensate.TargetNodeId))
                {
                    issues.Add(Error(n.Id,
                        $"Compensate node '{Label(n)}' targets '{compensate.TargetNodeId}', which does not " +
                        "exist."));
                }

                if (n is UserActivityNode task)
                {
                    issues.AddRange(UserTaskIssues(task, outs));
                }

                if (n is StartNode startNode)
                {
                    issues.AddRange(StartFormIssues(startNode));
                    issues.AddRange(StartTriggerIssues(startNode));
                }

                if (n is BoundaryTimerNode boundary)
                {
                    issues.AddRange(BoundaryTimerIssues(boundary, byId, flows, ends));
                }

                if (n is BoundaryMessageNode boundaryMessage)
                {
                    issues.AddRange(BoundaryMessageIssues(boundaryMessage, byId, flows, ends));
                }

                // Fehler-Ausgang (Aktivitaet ODER Subworkflow-Aufruf): die Fehler-Kante muss eine der
                // ausgehenden Kanten sein, und es muss GENAU eine weitere (Erfolgs-)Kante geben.
                string errorFlowId = n switch
                {
                    AutomatedActivityNode a => a.ErrorFlowId,
                    CallWorkflowNode c => c.ErrorFlowId,
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(errorFlowId))
                {
                    List<SequenceFlow> outFlows = flows.Where(f => f.SourceId == n.Id).ToList();
                    if (outFlows.All(f => f.Id != errorFlowId))
                    {
                        issues.Add(Error(n.Id,
                            $"Node '{Label(n)}' error flow '{errorFlowId}' is not one of its outgoing connections."));
                    }
                    else if (outFlows.Count(f => f.Id != errorFlowId) != 1)
                    {
                        issues.Add(Error(n.Id,
                            $"Node '{Label(n)}' with an error flow must have exactly one success connection."));
                    }
                }
            }

            // Das Mapping der Kante schreibt in denselben Variablen-Stack wie ein Knoten - also dieselben
            // Pruefungen: eine namenlose Bindung ueberspringt die Engine still.
            foreach (SequenceFlow f in flows)
            {
                if (f?.Inputs != null
                    && f.Inputs.Any(b => b == null || string.IsNullOrWhiteSpace(b.Parameter)))
                {
                    issues.Add(Warn(f.Id,
                        $"Connection {FlowLabel(f, byId)} has a mapping without a variable name - it is ignored."));
                }
            }

            // Ein Zweig, der ins Ende laeuft statt in seinen Join, geht mit seinem Zweig-Scope verloren
            // (die Laufzeit meldet das ebenfalls) - und die uebrigen Zweige haengen dann ewig am Join.
            // Warnung statt Fehler: die Regionen-Analyse ist bei Zyklen/Retry-Schleifen nicht eindeutig.
            HashSet<string> parallelRegion = NodesInsideParallelRegion(nodes, flows, byId, inCount, outCount);
            foreach (WorkflowNode n in nodes)
            {
                if (n is EndNode && !string.IsNullOrWhiteSpace(n.Id) && parallelRegion.Contains(n.Id))
                {
                    issues.Add(Warn(n.Id,
                        $"End node '{Label(n)}' is inside a parallel region - a branch ending here never passes " +
                        "its join: its branch variables are dropped and the other branches wait forever. " +
                        "Route every branch through the join."));
                }
            }

            // Seit den Zweig-Scopes ist ein paralleler Schreibzugriff kein Laufzeitfehler mehr (jeder Zweig
            // arbeitet in seiner Kopie), aber der Join muss sich beim Zusammenfuehren fuer einen Wert
            // entscheiden - das gehoert modelliert, nicht dem Zufall der Zweig-Reihenfolge ueberlassen.
            issues.AddRange(ParallelWriteConflicts(nodes, flows, byId, inCount, outCount));

            // Die Aufbewahrungsfristen haengen an keinem Knoten - und ihre Fehler faellt sonst niemandem
            // auf: die Regel schweigt bei Unsinn, statt zu werfen, und was sie verwirft, verschwindet
            // wortlos. Hier ist die Stelle, an der es der Autor erfaehrt.
            issues.AddRange(RetentionIssues(definition));

            // Fehler zuerst, dann Warnungen - stabile Reihenfolge fuer die Anzeige.
            return issues.OrderBy(x => x.Severity).ToList();
        }

        /// <summary>
        /// Prueft die Aufbewahrungs-Einstellungen der Definition.
        /// </summary>
        /// <remarks>
        /// <b>Warum das hier stehen MUSS:</b> die Aufbewahrungsregel wirft bei keiner dieser Eingaben.
        /// Eine negative Frist wird verworfen (und die naechste Stufe der Kette gilt), ein
        /// widerspruechlicher Rahmen wird ganz ignoriert, ein Rahmen ohne Erlaubnis kommt nie zum Zug.
        /// Das ist zur Laufzeit richtig - schweigend richtig. Ohne diese Meldungen erfaehrt der Autor
        /// seinen Tippfehler nie und wundert sich Monate spaeter, warum eine Frist nicht gilt.
        /// </remarks>
        private static IEnumerable<ValidationIssue> RetentionIssues(WorkflowDefinition definition)
        {
            var issues = new List<ValidationIssue>();

            void CheckDays(int? days, string what)
            {
                if (days is < 0)
                {
                    // Fehler und nicht Warnung: der Wert wird nicht etwa auf 0 gerundet, er zaehlt gar
                    // nicht - es gilt still die naechste Stufe der Kette.
                    issues.Add(Error(null,
                        $"{what} is negative ({days}). A negative period would put the cut-off date in "
                        + "the FUTURE and sweep away processes that have not even ended. It is discarded, "
                        + "and the next stage of the chain applies instead. Use 0 for "
                        + "\"right after it ends\"."));
                }
            }

            CheckDays(definition.RetentionDays, "The archive period");
            CheckDays(definition.AttachmentRetentionDays, "The attachment period");
            CheckDays(definition.MinTenantRetentionDays, "The lower bound for the archive period");
            CheckDays(definition.MaxTenantRetentionDays, "The upper bound for the archive period");
            CheckDays(definition.MinTenantAttachmentRetentionDays,
                "The lower bound for the attachment period");
            CheckDays(definition.MaxTenantAttachmentRetentionDays,
                "The upper bound for the attachment period");

            void CheckRange(int? min, int? max, string what)
            {
                if (min is >= 0 && max is >= 0 && min.Value > max.Value)
                {
                    issues.Add(Error(null,
                        $"The bounds for {what} contradict each other (at least {min}, at most {max}). "
                        + "Which one would win is anybody's guess, so NEITHER applies - a tenant's wish "
                        + "then stands unchanged, however far outside it lies."));
                }
            }

            CheckRange(definition.MinTenantRetentionDays, definition.MaxTenantRetentionDays,
                "the archive period");
            CheckRange(definition.MinTenantAttachmentRetentionDays,
                definition.MaxTenantAttachmentRetentionDays, "the attachment period");

            bool hasBounds = definition.MinTenantRetentionDays != null
                             || definition.MaxTenantRetentionDays != null
                             || definition.MinTenantAttachmentRetentionDays != null
                             || definition.MaxTenantAttachmentRetentionDays != null;
            if (hasBounds && !definition.AllowTenantRetentionOverride)
            {
                // Warnung, kein Fehler: es ist nicht falsch, nur wirkungslos - und wer den Schalter
                // spaeter umlegt, hat den Rahmen dann sofort richtig stehen.
                issues.Add(Warn(null,
                    "Bounds for the tenants' retention periods are set, but tenants are not allowed to "
                    + "set one at all (AllowTenantRetentionOverride is off) - the bounds have nothing to "
                    + "limit and do nothing."));
            }

            return issues;
        }

        /// <summary>
        /// Prueft „genau ein Start und ein Ende" fuer EINE Ebene - die Definition selbst
        /// (<paramref name="container"/> null) oder einen eingebetteten Abschnitt.
        /// </summary>
        /// <remarks>
        /// Der Grund ist auf beiden Ebenen derselbe: nur so hat die Ebene eine eindeutige Signatur und ein
        /// eindeutiges Ergebnis. Mehrere Start-Knoten waeren ein impliziter Parallelstart - das leistet ein
        /// AND-Split dahinter, und zwar sichtbar.
        /// </remarks>
        private static IEnumerable<ValidationIssue> ContainerIssues(List<WorkflowNode> starts,
            List<WorkflowNode> ends, WorkflowNode container, Dictionary<string, WorkflowNode> byId)
        {
            var issues = new List<ValidationIssue>();
            string where = container == null ? "A workflow" : $"Sub-process '{Label(container)}'";
            string scopeId = container?.Id;

            if (starts.Count == 0)
            {
                issues.Add(Error(scopeId, container == null
                    ? "No start node."
                    : $"{where} has no start node inside it - nothing could begin there."));
            }
            else if (starts.Count > 1)
            {
                issues.Add(Error(scopeId,
                    $"{where} has exactly one start node, but this one has {starts.Count} " +
                    $"({string.Join(", ", starts.Select(s => $"'{Label(s)}'"))}) - each of them would get a " +
                    "token (implicit parallel start) and the signature would be ambiguous. Keep one " +
                    "start node and put an AND split behind it."));
            }

            if (ends.Count == 0)
            {
                // Bewusst nur eine Warnung: eine Definition ohne Ende ist im Bau der Normalfall und soll
                // sich speichern lassen.
                issues.Add(Warn(scopeId, container == null
                    ? "No end node - the workflow can never complete."
                    : $"{where} has no end node - the section could never finish."));
            }
            else if (ends.Count > 1)
            {
                issues.Add(Error(scopeId,
                    $"{where} has exactly one end node, but this one has {ends.Count} " +
                    $"({string.Join(", ", ends.Select(e => $"'{Label(e)}'"))}) - the result " +
                    "would depend on which end is reached. Merge them into one end node."));
            }

            return issues;
        }

        /// <summary>
        /// Prueft einen Rueckabwicklungs-Pfad: er muss an einem Schritt haengen, der ueberhaupt etwas
        /// bewirkt, und genau einen Ausgang haben.
        /// </summary>
        private static IEnumerable<ValidationIssue> CompensationIssues(CompensationNode node,
            Dictionary<string, WorkflowNode> byId, int outgoing, int incoming)
        {
            var issues = new List<ValidationIssue>();

            if (string.IsNullOrWhiteSpace(node.AttachedToNodeId))
            {
                issues.Add(Error(node.Id,
                    $"Compensation handler '{Label(node)}' is not attached to a step - it would never be " +
                    "triggered."));
            }
            else if (!byId.TryGetValue(node.AttachedToNodeId, out WorkflowNode host))
            {
                issues.Add(Error(node.Id,
                    $"Compensation handler '{Label(node)}' is attached to '{node.AttachedToNodeId}', which " +
                    "does not exist."));
            }
            else if (!CompensationNode.CanCompensate(host))
            {
                // Ein Wartepunkt oder ein Gateway hinterlaesst nichts, was zurueckzunehmen waere - ein
                // Pfad dort waere ein stiller Nicht-Effekt.
                issues.Add(Error(node.Id,
                    $"Compensation handler '{Label(node)}' hangs on '{Label(host)}', which does not do any " +
                    "work that could be undone. Attach it to an activity, a user task, a sub-process or a " +
                    "subworkflow call."));
            }

            if (incoming > 0)
            {
                issues.Add(Error(node.Id,
                    $"Compensation handler '{Label(node)}' has an incoming connection - it is triggered by a " +
                    "compensate node, not reached by a connection."));
            }

            if (outgoing != 1)
            {
                issues.Add(Error(node.Id,
                    $"Compensation handler '{Label(node)}' has {outgoing} outgoing connections - it needs " +
                    "exactly one, leading to the steps that undo the work."));
            }

            return issues;
        }

        /// <summary>
        /// Prueft ein inklusives Gateway (OR). Die Regel, die zaehlt: Split und Join muessen ein
        /// <b>Paar</b> bilden - jeder Zweig des Splits muss seinen Join auch erreichen.
        /// </summary>
        /// <remarks>
        /// Das ist der Preis des strukturierten OR, und er wird genau hier bezahlt. Der Join zaehlt gegen
        /// die Zahl, die sein Split angemeldet hat; ein Zweig, der den Join nie erreicht (weil er
        /// woanders endet oder aus der Region herausfuehrt), laesst diese Zahl nie voll werden - die
        /// Instanz haenge fuer immer, ohne dass irgendwo ein Fehler stuende. Deshalb hier, beim
        /// Zeichnen, und als Fehler.
        /// </remarks>
        private static IEnumerable<ValidationIssue> InclusiveGatewayIssues(InclusiveGatewayNode node,
            WorkflowDefinition definition, List<SequenceFlow> flows, int incoming, int outgoing)
        {
            var issues = new List<ValidationIssue>();

            if (incoming > 1 && outgoing > 1)
            {
                // Beim AND ist "zwei rein, zwei raus" zulaessig (Join und Split in einem). Beim OR nicht:
                // der Knoten muesste gleichzeitig zaehlen und anmelden, und beides mit derselben Id.
                issues.Add(Error(node.Id,
                    $"Inclusive gateway '{Label(node)}' is a join and a split at the same time - split the " +
                    "two into separate gateways, otherwise the branch count is ambiguous."));
                return issues;
            }

            if (incoming > 1)
            {
                bool fedBySplit = definition.Nodes.OfType<InclusiveGatewayNode>().Any(split =>
                    split.Id != node.Id
                    && flows.Count(f => f.SourceId == split.Id) == incoming
                    && Reaches(flows, split.Id, node.Id));
                if (!fedBySplit)
                {
                    issues.Add(Error(node.Id,
                        $"Inclusive join '{Label(node)}' has no matching inclusive split upstream (one with " +
                        $"{incoming} outgoing connections). It waits for a branch count that nobody " +
                        "announces, so it would wait forever."));
                }

                return issues;
            }

            if (outgoing <= 1)
            {
                return issues; // Durchreiche - nichts zu paaren.
            }

            var branches = flows.Where(f => f.SourceId == node.Id).ToList();
            if (branches.All(f => string.IsNullOrWhiteSpace(f.Condition)))
            {
                issues.Add(Error(node.Id,
                    $"No outgoing connection of inclusive gateway '{Label(node)}' has a condition - every " +
                    "branch would always be taken. That is an AND gateway; use one."));
            }

            if (string.IsNullOrWhiteSpace(node.DefaultFlowId))
            {
                issues.Add(Warn(node.Id,
                    $"Inclusive gateway '{Label(node)}' has no default flow - the instance faults if no " +
                    "condition matches."));
            }

            // Der Join, auf den dieser Split zielt: der einzige inklusive Join mit passender Zahl von
            // Eingaengen, den ALLE Zweige erreichen.
            List<InclusiveGatewayNode> joins = definition.Nodes.OfType<InclusiveGatewayNode>()
                .Where(j => j.Id != node.Id && flows.Count(f => f.TargetId == j.Id) == outgoing
                            && branches.All(b => b.TargetId != null && Reaches(flows, b.TargetId, j.Id)))
                .ToList();
            if (joins.Count == 0)
            {
                issues.Add(Error(node.Id,
                    $"Inclusive gateway '{Label(node)}' has no matching join: there is no inclusive gateway " +
                    $"with {outgoing} incoming connections that EVERY branch reaches. A branch that misses " +
                    "the join makes it wait forever."));
            }

            return issues;
        }

        /// <summary>
        /// Ist <paramref name="targetId"/> von <paramref name="fromId"/> aus ueber Kanten erreichbar
        /// (der Knoten selbst zaehlt als erreicht)? Reine Struktur - Bedingungen spielen keine Rolle.
        /// </summary>
        private static bool Reaches(List<SequenceFlow> flows, string fromId, string targetId)
        {
            if (fromId == null || targetId == null)
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal) { fromId };
            var queue = new Queue<string>();
            queue.Enqueue(fromId);
            while (queue.Count != 0)
            {
                string current = queue.Dequeue();
                if (current == targetId)
                {
                    return true;
                }

                foreach (SequenceFlow flow in flows.Where(f => f.SourceId == current))
                {
                    if (flow.TargetId != null && seen.Add(flow.TargetId))
                    {
                        queue.Enqueue(flow.TargetId);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Prueft ein ereignisbasiertes Gateway. Die eine Regel, die zaehlt: hinter jedem Ausgang muss
        /// etwas stehen, das auch wirklich <b>wartet</b>.
        /// </summary>
        /// <remarks>
        /// Steht dort eine automatische Aktivitaet, laeuft sie sofort durch und gewinnt jedes Rennen - das
        /// Gateway waere ein stiller Nicht-Effekt, und im Diagramm saehe man ihm das nicht an. Deshalb ein
        /// Fehler und keine Warnung.
        /// </remarks>
        private static IEnumerable<ValidationIssue> EventGatewayIssues(WorkflowNode node,
            List<SequenceFlow> flows, Dictionary<string, WorkflowNode> byId, int outgoing)
        {
            var issues = new List<ValidationIssue>();
            if (outgoing < 2)
            {
                issues.Add(Error(node.Id,
                    $"Event gateway '{Label(node)}' has {outgoing} outgoing connection(s) - it needs at least " +
                    "two events to race."));
            }

            foreach (SequenceFlow flow in flows.Where(f => f.SourceId == node.Id))
            {
                if (flow.TargetId == null || !byId.TryGetValue(flow.TargetId, out WorkflowNode target))
                {
                    continue;
                }

                if (!EventGatewayNode.CanRace(target))
                {
                    issues.Add(Error(node.Id,
                        $"Event gateway '{Label(node)}' leads to '{Label(target)}', which does not wait for an " +
                        "event. Only a wait point, a timer or a user task can take part in the race - anything " +
                        "else would run straight through and always win."));
                }
            }

            return issues;
        }

        /// <summary>
        /// Prueft die Iterations-Einstellung eines Aktivitaets-Knotens. Die Engine faultet zur Laufzeit,
        /// wenn die Sammlung nicht gebunden ist - das gehoert in den Editor, nicht in eine Instanz, die
        /// erst am Knoten stirbt.
        /// </summary>
        private static IEnumerable<ValidationIssue> IterationIssues(AutomatedActivityNode node)
        {
            var issues = new List<ValidationIssue>();
            ActivityIteration it = node.Iteration;
            if (!it.IsConfigured)
            {
                // Ein angelegter, aber leerer Iterations-Block ist ein Nicht-Effekt: die Aktivitaet laeuft
                // genau einmal. Das ist harmlos, sieht im Editor aber nach "ist eingestellt" aus.
                issues.Add(Warn(node.Id,
                    $"Activity '{Label(node)}' has an iteration block without a collection parameter - it is " +
                    "ignored and the activity runs once."));
                return issues;
            }

            if (node.Inputs == null || node.Inputs.All(b => b == null
                                                            || !string.Equals(b.Parameter, it.ItemsInput, StringComparison.Ordinal)))
            {
                issues.Add(Error(node.Id,
                    $"Activity '{Label(node)}' iterates over input parameter '{it.ItemsInput}', but no input " +
                    "binding of that name exists - the instance would fault here."));
            }

            if (!string.IsNullOrWhiteSpace(it.IndexParameter)
                && string.Equals(it.IndexParameter, it.EffectiveItemParameter, StringComparison.Ordinal))
            {
                issues.Add(Error(node.Id,
                    $"Activity '{Label(node)}' passes item and index under the same parameter name " +
                    $"'{it.IndexParameter}' - the index would overwrite the item."));
            }

            if (!string.IsNullOrWhiteSpace(it.CarryOverInput))
            {
                if (node.Inputs == null || node.Inputs.All(b => b == null
                                                                || !string.Equals(b.Parameter, it.CarryOverInput, StringComparison.Ordinal)))
                {
                    issues.Add(Error(node.Id,
                        $"Activity '{Label(node)}' carries over input parameter '{it.CarryOverInput}', but no " +
                        "input binding of that name exists - the instance would fault here."));
                }

                if (string.IsNullOrWhiteSpace(it.SucceededItemsOutput))
                {
                    // Die Uebernahme fliesst ausschliesslich in die Erfolgs-Liste. Ohne sie ist sie ein
                    // stiller Nicht-Effekt - und genau daran scheitert eine Wiederholungs-Schleife lautlos.
                    issues.Add(Warn(node.Id,
                        $"Activity '{Label(node)}' carries over '{it.CarryOverInput}' but declares no output for " +
                        "the succeeded items - the carried-over items would go nowhere."));
                }

                if (string.Equals(it.CarryOverInput, it.ItemsInput, StringComparison.Ordinal))
                {
                    issues.Add(Error(node.Id,
                        $"Activity '{Label(node)}' carries over the very collection it iterates " +
                        $"('{it.ItemsInput}') - already finished items would be processed again."));
                }
            }

            if (!string.IsNullOrWhiteSpace(it.ItemResultOutput)
                && string.IsNullOrWhiteSpace(it.SucceededItemsOutput))
            {
                issues.Add(Warn(node.Id,
                    $"Activity '{Label(node)}' declares '{it.ItemResultOutput}' as the per-item result but has no " +
                    "output for the succeeded items - the setting has no effect."));
            }

            if (it.ContinueOnError && string.IsNullOrWhiteSpace(node.ErrorFlowId))
            {
                // Ohne Fehler-Ausgang faultet die Instanz am Ende trotzdem - dann war das Durchlaufen
                // umsonst, und die gesammelte Fehlerliste sieht niemand.
                issues.Add(Warn(node.Id,
                    $"Activity '{Label(node)}' continues on item errors but has no error flow - a failed item " +
                    "still faults the instance at the end, and the collected failures are never routed."));
            }

            if (!it.ContinueOnError && !string.IsNullOrWhiteSpace(it.FailedItemsOutput)
                && string.IsNullOrWhiteSpace(it.PendingItemsOutput))
            {
                // Der Abbruch laesst Elemente unversucht liegen. Wer nur die GESCHEITERTEN wiederholt,
                // verliert die nie versuchten - lautlos, und erst im Ergebnis zu sehen.
                issues.Add(Warn(node.Id,
                    $"Activity '{Label(node)}' stops at the first failing item, so items may stay unattempted - " +
                    "but only the failed ones are reported. Add an output for the pending items, otherwise a " +
                    "retry silently drops everything that was never tried."));
            }

            if (it.MaxParallel > 1 && !string.IsNullOrWhiteSpace(node.ExecutionTarget))
            {
                // Kein Fehler - nur der Hinweis, dass die Parallelitaet auf DEM Zielhost entsteht.
                issues.Add(Warn(node.Id,
                    $"Activity '{Label(node)}' runs {it.MaxParallel} items in parallel on execution target " +
                    $"'{node.ExecutionTarget}' - the load lands on that host, not on the one that started the branch."));
            }

            return issues;
        }

        /// <summary>
        /// Prueft eine Benutzer-Aufgabe. Der teure Teil ist nicht der Ablauf, sondern das, was der Benutzer
        /// spaeter zu sehen bekommt: eine Aufgabe ohne Art landet in keiner Liste, ein kaputtes Kultur-JSON
        /// steht woertlich in der Oberflaeche.
        /// </summary>
        private static IEnumerable<ValidationIssue> UserTaskIssues(UserActivityNode node, int outgoing)
        {
            var issues = new List<ValidationIssue>();
            if (string.IsNullOrWhiteSpace(node.TaskKey))
            {
                issues.Add(Error(node.Id,
                    $"User task '{Label(node)}' has no task key - it would never show up in a work list."));
            }

            if (outgoing > 1)
            {
                // Die Engine faultet hier zur Laufzeit ("must have exactly one outgoing flow") - besser
                // rot im Editor als eine Instanz, die es bis zur Aufgabe schafft und dann stirbt.
                issues.Add(Error(node.Id,
                    $"User task '{Label(node)}' has {outgoing} outgoing connections - it must have exactly one " +
                    "(use a gateway after it to branch)."));
            }

            // Titel/Beschriftungen sind ENTWEDER Klartext ODER Kultur-JSON. Ist die Absicht erkennbar
            // (beginnt mit '{'), muss es parsebar sein - sonst zeigt die Oberflaeche das JSON woertlich an.
            AddIfBrokenCultureJson(issues, node.Id, node.Title, $"User task '{Label(node)}' title");
            AddIfBrokenCultureJson(issues, node.Id, node.Description, $"User task '{Label(node)}' description");

            if (node.Inputs != null
                && node.Inputs.Any(b => b == null || string.IsNullOrWhiteSpace(b.Parameter)))
            {
                issues.Add(Warn(node.Id,
                    $"User task '{Label(node)}' has an input without a name - it is ignored."));
            }

            if (node.Outputs != null
                && node.Outputs.Any(b => b == null || string.IsNullOrWhiteSpace(b.Parameter)
                                         || string.IsNullOrWhiteSpace(b.Variable)))
            {
                issues.Add(Warn(node.Id,
                    $"User task '{Label(node)}' has a result mapping without a field name or a target variable - " +
                    "it is ignored."));
            }

            issues.AddRange(FormFieldIssues(node.Id, $"User task '{Label(node)}'", node.FormFields,
                readOnlyMeaningful: true));

            return issues;
        }

        /// <summary>
        /// Prueft einen Fristen-Timer am Schritt: woran er haengt, ob er dort ueberhaupt feuern kann und
        /// ob sein Nebenpfad ein Nebenpfad bleibt.
        /// </summary>
        private static List<ValidationIssue> BoundaryTimerIssues(BoundaryTimerNode timer,
            Dictionary<string, WorkflowNode> byId, List<SequenceFlow> flows, List<WorkflowNode> ends)
        {
            var issues = new List<ValidationIssue>();

            if (string.IsNullOrWhiteSpace(timer.AttachedToNodeId))
            {
                issues.Add(Error(timer.Id, $"Boundary timer '{Label(timer)}' is not attached to a step."));
            }
            else if (!byId.TryGetValue(timer.AttachedToNodeId, out WorkflowNode host))
            {
                issues.Add(Error(timer.Id,
                    $"Boundary timer '{Label(timer)}' is attached to unknown step '{timer.AttachedToNodeId}'."));
            }
            else
            {
                // Feuern kann er nur, wo das Token stehen bleibt. Eine gewoehnliche Aktivitaet laeuft
                // synchron durch - dort waere der Timer eine stille Attrappe.
                if (!BoundaryTimerNode.CanHost(host))
                {
                    issues.Add(Warn(timer.Id,
                        $"Boundary timer '{Label(timer)}' is attached to '{Label(host)}', where the token does " +
                        "not park - it can never fire. Attach it to a user task, a subworkflow call or an " +
                        "activity with an execution target."));
                }
            }

            // Die Fristen sind jetzt Ausdruecke: WAS sie liefern, steht erst zur Laufzeit fest (die Engine
            // meldet einen unbrauchbaren Wert dann in Log und Historie). Statisch pruefbar bleibt, DASS es
            // eine gibt und dass keine leer ist.
            IReadOnlyList<BoundaryDeadline> deadlines = timer.EffectiveDeadlines();
            if (deadlines.Count == 0)
            {
                issues.Add(Error(timer.Id,
                    $"Boundary timer '{Label(timer)}' has no deadline - it would never fire."));
            }
            else if (deadlines.Any(d => string.IsNullOrWhiteSpace(d.Expression)))
            {
                issues.Add(Error(timer.Id,
                    $"Boundary timer '{Label(timer)}' has a deadline without an expression."));
            }
            else if (timer.Interrupting && deadlines.Count > 1)
            {
                // Nach dem Unterbrechen steht das Token woanders - eine zweite Frist kaeme nie dran.
                issues.Add(Warn(timer.Id,
                    $"Boundary timer '{Label(timer)}' is interrupting, so only the first deadline is used - " +
                    "the others (and 'repeat last') have no effect."));
            }

            List<SequenceFlow> outgoing = flows.Where(f => f.SourceId == timer.Id).ToList();
            if (outgoing.Count > 1)
            {
                issues.Add(Error(timer.Id,
                    $"Boundary timer '{Label(timer)}' has {outgoing.Count} outgoing connections - it must have " +
                    "exactly one (use a gateway on the side path to branch)."));
            }

            // Der Nebenpfad darf den Workflow nicht beenden: sein Token wird verworfen, sobald der Schritt
            // weiterlaeuft - ein End-Knoten dort wuerde entweder nie erreicht oder wuerde das Ergebnis der
            // ganzen Instanz festschreiben, obwohl nur die Eskalation durchgelaufen ist.
            if (outgoing.Count == 1 && ends.Count > 0)
            {
                var endIds = new HashSet<string>(ends.Select(e => e.Id), StringComparer.Ordinal);
                if (Reaches(outgoing[0].TargetId, endIds, flows))
                {
                    issues.Add(Error(timer.Id,
                        $"The side path of boundary timer '{Label(timer)}' can reach the end node - a side path " +
                        "must not end the workflow. Close it with a side-path end instead."));
                }
            }

            return issues;
        }

        /// <summary>
        /// Prueft einen Nachrichten-Empfang am Schritt - dieselben Fragen wie beim Fristen-Timer, nur dass
        /// statt der Frist der Name der Nachricht zaehlt.
        /// </summary>
        private static List<ValidationIssue> BoundaryMessageIssues(BoundaryMessageNode boundary,
            Dictionary<string, WorkflowNode> byId, List<SequenceFlow> flows, List<WorkflowNode> ends)
        {
            var issues = new List<ValidationIssue>();

            if (string.IsNullOrWhiteSpace(boundary.AttachedToNodeId))
            {
                issues.Add(Error(boundary.Id,
                    $"Boundary message '{Label(boundary)}' is not attached to a step."));
            }
            else if (!byId.TryGetValue(boundary.AttachedToNodeId, out WorkflowNode host))
            {
                issues.Add(Error(boundary.Id,
                    $"Boundary message '{Label(boundary)}' is attached to unknown step "
                    + $"'{boundary.AttachedToNodeId}'."));
            }
            else if (!BoundaryTimerNode.CanHost(host))
            {
                // Dieselbe Regel wie beim Timer, und bewusst dieselbe Quelle: empfangen kann nur, wo das
                // Token stehen bleibt. Eine gewoehnliche Aktivitaet laeuft synchron durch - dort waere der
                // Empfang eine stille Attrappe.
                issues.Add(Warn(boundary.Id,
                    $"Boundary message '{Label(boundary)}' is attached to '{Label(host)}', where the token "
                    + "does not park - it can never fire. Attach it to a user task, a subworkflow call or an "
                    + "activity with an execution target."));
            }

            if (string.IsNullOrWhiteSpace(boundary.SignalName))
            {
                issues.Add(Error(boundary.Id,
                    $"Boundary message '{Label(boundary)}' has no message name - nothing would ever reach it."));
            }

            List<SequenceFlow> outgoing = flows.Where(f => f.SourceId == boundary.Id).ToList();
            if (outgoing.Count > 1)
            {
                issues.Add(Error(boundary.Id,
                    $"Boundary message '{Label(boundary)}' has {outgoing.Count} outgoing connections - it must "
                    + "have exactly one (use a gateway on the side path to branch)."));
            }

            // Wie beim Timer: der Nebenpfad darf den Workflow nicht beenden - sein Token wird verworfen,
            // sobald der Schritt weiterlaeuft.
            if (outgoing.Count == 1 && ends.Count > 0)
            {
                var endIds = new HashSet<string>(ends.Select(e => e.Id), StringComparer.Ordinal);
                if (Reaches(outgoing[0].TargetId, endIds, flows))
                {
                    issues.Add(Error(boundary.Id,
                        $"The side path of boundary message '{Label(boundary)}' can reach the end node - a side "
                        + "path must not end the workflow. Close it with a side-path end instead."));
                }
            }

            return issues;
        }

        /// <summary>Ist einer der Zielknoten von <paramref name="startId"/> aus erreichbar?</summary>
        private static bool Reaches(string startId, HashSet<string> targets, List<SequenceFlow> flows)
        {
            if (startId == null)
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(startId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (!seen.Add(current))
                {
                    continue;
                }

                if (targets.Contains(current))
                {
                    return true;
                }

                foreach (SequenceFlow f in flows.Where(f => f.SourceId == current && f.TargetId != null))
                {
                    queue.Enqueue(f.TargetId);
                }
            }

            return false;
        }

        /// <summary>
        /// Prueft die START-Maske eines Start-Knotens. Die Felder selbst sind dieselben wie bei einer
        /// Benutzer-Aufgabe (daher <see cref="FormFieldIssues"/>); dazu kommt, was nur beim Start gilt:
        /// eine strikte Signatur verschluckt jedes Feld, das nicht deklariert ist.
        /// </summary>
        /// <summary>
        /// Prueft die <b>Ausloeser</b> eines Einstiegs: den Nachrichten-Start und den Zeitplan.
        /// </summary>
        /// <remarks>
        /// Beide fallen sonst erst im Betrieb auf, und zwar durch Nichts-Tun - der schlechteste Weg, einen
        /// Tippfehler zu bemerken. Ein Zeitplan, der nie zutrifft, schweigt einfach; eine Nachricht ohne
        /// Namen wird von keiner Zustellung je gefunden.
        /// </remarks>
        private static List<ValidationIssue> StartTriggerIssues(StartNode node)
        {
            var issues = new List<ValidationIssue>();

            if (node.MessageStart != null)
            {
                if (string.IsNullOrWhiteSpace(node.MessageStart.SignalName))
                {
                    issues.Add(Error(node.Id,
                        $"Start node '{Label(node)}' declares a message start without a message name - nothing "
                        + "will ever trigger it."));
                }

                if (node.MessageStart.Mode == MessageStartMode.StartIfNoneRunning
                    && !node.MessageStart.AdoptCorrelationKey)
                {
                    // Der Riegel fragt "laeuft schon eine MIT DIESEM Schluessel?". Wird der Schluessel
                    // nicht uebernommen, hat keine Instanz je einen - die Antwort ist dann immer "nein",
                    // und der Riegel ist ein Schalter, der nichts tut.
                    issues.Add(Warn(node.Id,
                        $"Start node '{Label(node)}' only starts when nothing is running, but does not adopt "
                        + "the correlation key - the check can never find a running instance and will never "
                        + "block anything."));
                }
            }

            if (node.ScheduleStart == null)
            {
                return issues;
            }

            if (string.IsNullOrWhiteSpace(node.ScheduleStart.Pattern))
            {
                issues.Add(Error(node.Id,
                    $"Start node '{Label(node)}' declares a schedule without a pattern - it will never run."));
            }
            else if (!ScheduleEvaluator.TryValidate(node.ScheduleStart.Pattern, out string error))
            {
                issues.Add(Error(node.Id, $"Start node '{Label(node)}' has an unusable schedule: {error}"));
            }

            return issues;
        }

        private static List<ValidationIssue> StartFormIssues(StartNode node)
        {
            var issues = new List<ValidationIssue>();
            AddIfBrokenCultureJson(issues, node.Id, node.FormDescription,
                $"Start node '{Label(node)}' form description");

            // ReadOnly/PayloadName sind beim Start bedeutungslos (es gibt noch keinen Payload) - deshalb
            // hier kein "read-only und required"-Hinweis, sondern der Hinweis, dass ReadOnly nichts tut.
            issues.AddRange(FormFieldIssues(node.Id, $"Start node '{Label(node)}'", node.FormFields,
                readOnlyMeaningful: false));

            if (node.FormFields == null || node.FormFields.Count == 0
                || node.ScopeMode != ActivityScopeMode.Replace
                || node.Inputs == null || node.Inputs.Count == 0)
            {
                return issues;
            }

            // Strikte Signatur: nach dem Start besteht der Stack genau aus den deklarierten Parametern
            // (plus keep-Liste). Ein Feld, das dort fehlt, wird eingegeben und sofort verworfen - das
            // sieht man der Maske nicht an und faellt sonst erst zur Laufzeit auf.
            foreach (UserTaskField field in node.FormFields)
            {
                if (field == null || string.IsNullOrWhiteSpace(field.Name))
                {
                    continue;
                }

                bool declared = node.Inputs.Any(b =>
                                    b != null && string.Equals(b.Parameter, field.Name, StringComparison.OrdinalIgnoreCase))
                                || (node.RetainVariables != null && node.RetainVariables.Any(r =>
                                    string.Equals(r, field.Name, StringComparison.OrdinalIgnoreCase)));
                if (!declared)
                {
                    issues.Add(Warn(node.Id,
                        $"Start node '{Label(node)}' form field '{field.Name}' is not declared in the strict " +
                        "signature - it is dropped right after the start."));
                }
            }

            return issues;
        }

        /// <summary>
        /// Prueft eine Feld-Deklaration (<see cref="UserTaskField"/>). Geteilt von der Aufgaben-Maske und
        /// der Start-Maske: dieselbe Beschreibung darf nicht zweimal verschieden geprueft werden.
        /// <paramref name="readOnlyMeaningful"/> unterscheidet die eine Stelle, an der sich die beiden
        /// Kontexte unterscheiden - beim Start gibt es keinen Payload und damit kein Nur-Anzeige-Feld.
        /// </summary>
        private static List<ValidationIssue> FormFieldIssues(string nodeId, string what,
            List<UserTaskField> fields, bool readOnlyMeaningful)
        {
            var issues = new List<ValidationIssue>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (UserTaskField field in fields ?? new List<UserTaskField>())
            {
                if (field == null || string.IsNullOrWhiteSpace(field.Name))
                {
                    issues.Add(Warn(nodeId, $"{what} has a form field without a name - it is ignored."));
                    continue;
                }

                if (!seen.Add(field.Name))
                {
                    issues.Add(Error(nodeId,
                        $"{what} declares the form field '{field.Name}' more than once - " +
                        "only one of them could ever reach the result."));
                }

                AddIfBrokenCultureJson(issues, nodeId, field.Label, $"{what} label of field '{field.Name}'");
                AddIfBrokenCultureJson(issues, nodeId, field.HelpText, $"{what} help text of field '{field.Name}'");

                if (field.Kind == UserTaskFieldKind.Choice && (field.Choices == null || field.Choices.Count == 0))
                {
                    issues.Add(Warn(nodeId, $"{what} field '{field.Name}' is a choice without any options."));
                }

                if (!field.ReadOnly)
                {
                    continue;
                }

                if (readOnlyMeaningful)
                {
                    if (field.Required)
                    {
                        issues.Add(Warn(nodeId,
                            $"{what} field '{field.Name}' is both read-only and required - " +
                            "read-only fields never reach the result, so the requirement has no effect."));
                    }
                }
                else
                {
                    issues.Add(Warn(nodeId,
                        $"{what} field '{field.Name}' is marked read-only - there is no payload before the " +
                        "instance exists, so the field is not shown at all."));
                }
            }

            return issues;
        }

        /// <summary>
        /// Meldet einen Wert, der nach Kultur-JSON aussieht, es aber nicht ist. Genau diesen Wert wuerde die
        /// Oberflaeche unveraendert anzeigen.
        /// </summary>
        /// <remarks>
        /// Die Absicht erkennt der Validator am fuehrenden '{'. Die Laufzeit
        /// (<c>StringExtensions.Translate</c>) verlangt zusaetzlich die schliessende Klammer und laesst
        /// einen abgeschnittenen Datensatz kommentarlos als Klartext durch - genau deshalb ist die
        /// fehlende Klammer hier ein eigener Befund und kein uebersehener Fall.
        /// </remarks>
        private static void AddIfBrokenCultureJson(List<ValidationIssue> issues, string nodeId, string value,
            string what)
        {
            string trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith("{"))
            {
                return;
            }

            if (!trimmed.EndsWith("}"))
            {
                issues.Add(Error(nodeId,
                    $"{what} looks like a per-culture record but does not end with '}}' - it would be shown " +
                    "verbatim."));
                return;
            }

            try
            {
                using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    issues.Add(Error(nodeId,
                        $"{what} looks like a per-culture record but is not a JSON object - it would be shown " +
                        "verbatim."));
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                issues.Add(Error(nodeId,
                    $"{what} looks like a per-culture record but is not valid JSON ({ex.Message}) - it would be " +
                    "shown verbatim."));
            }
        }

        /// <summary>
        /// Findet Variablen, die aus zwei oder mehr Zweigen DESSELBEN Splits (AND oder OR) geschrieben
        /// werden (ueber
        /// die deklarierten Output-Bindungen der Knoten und die Mappings der Kanten) - beim Zusammenfuehren
        /// am Join muss sich dann einer der Werte durchsetzen. Je betroffener Variable ein Befund. Nur deklarierte
        /// Schreibzugriffe sind statisch sichtbar; generische Aktivitaeten, die frei in <c>Variables</c>
        /// schreiben, kann die Pruefung nicht erfassen. Zwei Schreibzugriffe auf demselben Zweig
        /// (sequenziell) sind zulaessig und loesen keine Warnung aus.
        /// </summary>
        private static IEnumerable<ValidationIssue> ParallelWriteConflicts(List<WorkflowNode> nodes,
            List<SequenceFlow> flows, Dictionary<string, WorkflowNode> byId,
            Dictionary<string, int> inCount, Dictionary<string, int> outCount)
        {
            // Bewusst die KANTEN merken, nicht nur die Ziel-Ids: seit dem Kanten-Mapping ist die Kante
            // selbst eine Schreibstelle - der Zweig beginnt also schon an der Kante des Splits.
            var outgoing = new Dictionary<string, List<SequenceFlow>>(StringComparer.Ordinal);
            foreach (SequenceFlow f in flows)
            {
                if (f.SourceId == null || f.TargetId == null)
                {
                    continue;
                }

                if (!outgoing.TryGetValue(f.SourceId, out List<SequenceFlow> list))
                {
                    outgoing[f.SourceId] = list = new List<SequenceFlow>();
                }

                list.Add(f);
            }

            // AND und OR gleichermassen: beide geben ihren Zweigen eigene Kopien und fuehren sie an
            // einem Join wieder zusammen. Nur das AND zu betrachten hiesse, dieselbe Falle beim OR
            // schweigend durchgehen zu lassen.
            bool IsBranching(string id) => byId.TryGetValue(id, out WorkflowNode nn)
                                           && nn.Kind is NodeKind.ParallelGateway or NodeKind.InclusiveGateway;
            bool IsJoin(string id) => IsBranching(id) && (inCount.TryGetValue(id, out int c) ? c : 0) > 1;
            bool IsSplit(string id) => IsBranching(id) && (outCount.TryGetValue(id, out int c) ? c : 0) > 1;

            var result = new List<ValidationIssue>();
            var reported = new HashSet<string>(StringComparer.Ordinal); // je Variable nur ein Befund

            foreach (WorkflowNode split in nodes)
            {
                if (string.IsNullOrWhiteSpace(split?.Id) || !IsSplit(split.Id)
                    || !outgoing.TryGetValue(split.Id, out List<SequenceFlow> branches))
                {
                    continue;
                }

                // Je Variable: aus welchen (direkten) Split-Zweigen wird sie geschrieben, und von wo?
                var branchesByVar = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
                var writersByVar = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

                void Record(string variable, int branchIndex, string writer)
                {
                    if (string.IsNullOrWhiteSpace(variable))
                    {
                        return;
                    }

                    if (!branchesByVar.TryGetValue(variable, out HashSet<int> set))
                    {
                        branchesByVar[variable] = set = new HashSet<int>();
                    }

                    set.Add(branchIndex);
                    if (!writersByVar.TryGetValue(variable, out SortedSet<string> ws))
                    {
                        writersByVar[variable] = ws = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    ws.Add(writer);
                }

                for (int bi = 0; bi < branches.Count; bi++)
                {
                    var visited = new HashSet<string>(StringComparer.Ordinal);
                    var queue = new Queue<string>();

                    // Die Kante des Splits selbst gehoert schon zum Zweig.
                    RecordFlowWrites(branches[bi], bi, Record);
                    queue.Enqueue(branches[bi].TargetId);
                    while (queue.Count > 0)
                    {
                        string cur = queue.Dequeue();
                        if (!visited.Add(cur) || IsJoin(cur))
                        {
                            continue; // Grenze: der Join gehoert nicht mehr zur Region.
                        }

                        if (byId.TryGetValue(cur, out WorkflowNode cn) && cn is AutomatedActivityNode act
                            && act.Outputs != null)
                        {
                            foreach (ActivityOutputBinding ob in act.Outputs)
                            {
                                Record(ob?.Variable, bi, $"node '{Label(cn)}'");
                            }
                        }

                        if (outgoing.TryGetValue(cur, out List<SequenceFlow> nexts))
                        {
                            foreach (SequenceFlow nx in nexts)
                            {
                                RecordFlowWrites(nx, bi, Record);
                                queue.Enqueue(nx.TargetId);
                            }
                        }
                    }
                }

                foreach (KeyValuePair<string, HashSet<int>> kv in branchesByVar)
                {
                    if (kv.Value.Count >= 2 && reported.Add(kv.Key))
                    {
                        result.Add(Warn(null,
                            $"Variable '{kv.Key}' is written by parallel branches ({string.Join(", ", writersByVar[kv.Key])}) - " +
                            "each branch works on its own copy, so the join has to pick one of the values. Give " +
                            "each branch its own result name and merge them with a mapping on the join."));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Ermittelt die Knoten, die innerhalb einer parallelen Region liegen: erreichbar von einem
        /// Split (AND- oder OR-Gateway mit &gt;1 Ausgang), ohne den zugehoerigen Join (dieselben
        /// Gateway-Arten mit &gt;1 Eingang) zu ueberschreiten. Der Join ist die Grenze - er selbst und
        /// alles dahinter zaehlen nicht als "in der Region".
        /// </summary>
        private static HashSet<string> NodesInsideParallelRegion(List<WorkflowNode> nodes,
            List<SequenceFlow> flows, Dictionary<string, WorkflowNode> byId,
            Dictionary<string, int> inCount, Dictionary<string, int> outCount)
        {
            var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (SequenceFlow f in flows)
            {
                if (f.SourceId == null || f.TargetId == null)
                {
                    continue;
                }

                if (!outgoing.TryGetValue(f.SourceId, out List<string> list))
                {
                    outgoing[f.SourceId] = list = new List<string>();
                }

                list.Add(f.TargetId);
            }

            // AND und OR gleichermassen: beide geben ihren Zweigen eigene Kopien und fuehren sie an
            // einem Join wieder zusammen. Nur das AND zu betrachten hiesse, dieselbe Falle beim OR
            // schweigend durchgehen zu lassen.
            bool IsBranching(string id) => byId.TryGetValue(id, out WorkflowNode nn)
                                           && nn.Kind is NodeKind.ParallelGateway or NodeKind.InclusiveGateway;
            bool IsJoin(string id) => IsBranching(id) && (inCount.TryGetValue(id, out int c) ? c : 0) > 1;
            bool IsSplit(string id) => IsBranching(id) && (outCount.TryGetValue(id, out int c) ? c : 0) > 1;

            var region = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorkflowNode n in nodes)
            {
                if (string.IsNullOrWhiteSpace(n?.Id) || !IsSplit(n.Id))
                {
                    continue;
                }

                var queue = new Queue<string>();
                var visited = new HashSet<string>(StringComparer.Ordinal);
                if (outgoing.TryGetValue(n.Id, out List<string> starts))
                {
                    foreach (string s in starts)
                    {
                        queue.Enqueue(s);
                    }
                }

                while (queue.Count > 0)
                {
                    string cur = queue.Dequeue();
                    if (!visited.Add(cur))
                    {
                        continue;
                    }

                    if (IsJoin(cur))
                    {
                        continue; // Grenze: bis zum Join, nicht darueber hinaus.
                    }

                    region.Add(cur);
                    if (outgoing.TryGetValue(cur, out List<string> nexts))
                    {
                        foreach (string nx in nexts)
                        {
                            queue.Enqueue(nx);
                        }
                    }
                }
            }

            return region;
        }

        /// <summary>
        /// Traegt die Variablen ein, die das Mapping einer Kante schreibt - fuer die Kante gilt dasselbe wie
        /// fuer einen Knoten: sie schreibt in den gemeinsamen Instanz-Stack.
        /// </summary>
        private static void RecordFlowWrites(SequenceFlow flow, int branchIndex,
            Action<string, int, string> record)
        {
            if (flow?.Inputs == null)
            {
                return;
            }

            foreach (ActivityInputBinding b in flow.Inputs)
            {
                record(b?.Parameter, branchIndex, $"connection '{FlowName(flow)}'");
            }
        }

        private static string Label(WorkflowNode n) => string.IsNullOrEmpty(n.Name) ? n.Id : n.Name;

        private static string FlowName(SequenceFlow f) => string.IsNullOrEmpty(f.Name) ? f.Id : f.Name;

        /// <summary>Beschriftet eine Kante fuer eine Meldung: ihr Name plus die verbundenen Knoten.</summary>
        private static string FlowLabel(SequenceFlow f, Dictionary<string, WorkflowNode> byId)
        {
            string Node(string id) => id != null && byId.TryGetValue(id, out WorkflowNode n) ? Label(n) : "?";
            return $"'{FlowName(f)}' ({Node(f.SourceId)} -> {Node(f.TargetId)})";
        }

        private static ValidationIssue Error(string nodeId, string message)
            => new ValidationIssue { Severity = ValidationSeverity.Error, NodeId = nodeId, Message = message };

        private static ValidationIssue Warn(string nodeId, string message)
            => new ValidationIssue { Severity = ValidationSeverity.Warning, NodeId = nodeId, Message = message };
    }
}
