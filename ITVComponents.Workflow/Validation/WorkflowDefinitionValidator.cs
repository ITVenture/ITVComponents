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
            }

            // Fehler zuerst, dann Warnungen - stabile Reihenfolge fuer die Anzeige.
            return issues.OrderBy(x => x.Severity).ToList();
        }

        private static string Label(WorkflowNode n) => string.IsNullOrEmpty(n.Name) ? n.Id : n.Name;

        private static ValidationIssue Error(string nodeId, string message)
            => new ValidationIssue { Severity = ValidationSeverity.Error, NodeId = nodeId, Message = message };

        private static ValidationIssue Warn(string nodeId, string message)
            => new ValidationIssue { Severity = ValidationSeverity.Warning, NodeId = nodeId, Message = message };
    }
}
