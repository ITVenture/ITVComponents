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

        private readonly ConcurrentDictionary<(string InstanceId, string TokenId), string> branchLocks =
            new ConcurrentDictionary<(string, string), string>();

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

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindRunnable()
        {
            return instances.Values.Where(i => i.Status == WorkflowStatus.Running).ToList();
        }

        /// <inheritdoc/>
        public IWorkflowBranchLock TryAcquireBranchLock(string instanceId, string tokenId, string owner)
        {
            if (instanceId == null) throw new ArgumentNullException(nameof(instanceId));
            if (tokenId == null) throw new ArgumentNullException(nameof(tokenId));
            if (string.IsNullOrEmpty(owner)) throw new ArgumentNullException(nameof(owner));

            return branchLocks.TryAdd((instanceId, tokenId), owner)
                ? new BranchLock(this, instanceId, tokenId, owner)
                : null;
        }

        /// <inheritdoc/>
        public void ReleaseLocksOfOwner(string owner)
        {
            if (string.IsNullOrEmpty(owner))
            {
                return;
            }

            foreach (KeyValuePair<(string, string), string> pair in branchLocks.Where(k => k.Value == owner).ToList())
            {
                branchLocks.TryRemove(pair);
            }
        }

        private void ReleaseBranch(string instanceId, string tokenId, string owner)
        {
            // Nur der Besitzer gibt frei (atomar ueber den Wert-gebundenen TryRemove).
            branchLocks.TryRemove(
                new KeyValuePair<(string, string), string>((instanceId, tokenId), owner));
        }

        private static string Key(string id, int version)
        {
            return $"{id}#{version}";
        }

        private sealed class BranchLock : IWorkflowBranchLock
        {
            private readonly InMemoryWorkflowStore store;
            private bool released;

            public BranchLock(InMemoryWorkflowStore store, string instanceId, string tokenId, string owner)
            {
                this.store = store;
                InstanceId = instanceId;
                TokenId = tokenId;
                Owner = owner;
            }

            public string InstanceId { get; }

            public string TokenId { get; }

            public string Owner { get; }

            public void Dispose()
            {
                if (!released)
                {
                    released = true;
                    store.ReleaseBranch(InstanceId, TokenId, Owner);
                }
            }
        }
    }
}
