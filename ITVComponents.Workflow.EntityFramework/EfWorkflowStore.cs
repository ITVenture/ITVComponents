using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.EntityFrameworkCore;

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
            // Bewusst OHNE Query-Filter und explizit auf den Tenant der zu speichernden Definition
            // gematcht: das Schreiben soll deterministisch die richtige Zeile treffen, unabhaengig vom
            // gerade aktiven Tenant-Kontext (sonst koennte der Filter die zu aktualisierende Zeile
            // verstecken und ein Duplikat/Schluesselkonflikt entstehen).
            WorkflowDefinitionRow row = ctx.WorkflowDefinitions
                .IgnoreQueryFilters()
                .FirstOrDefault(d => d.Id == definition.Id && d.Version == definition.Version
                                     && d.TenantId == definition.TenantId);
            if (row == null)
            {
                row = new WorkflowDefinitionRow { Id = definition.Id, Version = definition.Version };
                ctx.WorkflowDefinitions.Add(row);
            }

            row.TenantId = definition.TenantId;
            // Die Knoten-Polymorphie steckt in den Diskriminator-Attributen - das JSON bleibt frei
            // von .NET-Typnamen.
            row.DefinitionJson = WorkflowJson.Serialize(definition);
            ctx.SaveChanges();
        }

        /// <inheritdoc/>
        public WorkflowDefinition GetDefinition(string definitionId, int? version = null)
        {
            using WorkflowContext ctx = contextFactory();
            // Bewusst .Where statt .Find: Find umgeht in EF Core die globalen Query-Filter - der Tenant-
            // Filter (eigener Tenant ODER oeffentlich) muss hier aber greifen.
            WorkflowDefinitionRow row = version.HasValue
                ? ctx.WorkflowDefinitions
                    .Where(d => d.Id == definitionId && d.Version == version.Value)
                    .FirstOrDefault()
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
            // Instanz-Id ist global eindeutig (GUID) - der Lookup ignoriert bewusst die Query-Filter,
            // damit ein Speichern die vorhandene Zeile trifft, egal welcher Tenant gerade aktiv ist.
            WorkflowInstanceRow row = ctx.WorkflowInstances
                .IgnoreQueryFilters()
                .FirstOrDefault(r => r.Id == instance.Id);
            if (row == null)
            {
                // Neue Instanz: Tenant festschreiben (aus der Instanz oder dem aktiven Kontext) und
                // zurueckspiegeln. Bei bestehenden Zeilen bleibt der Tenant unveraendert.
                string tenant = instance.TenantId ?? ctx.CurrentTenant;
                row = new WorkflowInstanceRow { Id = instance.Id, TenantId = tenant };
                instance.TenantId = tenant;
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
            // .Where statt .Find, damit der strikte Instanz-Tenant-Filter greift (Find umgeht ihn).
            WorkflowInstanceRow row = ctx.WorkflowInstances.FirstOrDefault(r => r.Id == instanceId);
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

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindRunnable()
        {
            using WorkflowContext ctx = contextFactory();
            int running = (int)WorkflowStatus.Running;
            return ctx.WorkflowInstances
                .Where(r => r.Status == running)
                .ToList()
                .Select(ToInstance)
                .ToList();
        }

        /// <inheritdoc/>
        public IWorkflowBranchLock TryAcquireBranchLock(string instanceId, string tokenId, string owner)
        {
            if (instanceId == null) throw new ArgumentNullException(nameof(instanceId));
            if (tokenId == null) throw new ArgumentNullException(nameof(tokenId));
            if (string.IsNullOrEmpty(owner)) throw new ArgumentNullException(nameof(owner));

            try
            {
                using WorkflowContext ctx = contextFactory();
                // Atomarer Erwerb: der INSERT des (InstanceId, TokenId)-Schluessels ist der CAS-Punkt -
                // ist der Zweig bereits gesperrt, verletzt er den Primaerschluessel.
                ctx.BranchLocks.Add(new WorkflowBranchLockRow
                {
                    InstanceId = instanceId,
                    TokenId = tokenId,
                    Owner = owner,
                    AcquiredUtc = DateTime.UtcNow
                });
                ctx.SaveChanges();
                return new BranchLock(this, instanceId, tokenId, owner);
            }
            catch (DbUpdateException ex)
            {
                // Entweder Contention (Schluessel existiert bereits) oder ein echter DB-Fehler - das
                // muss unterschieden werden, damit ein realer Fehler nicht als "gesperrt" verschluckt wird.
                using WorkflowContext check = contextFactory();
                if (check.BranchLocks.Any(l => l.InstanceId == instanceId && l.TokenId == tokenId))
                {
                    return null; // bereits gesperrt - regulaeres Ergebnis
                }

                LogEnvironment.LogEvent(
                    $"Unexpected error acquiring branch lock for instance '{instanceId}' token '{tokenId}': " +
                    $"{ex.OutlineException()}", LogSeverity.Error);
                throw;
            }
        }

        /// <inheritdoc/>
        public void ReleaseLocksOfOwner(string owner)
        {
            if (string.IsNullOrEmpty(owner))
            {
                return;
            }

            using WorkflowContext ctx = contextFactory();
            ctx.BranchLocks.Where(l => l.Owner == owner).ExecuteDelete();
        }

        private void ReleaseBranch(string instanceId, string tokenId, string owner)
        {
            using WorkflowContext ctx = contextFactory();
            // Nur der Besitzer gibt frei.
            ctx.BranchLocks
                .Where(l => l.InstanceId == instanceId && l.TokenId == tokenId && l.Owner == owner)
                .ExecuteDelete();
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
                TenantId = row.TenantId,
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

        private sealed class BranchLock : IWorkflowBranchLock
        {
            private readonly EfWorkflowStore store;
            private bool released;

            public BranchLock(EfWorkflowStore store, string instanceId, string tokenId, string owner)
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
                if (released)
                {
                    return;
                }

                released = true;
                try
                {
                    store.ReleaseBranch(InstanceId, TokenId, Owner);
                }
                catch (Exception ex)
                {
                    // Freigabe darf nicht mitreissen; ein nicht freigegebener Lock wird spaetestens beim
                    // Runner-Neustart (ReleaseLocksOfOwner) abgeraeumt. Der Fehler wird protokolliert.
                    LogEnvironment.LogEvent(
                        $"Could not release branch lock for instance '{InstanceId}' token '{TokenId}': " +
                        $"{ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }
    }
}
