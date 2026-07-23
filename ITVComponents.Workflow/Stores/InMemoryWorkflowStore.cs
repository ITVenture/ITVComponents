using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;

namespace ITVComponents.Workflow.Stores
{
    /// <summary>
    /// Eine In-Memory-Implementierung von <see cref="IWorkflowStore"/> fuer Tests und einfache,
    /// nicht-persistente Szenarien.
    /// </summary>
    /// <remarks>
    /// Haelt die Instanzen als Referenzen (keine Serialisierung). Ein echter DB-Store schreibt und
    /// liest serialisierte Kopien; darum sollte die Engine Instanzen immer explizit ueber
    /// <see cref="SaveInstance"/> festhalten und nicht auf geteilte Referenzen bauen.
    /// </remarks>
    public class InMemoryWorkflowStore : IWorkflowStore
    {
        private readonly ConcurrentDictionary<string, WorkflowDefinition> definitions =
            new ConcurrentDictionary<string, WorkflowDefinition>();

        private readonly ConcurrentDictionary<string, WorkflowInstance> instances =
            new ConcurrentDictionary<string, WorkflowInstance>();

        /// <inheritdoc/>
        public void SaveDefinition(WorkflowDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definitions[Key(definition.Id, definition.Version)] = definition;
        }

        /// <inheritdoc/>
        public WorkflowDefinition GetDefinition(string definitionId, int? version = null)
        {
            if (version.HasValue)
            {
                return definitions.TryGetValue(Key(definitionId, version.Value), out WorkflowDefinition def)
                    ? def
                    : null;
            }

            return definitions.Values
                .Where(d => d.Id == definitionId)
                .OrderByDescending(d => d.Version)
                .FirstOrDefault();
        }

        /// <inheritdoc/>
        public void SaveInstance(WorkflowInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            instance.UpdatedUtc = DateTime.UtcNow;
            instances[instance.Id] = instance;
        }

        /// <inheritdoc/>
        public WorkflowInstance GetInstance(string instanceId)
        {
            return instanceId != null && instances.TryGetValue(instanceId, out WorkflowInstance instance)
                ? instance
                : null;
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindWaitingForSignal(string signalName, string correlationKey = null)
        {
            return instances.Values
                .Where(i => i.Status == WorkflowStatus.Waiting)
                .Where(i => correlationKey == null || i.CorrelationKey == correlationKey || i.Id == correlationKey)
                .Where(i => i.WaitingTokens.Any(t => t.WaitingSignal == signalName))
                .ToList();
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindDueTimers(DateTime nowUtc)
        {
            return instances.Values
                .Where(i => i.Status == WorkflowStatus.Waiting)
                .Where(i => i.WaitingTokens.Any(t => t.DueUtc.HasValue && t.DueUtc.Value <= nowUtc))
                .ToList();
        }

        private static string Key(string id, int version)
        {
            return $"{id}#{version}";
        }
    }
}
