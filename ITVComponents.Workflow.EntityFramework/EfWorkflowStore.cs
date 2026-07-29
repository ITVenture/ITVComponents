using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Serialization;
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
            bool isNew = row == null;
            if (isNew)
            {
                // Neue Instanz: Tenant festschreiben (aus der Instanz oder dem aktiven Kontext) und
                // zurueckspiegeln. Bei bestehenden Zeilen bleibt der Tenant unveraendert.
                string tenant = instance.TenantId ?? ctx.CurrentTenant;
                row = new WorkflowInstanceRow { Id = instance.Id, TenantId = tenant, Version = 0 };
                instance.TenantId = tenant;
                ctx.WorkflowInstances.Add(row);
            }
            else
            {
                // Force-Write (sequenzieller Pfad): Version fortschreiben. Das EF-Concurrency-Token wuerde
                // bei einer zwischenzeitlichen Aenderung werfen - im sequenziellen Betrieb passiert das nicht.
                row.Version++;
            }

            ApplyInstanceToRow(ctx, instance, row);
            ctx.SaveChanges();
            instance.Version = row.Version;
        }

        /// <inheritdoc/>
        public bool TryCommitInstance(WorkflowInstance instance, int baseVersion)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            instance.UpdatedUtc = DateTime.UtcNow;

            using WorkflowContext ctx = contextFactory();
            WorkflowInstanceRow row = ctx.WorkflowInstances
                .IgnoreQueryFilters()
                .FirstOrDefault(r => r.Id == instance.Id);
            if (row == null || row.Version != baseVersion)
            {
                // Entweder verschwunden oder ein anderer Zweig hat inzwischen committed -> Konflikt.
                return false;
            }

            row.Version = baseVersion + 1;
            ApplyInstanceToRow(ctx, instance, row);
            try
            {
                ctx.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Zwischen Laden und Speichern hat ein anderer committed (das Token deckt das Rennen ab).
                return false;
            }

            instance.Version = row.Version;
            return true;
        }

        /// <summary>
        /// Uebertraegt Felder und Token-Zeilen der Instanz auf die Zeile (ohne Version/Tenant - die werden
        /// vom Aufrufer gesetzt). Token-Zeilen als In-Place-Upsert (vorhandene aktualisieren, neue anlegen,
        /// verschwundene loeschen) in EINEM SaveChanges - ohne Loeschen+Neuanlegen desselben Schluessels
        /// (das wuerde den Change-Tracker in Konflikt bringen).
        /// </summary>
        private static void ApplyInstanceToRow(WorkflowContext ctx, WorkflowInstance instance, WorkflowInstanceRow row)
        {
            row.DefinitionId = instance.DefinitionId;
            row.DefinitionVersion = instance.DefinitionVersion;
            row.Status = (int)instance.Status;
            row.CorrelationKey = instance.CorrelationKey;
            row.FaultMessage = instance.FaultMessage;
            row.ParentInstanceId = instance.ParentInstanceId;
            row.ParentTokenId = instance.ParentTokenId;
            row.RootInstanceId = instance.EffectiveRootInstanceId;
            row.CallDepth = instance.CallDepth;
            row.CreatedUtc = instance.CreatedUtc;
            row.UpdatedUtc = instance.UpdatedUtc;
            row.VariablesJson = WorkflowJson.Serialize(instance.Variables);

            // Protokoll append-only: nur die noch nicht persistierten Eintraege einfuegen - NICHT das ganze
            // (wachsende) Protokoll neu schreiben. Die Inserts laufen im selben (versions-gepruefen)
            // SaveChanges wie das Instanz-Update; bei einem Konflikt rollt alles zusammen zurueck, ein Retry
            // fuegt daher nichts doppelt ein. Seq setzt die instanz-interne Reihenfolge fort; RootInstanceId
            // ist (mangels Eltern-Workflow) die eigene Id und traegt spaeter den aggregierten Prozessbaum.
            int persisted = ctx.HistoryEntries.Count(h => h.InstanceId == instance.Id);
            for (int i = persisted; i < instance.History.Count; i++)
            {
                HistoryEntry h = instance.History[i];
                ctx.HistoryEntries.Add(new HistoryEntryRow
                {
                    InstanceId = instance.Id,
                    RootInstanceId = instance.EffectiveRootInstanceId,
                    Seq = i,
                    TimestampUtc = h.TimestampUtc,
                    NodeId = h.NodeId,
                    Event = h.Event,
                    Detail = h.Detail,
                    Severity = (int)h.Severity
                });
            }

            List<TokenRow> existing = ctx.Tokens.Where(t => t.InstanceId == instance.Id).ToList();
            Dictionary<string, TokenRow> byTokenId = existing.ToDictionary(t => t.TokenId);
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (Token token in instance.Tokens)
            {
                wanted.Add(token.Id);
                if (!byTokenId.TryGetValue(token.Id, out TokenRow tr))
                {
                    tr = new TokenRow { InstanceId = instance.Id, TokenId = token.Id };
                    ctx.Tokens.Add(tr);
                }

                tr.NodeId = token.NodeId;
                tr.Status = (int)token.Status;
                tr.WaitingSignal = token.WaitingSignal;
                tr.DueUtc = token.DueUtc;
                tr.WaitingTarget = token.WaitingTarget;
                tr.WaitingForChildInstanceId = token.WaitingForChildInstanceId;
                // Zweig-Scope: nur gesetzt, solange das Token in einer parallelen Region laeuft - sonst
                // bleibt die Spalte null (und die Zeile so klein wie bisher).
                tr.VariablesJson = token.Variables == null ? null : WorkflowJson.Serialize(token.Variables);
                tr.SplitTokenId = token.SplitTokenId;
                // Denormalisiert, damit die Arbeitsliste eine Abfrage ist und kein Auspacken von JSON:
                // der Tenant kommt von der Instanz (die Token-Zeile hat keinen eigenen Filter), der Rest
                // ist der Aufgaben-Stempel, den die Engine beim Parken setzt und beim Abschluss leert.
                tr.TenantId = row.TenantId;
                tr.TaskKey = token.TaskKey;
                tr.TaskPermission = token.TaskPermission;
                tr.AssignedTo = token.AssignedTo;
                tr.TaskTitle = token.TaskTitle;
                tr.TaskCreatedUtc = token.TaskCreatedUtc;
                tr.TaskDueUtc = token.TaskDueUtc;
                if (token.TaskKey == null)
                {
                    // Die Aufgabe ist weg (erledigt oder es war nie eine) - die weiche Sperre haette sonst
                    // keinen Bezug mehr und wuerde die Zeile in der "wird gerade bearbeitet"-Anzeige halten.
                    // ClaimedBy/ClaimedUntil sind bewusst NICHT Teil des Token-Modells: sie gehoeren der
                    // Oberflaeche, nicht dem Ablauf - ein Zweig-Commit darf sie nicht ueberschreiben.
                    tr.ClaimedBy = null;
                    tr.ClaimedUntil = null;
                }
            }

            foreach (TokenRow tr in existing.Where(t => !wanted.Contains(t.TokenId)))
            {
                ctx.Tokens.Remove(tr);
            }
        }

        /// <inheritdoc/>
        public WorkflowInstance GetInstance(string instanceId)
        {
            using WorkflowContext ctx = contextFactory();
            // .Where statt .Find, damit der strikte Instanz-Tenant-Filter greift (Find umgeht ihn).
            WorkflowInstanceRow row = ctx.WorkflowInstances.FirstOrDefault(r => r.Id == instanceId);
            if (row == null)
            {
                return null;
            }

            List<TokenRow> tokens = ctx.Tokens.Where(t => t.InstanceId == instanceId).ToList();
            List<HistoryEntryRow> history = ctx.HistoryEntries
                .Where(h => h.InstanceId == instanceId).OrderBy(h => h.Seq).ToList();
            return ToInstance(row, tokens, history);
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindWaitingForSignal(string signalName, string correlationKey = null)
        {
            using WorkflowContext ctx = contextFactory();
            int waiting = (int)TokenStatus.Waiting;
            List<string> ids = ctx.Tokens
                .Where(t => t.Status == waiting && t.WaitingSignal == signalName)
                .Select(t => t.InstanceId)
                .Distinct()
                .ToList();

            List<WorkflowInstance> found = LoadInstances(ctx, ids);
            // Korrelation an der Instanz (nicht mehr an der Token-Zeile denormalisiert) - die
            // Kandidatenmenge ist klein, daher in-memory.
            if (correlationKey != null)
            {
                found = found.Where(i => i.CorrelationKey == correlationKey || i.Id == correlationKey).ToList();
            }

            return found;
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindDueTimers(DateTime nowUtc)
        {
            using WorkflowContext ctx = contextFactory();
            int waiting = (int)TokenStatus.Waiting;
            List<string> ids = ctx.Tokens
                .Where(t => t.Status == waiting && t.DueUtc != null && t.DueUtc <= nowUtc)
                .Select(t => t.InstanceId)
                .Distinct()
                .ToList();
            return LoadInstances(ctx, ids);
        }

        /// <inheritdoc/>
        public DateTime? PeekNextTimerDueUtc(DateTime nowUtc)
        {
            using WorkflowContext ctx = contextFactory();
            int waiting = (int)TokenStatus.Waiting;
            // Frueheste kuenftige Timer-Faelligkeit (indizierter MIN-Query ueber die wartenden Timer-Token).
            // Min() ueber DateTime? liefert null, wenn kein passender Token existiert.
            return ctx.Tokens
                .Where(t => t.Status == waiting && t.DueUtc != null && t.DueUtc > nowUtc)
                .Min(t => t.DueUtc);
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindBranchesWaitingForTarget(IEnumerable<string> targets)
        {
            List<string> targetList = (targets ?? Enumerable.Empty<string>()).Distinct().ToList();
            if (targetList.Count == 0)
            {
                return new List<WorkflowInstance>();
            }

            using WorkflowContext ctx = contextFactory();
            int waitingForTarget = (int)TokenStatus.WaitingForTarget;
            List<string> ids = ctx.Tokens
                .Where(t => t.Status == waitingForTarget
                            && t.WaitingTarget != null && targetList.Contains(t.WaitingTarget))
                .Select(t => t.InstanceId)
                .Distinct()
                .ToList();
            return LoadInstances(ctx, ids);
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindRunnable()
        {
            using WorkflowContext ctx = contextFactory();
            int running = (int)WorkflowStatus.Running;
            List<string> ids = ctx.WorkflowInstances
                .Where(r => r.Status == running)
                .Select(r => r.Id)
                .ToList();
            return LoadInstances(ctx, ids);
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindChildInstances(string parentInstanceId)
        {
            if (string.IsNullOrEmpty(parentInstanceId))
            {
                return new List<WorkflowInstance>();
            }

            using WorkflowContext ctx = contextFactory();
            List<string> ids = ctx.WorkflowInstances
                .Where(r => r.ParentInstanceId == parentInstanceId)
                .Select(r => r.Id)
                .ToList();
            return LoadInstances(ctx, ids);
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindFinishedChildrenWithWaitingParent()
        {
            using WorkflowContext ctx = contextFactory();
            int waiting = (int)TokenStatus.Waiting;
            // Von den (wenigen) aktuell wartenden Aufrufer-Tokens ausgehen - nicht von allen je beendeten
            // Kindern (die waechsen unbegrenzt).
            List<string> awaited = ctx.Tokens
                .Where(t => t.Status == waiting && t.WaitingForChildInstanceId != null)
                .Select(t => t.WaitingForChildInstanceId)
                .Distinct()
                .ToList();
            if (awaited.Count == 0)
            {
                return new List<WorkflowInstance>();
            }

            int completed = (int)WorkflowStatus.Completed;
            int faulted = (int)WorkflowStatus.Faulted;
            List<string> finished = ctx.WorkflowInstances
                .Where(r => awaited.Contains(r.Id) && (r.Status == completed || r.Status == faulted))
                .Select(r => r.Id)
                .ToList();
            return LoadInstances(ctx, finished);
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

            // Erst die (tenant-gefilterten) Instanz-Zeilen, dann fuer genau diese in EINER Abfrage die
            // Token-Zeilen laden und gruppieren.
            List<WorkflowInstanceRow> rows = ctx.WorkflowInstances.Where(r => ids.Contains(r.Id)).ToList();
            List<string> foundIds = rows.Select(r => r.Id).ToList();
            Dictionary<string, List<TokenRow>> tokensByInstance = ctx.Tokens
                .Where(t => foundIds.Contains(t.InstanceId))
                .ToList()
                .GroupBy(t => t.InstanceId)
                .ToDictionary(g => g.Key, g => g.ToList());
            Dictionary<string, List<HistoryEntryRow>> historyByInstance = ctx.HistoryEntries
                .Where(h => foundIds.Contains(h.InstanceId))
                .ToList()
                .GroupBy(h => h.InstanceId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Seq).ToList());

            return rows
                .Select(r => ToInstance(r,
                    tokensByInstance.TryGetValue(r.Id, out List<TokenRow> tl) ? tl : new List<TokenRow>(),
                    historyByInstance.TryGetValue(r.Id, out List<HistoryEntryRow> hl) ? hl : new List<HistoryEntryRow>()))
                .ToList();
        }

        private static WorkflowInstance ToInstance(WorkflowInstanceRow row, List<TokenRow> tokenRows,
            List<HistoryEntryRow> historyRows)
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
                ParentInstanceId = row.ParentInstanceId,
                ParentTokenId = row.ParentTokenId,
                RootInstanceId = row.RootInstanceId,
                CallDepth = row.CallDepth,
                Version = row.Version,
                CreatedUtc = row.CreatedUtc,
                UpdatedUtc = row.UpdatedUtc,
                Variables = WorkflowJson.Deserialize<Dictionary<string, object>>(row.VariablesJson)
                            ?? new Dictionary<string, object>(),
                Tokens = tokenRows.Select(t => new Token
                {
                    Id = t.TokenId,
                    NodeId = t.NodeId,
                    Status = (TokenStatus)t.Status,
                    WaitingSignal = t.WaitingSignal,
                    DueUtc = t.DueUtc,
                    WaitingTarget = t.WaitingTarget,
                    WaitingForChildInstanceId = t.WaitingForChildInstanceId,
                    Variables = string.IsNullOrEmpty(t.VariablesJson)
                        ? null
                        : WorkflowJson.Deserialize<Dictionary<string, object>>(t.VariablesJson),
                    SplitTokenId = t.SplitTokenId,
                    TaskKey = t.TaskKey,
                    TaskPermission = t.TaskPermission,
                    AssignedTo = t.AssignedTo,
                    TaskTitle = t.TaskTitle,
                    TaskCreatedUtc = t.TaskCreatedUtc,
                    TaskDueUtc = t.TaskDueUtc
                }).ToList(),
                History = historyRows
                    .OrderBy(h => h.Seq)
                    .Select(h => new HistoryEntry
                    {
                        TimestampUtc = h.TimestampUtc,
                        NodeId = h.NodeId,
                        Event = h.Event,
                        Detail = h.Detail,
                        Severity = (HistorySeverity)h.Severity
                    }).ToList()
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
