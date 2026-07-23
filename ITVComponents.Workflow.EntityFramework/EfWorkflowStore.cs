using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;

namespace ITVComponents.Workflow.EntityFramework
{
    /// <summary>
    /// Ein <see cref="IWorkflowStore"/> auf EF-Core-Basis. Instanzen und Definitionen liegen als
    /// JSON-Blob, ergaenzt um ausgegliederte Spalten und einen Warte-Token-Index fuer die Signal-
    /// und Timer-Abfragen.
    /// </summary>
    /// <remarks>
    /// Jeder Aufruf oeffnet einen eigenen Kontext (Unit of Work) ueber die uebergebene Factory -
    /// so ist der Store frei von der Thread-Bindung eines langlebigen DbContext.
    ///
    /// Serialisierung ueber <see cref="JsonHelper"/> (System.Text.Json): die Variablen und die
    /// Definition typerhaltend (<see cref="SerializationTypingMode.NativePolymorphism"/>, damit
    /// object-Werte und der polymorphe Knotengraph zurueckkommen), Tokens und Protokoll statisch
    /// getypt. Der eingebettete .NET-Typname der Definition koppelt gespeicherte Definitionen an die
    /// Knotentypen - beim visuellen Modeler (spaetere Phase) wird dafuer ein stabiler,
    /// typnamen-unabhaengiger Vertrag festgelegt.
    /// </remarks>
    public class EfWorkflowStore : IWorkflowStore
    {
        private readonly Func<WorkflowContext> contextFactory;

        /// <summary>
        /// Initialisiert den Store mit einer Factory, die pro Aufruf einen Kontext liefert.
        /// </summary>
        public EfWorkflowStore(Func<WorkflowContext> contextFactory)
        {
            this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        /// <inheritdoc/>
        public void SaveDefinition(WorkflowDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            using WorkflowContext ctx = contextFactory();
            WorkflowDefinitionRow row = ctx.WorkflowDefinitions.Find(definition.Id, definition.Version);
            if (row == null)
            {
                row = new WorkflowDefinitionRow { Id = definition.Id, Version = definition.Version };
                ctx.WorkflowDefinitions.Add(row);
            }

            // Die Knoten-Polymorphie steckt in den Diskriminator-Attributen - das JSON bleibt frei
            // von .NET-Typnamen.
            row.DefinitionJson = WorkflowJson.Serialize(definition);
            ctx.SaveChanges();
        }

        /// <inheritdoc/>
        public WorkflowDefinition GetDefinition(string definitionId, int? version = null)
        {
            using WorkflowContext ctx = contextFactory();
            WorkflowDefinitionRow row = version.HasValue
                ? ctx.WorkflowDefinitions.Find(definitionId, version.Value)
                : ctx.WorkflowDefinitions
                    .Where(d => d.Id == definitionId)
                    .OrderByDescending(d => d.Version)
                    .FirstOrDefault();

            return row == null ? null : WorkflowJson.Deserialize<WorkflowDefinition>(row.DefinitionJson);
        }

        /// <inheritdoc/>
        public void SaveInstance(WorkflowInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            instance.UpdatedUtc = DateTime.UtcNow;

            using WorkflowContext ctx = contextFactory();
            WorkflowInstanceRow row = ctx.WorkflowInstances.Find(instance.Id);
            if (row == null)
            {
                row = new WorkflowInstanceRow { Id = instance.Id };
                ctx.WorkflowInstances.Add(row);
            }

            row.DefinitionId = instance.DefinitionId;
            row.DefinitionVersion = instance.DefinitionVersion;
            row.Status = (int)instance.Status;
            row.CorrelationKey = instance.CorrelationKey;
            row.FaultMessage = instance.FaultMessage;
            row.CreatedUtc = instance.CreatedUtc;
            row.UpdatedUtc = instance.UpdatedUtc;
            row.VariablesJson = WorkflowJson.Serialize(instance.Variables);
            row.TokensJson = WorkflowJson.Serialize(instance.Tokens);
            row.HistoryJson = WorkflowJson.Serialize(instance.History);

            // Der Warte-Token-Index wird bei jedem Speichern neu aufgebaut - so bildet er immer den
            // aktuellen Wartestand ab, ohne dass verwaiste Zeilen die Abfragen verfaelschen.
            var stale = ctx.WaitingTokens.Where(w => w.InstanceId == instance.Id).ToList();
            ctx.WaitingTokens.RemoveRange(stale);
            foreach (Token token in instance.Tokens.Where(t => t.Status == TokenStatus.Waiting))
            {
                ctx.WaitingTokens.Add(new WaitingTokenRow
                {
                    InstanceId = instance.Id,
                    CorrelationKey = instance.CorrelationKey,
                    WaitingSignal = token.WaitingSignal,
                    DueUtc = token.DueUtc
                });
            }

            ctx.SaveChanges();
        }

        /// <inheritdoc/>
        public WorkflowInstance GetInstance(string instanceId)
        {
            using WorkflowContext ctx = contextFactory();
            WorkflowInstanceRow row = ctx.WorkflowInstances.Find(instanceId);
            return row == null ? null : ToInstance(row);
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindWaitingForSignal(string signalName, string correlationKey = null)
        {
            using WorkflowContext ctx = contextFactory();
            IQueryable<WaitingTokenRow> query = ctx.WaitingTokens.Where(w => w.WaitingSignal == signalName);
            if (correlationKey != null)
            {
                query = query.Where(w => w.CorrelationKey == correlationKey || w.InstanceId == correlationKey);
            }

            List<string> ids = query.Select(w => w.InstanceId).Distinct().ToList();
            return LoadInstances(ctx, ids);
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindDueTimers(DateTime nowUtc)
        {
            using WorkflowContext ctx = contextFactory();
            List<string> ids = ctx.WaitingTokens
                .Where(w => w.DueUtc != null && w.DueUtc <= nowUtc)
                .Select(w => w.InstanceId)
                .Distinct()
                .ToList();
            return LoadInstances(ctx, ids);
        }

        private static List<WorkflowInstance> LoadInstances(WorkflowContext ctx, List<string> ids)
        {
            if (ids.Count == 0)
            {
                return new List<WorkflowInstance>();
            }

            return ctx.WorkflowInstances
                .Where(r => ids.Contains(r.Id))
                .ToList()
                .Select(ToInstance)
                .ToList();
        }

        private static WorkflowInstance ToInstance(WorkflowInstanceRow row)
        {
            return new WorkflowInstance
            {
                Id = row.Id,
                DefinitionId = row.DefinitionId,
                DefinitionVersion = row.DefinitionVersion,
                Status = (WorkflowStatus)row.Status,
                CorrelationKey = row.CorrelationKey,
                FaultMessage = row.FaultMessage,
                CreatedUtc = row.CreatedUtc,
                UpdatedUtc = row.UpdatedUtc,
                Variables = WorkflowJson.Deserialize<Dictionary<string, object>>(row.VariablesJson)
                            ?? new Dictionary<string, object>(),
                Tokens = WorkflowJson.Deserialize<List<Token>>(row.TokensJson) ?? new List<Token>(),
                History = WorkflowJson.Deserialize<List<HistoryEntry>>(row.HistoryJson) ?? new List<HistoryEntry>()
            };
        }
    }
}
