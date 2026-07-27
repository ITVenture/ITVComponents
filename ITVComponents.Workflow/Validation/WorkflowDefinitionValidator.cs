using System;
using System.Collections.Generic;
using System.Linq;
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

            if (!nodes.Any(n => n.Kind == NodeKind.Start))
            {
                issues.Add(Error(null, "No start node."));
            }

            if (!nodes.Any(n => n.Kind == NodeKind.End))
            {
                issues.Add(Warn(null, "No end node - the workflow can never complete."));
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

                if (n.Kind != NodeKind.End && outs == 0)
                {
                    issues.Add(Error(n.Id, $"Node '{Label(n)}' has no outgoing connection - a token would get stuck."));
                }

                if (n.Kind == NodeKind.End && outs > 0)
                {
                    issues.Add(Warn(n.Id, $"End node '{Label(n)}' has outgoing connections - they are ignored."));
                }

                if (n.Kind != NodeKind.Start && ins == 0)
                {
                    issues.Add(Warn(n.Id, $"Node '{Label(n)}' has no incoming connection - it is unreachable."));
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
                    case TimerNode t when string.IsNullOrWhiteSpace(t.DueExpression):
                        issues.Add(Error(n.Id, $"Timer node '{Label(n)}' has no due expression."));
                        break;
                    case ExclusiveGatewayNode x when outs > 0 && string.IsNullOrWhiteSpace(x.DefaultFlowId):
                        issues.Add(Warn(n.Id,
                            $"Exclusive gateway '{Label(n)}' has no default flow - the instance faults if no condition matches."));
                        break;
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

            // Konsolidierung (ScopeMode.Replace) raeumt den ganzen Scope ab - das ist nur auf einem
            // Ein-Zweig-Segment sicher. Liegt sie innerhalb einer parallelen Region (zwischen AND-Split
            // und zugehoerigem Join), koennte sie Variablen verwerfen, die ein Geschwister-Zweig noch
            // braucht (oder mit dessen Merge kollidieren).
            HashSet<string> parallelRegion = NodesInsideParallelRegion(nodes, flows, byId, inCount, outCount);
            foreach (WorkflowNode n in nodes)
            {
                if (n is AutomatedActivityNode act && act.ScopeMode == ActivityScopeMode.Replace
                    && !string.IsNullOrWhiteSpace(act.Id) && parallelRegion.Contains(act.Id))
                {
                    issues.Add(Warn(act.Id,
                        $"Consolidation node '{Label(n)}' (scope replace) is inside a parallel region - it may " +
                        "discard variables a sibling branch still needs. Place it after the join."));
                }
            }

            // Zwei parallele Zweige, die dieselbe Variable schreiben, kollidieren zur Laufzeit (Fault, kein
            // stiller last-writer). Statisch erkennbar an den deklarierten Output-Bindungen: schreibt innerhalb
            // einer parallelen Region dieselbe Variable aus zwei verschiedenen Split-Zweigen, wird gewarnt.
            issues.AddRange(ParallelWriteConflicts(nodes, flows, byId, inCount, outCount));

            // Fehler zuerst, dann Warnungen - stabile Reihenfolge fuer die Anzeige.
            return issues.OrderBy(x => x.Severity).ToList();
        }

        /// <summary>
        /// Findet Variablen, die aus zwei oder mehr Zweigen DESSELBEN AND-Splits geschrieben werden (ueber
        /// die deklarierten Output-Bindungen) - solche parallelen Schreibzugriffe faulten zur Laufzeit. Je
        /// betroffener Variable ein Befund. Nur deklarierte Ausgaben sind statisch sichtbar; generische
        /// Aktivitaeten, die frei in <c>Variables</c> schreiben, kann die Pruefung nicht erfassen. Zwei
        /// Schreibzugriffe auf demselben Zweig (sequenziell) sind zulaessig und loesen keine Warnung aus.
        /// </summary>
        private static IEnumerable<ValidationIssue> ParallelWriteConflicts(List<WorkflowNode> nodes,
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

            bool IsJoin(string id) => byId.TryGetValue(id, out WorkflowNode nn)
                                      && nn.Kind == NodeKind.ParallelGateway
                                      && (inCount.TryGetValue(id, out int c) ? c : 0) > 1;
            bool IsSplit(string id) => byId.TryGetValue(id, out WorkflowNode nn)
                                       && nn.Kind == NodeKind.ParallelGateway
                                       && (outCount.TryGetValue(id, out int c) ? c : 0) > 1;

            var result = new List<ValidationIssue>();
            var reported = new HashSet<string>(StringComparer.Ordinal); // je Variable nur ein Befund

            foreach (WorkflowNode split in nodes)
            {
                if (string.IsNullOrWhiteSpace(split?.Id) || !IsSplit(split.Id)
                    || !outgoing.TryGetValue(split.Id, out List<string> branches))
                {
                    continue;
                }

                // Je Variable: aus welchen (direkten) Split-Zweigen wird sie geschrieben, und von welchen Knoten?
                var branchesByVar = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
                var nodesByVar = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

                for (int bi = 0; bi < branches.Count; bi++)
                {
                    var visited = new HashSet<string>(StringComparer.Ordinal);
                    var queue = new Queue<string>();
                    queue.Enqueue(branches[bi]);
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
                                if (ob == null || string.IsNullOrWhiteSpace(ob.Variable))
                                {
                                    continue;
                                }

                                if (!branchesByVar.TryGetValue(ob.Variable, out HashSet<int> set))
                                {
                                    branchesByVar[ob.Variable] = set = new HashSet<int>();
                                }

                                set.Add(bi);
                                if (!nodesByVar.TryGetValue(ob.Variable, out SortedSet<string> ns))
                                {
                                    nodesByVar[ob.Variable] = ns = new SortedSet<string>(StringComparer.Ordinal);
                                }

                                ns.Add(cur);
                            }
                        }

                        if (outgoing.TryGetValue(cur, out List<string> nexts))
                        {
                            foreach (string nx in nexts)
                            {
                                queue.Enqueue(nx);
                            }
                        }
                    }
                }

                foreach (KeyValuePair<string, HashSet<int>> kv in branchesByVar)
                {
                    if (kv.Value.Count >= 2 && reported.Add(kv.Key))
                    {
                        result.Add(Warn(null,
                            $"Variable '{kv.Key}' is written by parallel branches (nodes {string.Join(", ", nodesByVar[kv.Key])}) - " +
                            "concurrent writes to the same variable fault at runtime. Let only one branch write it, " +
                            "or consolidate after the join."));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Ermittelt die Knoten, die innerhalb einer parallelen Region liegen: erreichbar von einem
        /// AND-Split (paralleles Gateway mit &gt;1 Ausgang), ohne den zugehoerigen Join (paralleles
        /// Gateway mit &gt;1 Eingang) zu ueberschreiten. Der Join ist die Grenze - er selbst und alles
        /// dahinter zaehlen nicht als "in der Region".
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

            bool IsJoin(string id) => byId.TryGetValue(id, out WorkflowNode nn)
                                      && nn.Kind == NodeKind.ParallelGateway
                                      && (inCount.TryGetValue(id, out int c) ? c : 0) > 1;
            bool IsSplit(string id) => byId.TryGetValue(id, out WorkflowNode nn)
                                       && nn.Kind == NodeKind.ParallelGateway
                                       && (outCount.TryGetValue(id, out int c) ? c : 0) > 1;

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

        private static string Label(WorkflowNode n) => string.IsNullOrEmpty(n.Name) ? n.Id : n.Name;

        private static ValidationIssue Error(string nodeId, string message)
            => new ValidationIssue { Severity = ValidationSeverity.Error, NodeId = nodeId, Message = message };

        private static ValidationIssue Warn(string nodeId, string message)
            => new ValidationIssue { Severity = ValidationSeverity.Warning, NodeId = nodeId, Message = message };
    }
}
