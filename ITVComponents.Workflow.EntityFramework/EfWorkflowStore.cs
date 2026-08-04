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
        /// <summary>
        /// Wie oft der Erwerb einer Zweig-Sperre wiederholt wird, wenn der Schluessel beim Einfuegen
        /// belegt war, beim Nachsehen aber schon wieder frei ist.
        /// </summary>
        /// <remarks>
        /// Klein gehalten: das Fenster ist winzig, und ein DAUERHAFT fehlschlagendes Einfuegen ohne
        /// vorhandene Zeile ist ein echter Fehler, der nach wenigen Versuchen gemeldet gehoert - nicht
        /// in einer Schleife versteckt.
        /// </remarks>
        private const int BranchLockAcquireAttempts = 3;

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

            if (definition.IsPublic && !string.IsNullOrEmpty(definition.TenantId))
            {
                // Widerspruch statt Auslegungsfrage: wer beides setzt, hat sich nicht entschieden.
                throw new InvalidOperationException(
                    $"Definition '{definition.Id}' v{definition.Version} is marked public but also names " +
                    $"the tenant '{definition.TenantId}'. Decide one - public means no tenant.");
            }

            using WorkflowContext ctx = contextFactory();

            // Oeffentlich ist eine AUSDRUECKLICHE Entscheidung. Ohne sie gehoert die Definition dem
            // Mandanten des laufenden Kontexts - sonst legte der Editor still oeffentliche Definitionen
            // an, die jeder andere Mandant sieht und starten kann.
            definition.TenantId = definition.IsPublic ? null : definition.TenantId ?? ctx.CurrentTenant;

            // Bewusst OHNE Query-Filter und explizit auf den Tenant der zu speichernden Definition
            // gematcht: das Schreiben soll deterministisch die richtige Zeile treffen, unabhaengig vom
            // gerade aktiven Tenant-Kontext (sonst koennte der Filter die zu aktualisierende Zeile
            // verstecken und ein Duplikat/Schluesselkonflikt entstehen).
            WorkflowDefinitionRow row = definition.Key != 0
                ? ctx.WorkflowDefinitions.IgnoreQueryFilters()
                    .FirstOrDefault(d => d.DefinitionKey == definition.Key)
                : ctx.WorkflowDefinitions.IgnoreQueryFilters()
                    .FirstOrDefault(d => d.Id == definition.Id && d.Version == definition.Version
                                         && d.TenantId == definition.TenantId);
            if (row == null)
            {
                row = new WorkflowDefinitionRow { Id = definition.Id, Version = definition.Version };
                ctx.WorkflowDefinitions.Add(row);
            }
            else
            {
                // Beim Aktualisieren duerfen Name und Version mitwandern - die technische Kennung nicht.
                // An ihr haengen die laufenden Instanzen.
                row.Id = definition.Id;
                row.Version = definition.Version;
            }

            row.TenantId = definition.TenantId;
            // Die Knoten-Polymorphie steckt in den Diskriminator-Attributen - das JSON bleibt frei
            // von .NET-Typnamen.
            row.DefinitionJson = WorkflowJson.Serialize(definition);
            ctx.SaveChanges();
            definition.Key = row.DefinitionKey;
        }

        /// <inheritdoc/>
        public WorkflowDefinition GetDefinition(string definitionId, int? version = null,
            string tenantId = null)
        {
            using WorkflowContext ctx = contextFactory();

            // Ohne genannten Mandanten gilt der Query-Filter des Kontexts (eigener Tenant ODER
            // oeffentlich); mit genanntem wird die Sicht dieses Mandanten ausdruecklich hergestellt -
            // dafuer muss der Filter weg, sonst gaelte weiterhin der gerade aktive.
            IQueryable<WorkflowDefinitionRow> q = tenantId == null
                ? ctx.WorkflowDefinitions
                : ctx.WorkflowDefinitions.IgnoreQueryFilters()
                    .Where(d => d.TenantId == tenantId || d.TenantId == null);

            q = q.Where(d => d.Id == definitionId);
            if (version.HasValue)
            {
                q = q.Where(d => d.Version == version.Value);
            }

            // Die EIGENE Definition des Mandanten schlaegt die oeffentliche gleichen Namens - eine
            // mandanteneigene Fassung ist die Verfeinerung und soll die allgemeine ueberdecken. Ohne
            // diese Regel entschiede die Reihenfolge der Datenbank, also der Zufall.
            WorkflowDefinitionRow row = q
                .OrderByDescending(d => d.Version)
                .ThenByDescending(d => d.TenantId == null ? 0 : 1)
                .FirstOrDefault();

            return Materialize(row);
        }

        /// <inheritdoc/>
        public WorkflowDefinition GetDefinition(int definitionKey)
        {
            using WorkflowContext ctx = contextFactory();
            // Bewusst OHNE Query-Filter: eine laufende Instanz eines OEFFENTLICHEN Workflows muss ihre
            // Definition auch dann laden koennen, wenn gerade ein anderer Mandant aktiv ist (Runner,
            // Hintergrund-Abarbeitung). Die Zugriffsentscheidung faellt am Verweis, nicht hier - die
            // Instanz selbst ist mandanten-gefiltert.
            WorkflowDefinitionRow row = ctx.WorkflowDefinitions
                .IgnoreQueryFilters()
                .FirstOrDefault(d => d.DefinitionKey == definitionKey);
            return Materialize(row);
        }

        /// <summary>
        /// Baut die Definition aus der Zeile und setzt die technische Kennung nach - sie steht in der
        /// SPALTE, nicht im JSON (dort waere sie eine Zahl, die nur in dieser einen Ablage gilt).
        /// </summary>
        private static WorkflowDefinition Materialize(WorkflowDefinitionRow row)
        {
            if (row == null)
            {
                return null;
            }

            WorkflowDefinition definition = WorkflowJson.Deserialize<WorkflowDefinition>(row.DefinitionJson);
            if (definition != null)
            {
                definition.Key = row.DefinitionKey;
                definition.TenantId = row.TenantId;
                definition.IsPublic = row.TenantId == null;
            }

            return definition;
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
            row.Priority = instance.Priority;
            row.CorrelationKey = instance.CorrelationKey;
            row.FaultMessage = instance.FaultMessage;
            row.ParentInstanceId = instance.ParentInstanceId;
            row.ParentTokenId = instance.ParentTokenId;
            row.RootInstanceId = instance.EffectiveRootInstanceId;
            row.CallDepth = instance.CallDepth;
            row.CreatedUtc = instance.CreatedUtc;
            row.UpdatedUtc = instance.UpdatedUtc;
            row.DefinitionKey = instance.DefinitionKey;
            row.VariablesJson = WorkflowJson.SerializeVariables(instance.Variables);
            row.CompensationsJson = WorkflowJson.SerializeCompensations(instance.Compensations);

            // Die vorgemerkten Nachrichten - im SELBEN SaveChanges wie die Instanz. Genau das ist die
            // Zustell-Garantie: der Zweig und seine ausgehende Nachricht werden zusammen wirksam oder
            // gar nicht. Append-only: zugestellte Vormerkungen streicht CompleteOutgoingMessage.
            var persistedMessages = new HashSet<string>(
                ctx.Outbox.Where(o => o.InstanceId == instance.Id).Select(o => o.Id),
                StringComparer.Ordinal);
            foreach (OutgoingMessage message in instance.OutgoingMessages)
            {
                if (persistedMessages.Contains(message.Id))
                {
                    continue;
                }

                ctx.Outbox.Add(new WorkflowOutboxRow
                {
                    InstanceId = instance.Id,
                    Id = message.Id,
                    SignalName = message.SignalName,
                    CorrelationKey = message.CorrelationKey,
                    Broadcast = message.Broadcast,
                    TargetInstanceId = message.TargetInstanceId,
                    PayloadJson = message.Payload == null
                        ? null
                        : WorkflowJson.SerializeVariables(message.Payload),
                    WaitingTokenId = message.WaitingTokenId,
                    ReachedVariable = message.ReachedVariable,
                    TenantId = row.TenantId,
                    CreatedUtc = message.CreatedUtc,
                    Attempts = message.Attempts
                });
            }

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
                // Der Timer-Anspruch gehoert dem AUFGRIFF, nicht dem Token: schreibt die Engine diese
                // Zeile, ist der Aufgriff vorbei - der Timer hat gefeuert oder steht auf einer neuen
                // Frist. Bliebe der Stempel liegen, waere ein neu gestellter Fristen-Timer bis zum
                // Ablauf des ALTEN Anspruchs fuer jeden Runner unsichtbar (er feuerte also verspaetet).
                tr.TimerLeaseOwner = null;
                tr.TimerLeaseUntilUtc = null;
                tr.WaitingTarget = token.WaitingTarget;
                tr.WaitingForChildInstanceId = token.WaitingForChildInstanceId;
                // Zweig-Scope: nur gesetzt, solange das Token in einer parallelen Region laeuft - sonst
                // bleibt die Spalte null (und die Zeile so klein wie bisher).
                tr.VariablesJson = token.Variables == null ? null : WorkflowJson.SerializeVariables(token.Variables);
                tr.SplitTokenId = token.SplitTokenId;
                tr.BoundaryOwnerTokenId = token.BoundaryOwnerTokenId;
                tr.BoundaryIteration = token.BoundaryIteration;
                tr.RaceTokenId = token.RaceTokenId;
                tr.WaitingCorrelation = token.WaitingCorrelation;
                tr.WaitingKind = (int?)token.WaitingKind;
                tr.ArrivedViaFlowId = token.ArrivedViaFlowId;
                tr.SubProcessOwnerTokenId = token.SubProcessOwnerTokenId;
                tr.SplitBranchCount = token.SplitBranchCount;
                tr.CompensationOwnerTokenId = token.CompensationOwnerTokenId;
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
            List<WorkflowOutboxRow> outbox = ctx.Outbox
                .IgnoreQueryFilters()
                .Where(o => o.InstanceId == instanceId).ToList();
            return ToInstance(row, tokens, history, outbox);
        }

        /// <inheritdoc/>
        public int? GetInstancePriority(string instanceId)
        {
            using WorkflowContext ctx = contextFactory();
            // Eine Spalte, eine Zeile - der Punkt der Methode. Der Cast auf int? unterscheidet
            // "gibt es nicht" von "steht auf 0" (0 waere die HOECHSTE Stufe).
            return ctx.WorkflowInstances
                .Where(r => r.Id == instanceId)
                .Select(r => (int?)r.Priority)
                .FirstOrDefault();
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindWaitingForSignal(string signalName, string correlationKey = null)
        {
            using WorkflowContext ctx = contextFactory();
            int waiting = (int)TokenStatus.Waiting;

            // Der Schluessel am WARTEPUNKT wird in der Datenbank gefiltert - er ist der spezifischere und
            // trennt die Kandidaten am staerksten. Der Schluessel an der Instanz bleibt der Rueckfall fuer
            // Wartepunkte ohne eigenen (die Kandidatenmenge ist dann klein, daher in-memory).
            IQueryable<TokenRow> candidates = ctx.Tokens
                .Where(t => t.Status == waiting && t.WaitingSignal == signalName);
            if (correlationKey != null)
            {
                candidates = candidates.Where(t => t.WaitingCorrelation == correlationKey
                                                   || t.WaitingCorrelation == null);
            }

            List<string> ids = candidates.Select(t => t.InstanceId).Distinct().ToList();
            List<WorkflowInstance> found = LoadInstances(ctx, ids);
            if (correlationKey != null)
            {
                found = found
                    .Where(i => i.Tokens.Any(t => t.Status == TokenStatus.Waiting
                                                  && t.WaitingSignal == signalName
                                                  && (t.WaitingCorrelation == correlationKey
                                                      || (t.WaitingCorrelation == null
                                                          && (i.CorrelationKey == correlationKey
                                                              || i.Id == correlationKey)))))
                    .ToList();
            }

            return found;
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindWaitingForBroadcast(string signalName)
        {
            using WorkflowContext ctx = contextFactory();
            int waiting = (int)TokenStatus.Waiting;
            int broadcast = (int)Model.WaitKind.Signal;
            // Die Auswahl gehoert in die Datenbank: ein Rundruf kann tausende Instanzen betreffen, und
            // ohne die Art an der Token-Zeile muesste fuer jede erst die Definition geladen werden.
            List<string> ids = ctx.Tokens
                .Where(t => t.Status == waiting && t.WaitingSignal == signalName && t.WaitingKind == broadcast)
                .Select(t => t.InstanceId)
                .Distinct()
                .ToList();
            return LoadInstances(ctx, ids);
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
        /// <remarks>
        /// Drei Schritte statt eines Roh-SQL-Befehls (<c>OUTPUT inserted</c> / <c>FOR UPDATE SKIP
        /// LOCKED</c>): Kandidaten waehlen, stempeln, das Gestempelte zurueckholen. Der mittlere Schritt
        /// ist der entscheidende - <c>ExecuteUpdate</c> prueft die Bedingung beim Ausfuehren erneut und
        /// ist pro Zeile atomar; wer verliert, aktualisiert schlicht nichts. Damit bleibt der Store
        /// provider-neutral und braucht keine Sonderfassung je Datenbank.
        /// </remarks>
        public IEnumerable<WorkflowInstance> ClaimDueTimers(DateTime nowUtc, string owner, TimeSpan lease,
            int maxInstances)
        {
            if (string.IsNullOrEmpty(owner))
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (maxInstances <= 0)
            {
                return new List<WorkflowInstance>();
            }

            using WorkflowContext ctx = contextFactory();
            int waiting = (int)TokenStatus.Waiting;
            DateTime until = nowUtc.Add(lease);
            string claim = owner + "#" + Guid.NewGuid().ToString("N");

            // 1. Kandidaten: faellige Timer, die niemand (mehr) beansprucht. Erst nach Dringlichkeit der
            //    Instanz, dann nach der aeltesten Faelligkeit je Instanz - damit bei einem Stau die
            //    wichtigen zuerst drankommen und innerhalb einer Stufe die am laengsten ueberfaelligen,
            //    statt dass eine Instanz dauerhaft hinten liegen bleibt. Die Reihenfolge zaehlt hier
            //    wirklich: maxInstances schneidet ab, was dieser Poll NICHT mehr aufgreift.
            List<string> candidates = ctx.Tokens
                .Where(t => t.Status == waiting && t.DueUtc != null && t.DueUtc <= nowUtc
                            && (t.TimerLeaseUntilUtc == null || t.TimerLeaseUntilUtc <= nowUtc))
                .GroupBy(t => t.InstanceId)
                .Select(g => new { InstanceId = g.Key, Due = g.Min(t => t.DueUtc) })
                .Join(ctx.WorkflowInstances, x => x.InstanceId, r => r.Id,
                    (x, r) => new { x.InstanceId, x.Due, r.Priority })
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Due)
                .Take(maxInstances)
                .Select(x => x.InstanceId)
                .ToList();
            if (candidates.Count == 0)
            {
                return new List<WorkflowInstance>();
            }

            // 2. Stempeln. Die Bedingung steht hier ein zweites Mal - genau darin liegt der Ausschluss:
            //    zwischen Auswahl und Update kann ein anderer Runner dieselben Zeilen genommen haben,
            //    dann trifft dieses Update sie nicht mehr.
            _ = ctx.Tokens
                .Where(t => candidates.Contains(t.InstanceId)
                            && t.Status == waiting && t.DueUtc != null && t.DueUtc <= nowUtc
                            && (t.TimerLeaseUntilUtc == null || t.TimerLeaseUntilUtc <= nowUtc))
                .ExecuteUpdate(s => s
                    .SetProperty(t => t.TimerLeaseOwner, claim)
                    .SetProperty(t => t.TimerLeaseUntilUtc, until));

            // 3. Was DIESER Aufruf bekommen hat - erkennbar an der Aufruf-Guid. Nur dessen Instanzen
            //    werden geladen; das ist die eigentliche Ersparnis gegenueber FindDueTimers.
            List<string> ids = ctx.Tokens
                .Where(t => t.TimerLeaseOwner == claim)
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
            // Die Sortierung nach Dringlichkeit macht LoadInstances fuer alle Abfragen gemeinsam.
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

            for (int attempt = 1; ; attempt++)
            {
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

                    // Der Schluessel war beim Einfuegen belegt, ist es beim Nachsehen aber nicht mehr:
                    // der Besitzer hat GENAU DAZWISCHEN freigegeben. Das ist Nebenlaeufigkeit, kein
                    // Fehler - und bei kurzen Zweig-Schritten ein Fenster, das im Betrieb regelmaessig
                    // trifft. Ohne den erneuten Versuch faellt es als "unerwarteter Fehler" auf, der
                    // Zweig-Auftrag scheitert, und im Log steht ein PK-Verstoss, der wie ein
                    // Datenbank-Problem aussieht.
                    if (attempt < BranchLockAcquireAttempts)
                    {
                        LogEnvironment.LogEvent(
                            $"Branch lock for instance '{instanceId}' token '{tokenId}' was released " +
                            $"between the failed insert and the check - retrying (attempt {attempt} of " +
                            $"{BranchLockAcquireAttempts}).", LogSeverity.Report);
                        continue;
                    }

                    LogEnvironment.LogEvent(
                        $"Unexpected error acquiring branch lock for instance '{instanceId}' token " +
                        $"'{tokenId}' after {attempt} attempts: {ex.OutlineException()}", LogSeverity.Error);
                    throw;
                }
            }
        }

        /// <summary>Baut aus einer Outbox-Zeile die Vormerkung.</summary>
        private static OutgoingMessage ToMessage(WorkflowOutboxRow row) => new OutgoingMessage
        {
            Id = row.Id,
            InstanceId = row.InstanceId,
            SignalName = row.SignalName,
            CorrelationKey = row.CorrelationKey,
            Broadcast = row.Broadcast,
            TargetInstanceId = row.TargetInstanceId,
            Payload = string.IsNullOrEmpty(row.PayloadJson)
                ? null
                : WorkflowJson.DeserializeVariables(row.PayloadJson),
            WaitingTokenId = row.WaitingTokenId,
            ReachedVariable = row.ReachedVariable,
            CreatedUtc = row.CreatedUtc,
            Attempts = row.Attempts
        };

        /// <inheritdoc/>
        public IReadOnlyList<OutgoingMessage> ClaimOutgoingMessages(string owner, TimeSpan lease,
            int maxMessages)
        {
            if (string.IsNullOrEmpty(owner))
            {
                throw new ArgumentNullException(nameof(owner));
            }

            using WorkflowContext ctx = contextFactory();
            DateTime now = DateTime.UtcNow;

            // Bewusst OHNE Tenant-Filter: der Nachhol-Lauf gehoert einem tenant-uebergreifenden Runner.
            // Die eigentliche Zustellung laeuft danach im Store-Kontext des Aufrufers.
            List<WorkflowOutboxRow> rows = ctx.Outbox
                .IgnoreQueryFilters()
                .Where(o => o.ClaimedUntil == null || o.ClaimedUntil < now)
                .OrderBy(o => o.CreatedUtc)
                .Take(maxMessages)
                .ToList();
            if (rows.Count == 0)
            {
                return Array.Empty<OutgoingMessage>();
            }

            foreach (WorkflowOutboxRow row in rows)
            {
                row.ClaimedBy = owner;
                row.ClaimedUntil = now.Add(lease);
                row.Attempts++;
            }

            ctx.SaveChanges();
            return rows.Select(ToMessage).ToList();
        }

        /// <inheritdoc/>
        public void CompleteOutgoingMessage(string instanceId, string messageId)
        {
            using WorkflowContext ctx = contextFactory();
            ctx.Outbox
                .IgnoreQueryFilters()
                .Where(o => o.InstanceId == instanceId && o.Id == messageId)
                .ExecuteDelete();
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

            // Dazu die eigenen Timer-Ansprueche: sie tragen den Owner als Praefix vor der Aufruf-Guid
            // (siehe TokenRow.TimerLeaseOwner). Sie liefen zwar von selbst ab - aber ein gerade
            // gestarteter Runner soll die liegengebliebene Arbeit sofort aufholen koennen und nicht
            // erst vor seinen eigenen Leichen warten.
            string prefix = owner + "#";
            ctx.Tokens
                .Where(t => t.TimerLeaseOwner != null && t.TimerLeaseOwner.StartsWith(prefix))
                .ExecuteUpdate(s => s
                    .SetProperty(t => t.TimerLeaseOwner, (string)null)
                    .SetProperty(t => t.TimerLeaseUntilUtc, (DateTime?)null));
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
            // Ohne Tenant-Filter: die Vormerkung gehoert derselben Instanz, die oben schon gefiltert
            // wurde - ein zweiter Filter wuerde sie nur dann verstecken, wenn gerade ein anderer Tenant
            // aktiv ist (Runner), und die Nachricht ginge still verloren.
            Dictionary<string, List<WorkflowOutboxRow>> outboxByInstance = ctx.Outbox
                .IgnoreQueryFilters()
                .Where(o => foundIds.Contains(o.InstanceId))
                .ToList()
                .GroupBy(o => o.InstanceId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Die dringendsten zuerst (kleinere Zahl = wichtiger): jeder Aufrufer reiht in dieser
            // Reihenfolge ein, und wer nur einen Teil verarbeitet, hat wenigstens den richtigen Teil.
            // OrderBy ist stabil - innerhalb einer Stufe bleibt es bei der Reihenfolge der Datenbank.
            return rows
                .OrderBy(r => r.Priority)
                .Select(r => ToInstance(r,
                    tokensByInstance.TryGetValue(r.Id, out List<TokenRow> tl) ? tl : new List<TokenRow>(),
                    historyByInstance.TryGetValue(r.Id, out List<HistoryEntryRow> hl) ? hl : new List<HistoryEntryRow>(),
                    outboxByInstance.TryGetValue(r.Id, out List<WorkflowOutboxRow> ol) ? ol : new List<WorkflowOutboxRow>()))
                .ToList();
        }

        private static WorkflowInstance ToInstance(WorkflowInstanceRow row, List<TokenRow> tokenRows,
            List<HistoryEntryRow> historyRows, List<WorkflowOutboxRow> outboxRows)
        {
            return new WorkflowInstance
            {
                Id = row.Id,
                DefinitionId = row.DefinitionId,
                DefinitionVersion = row.DefinitionVersion,
                TenantId = row.TenantId,
                Status = (WorkflowStatus)row.Status,
                Priority = row.Priority,
                CorrelationKey = row.CorrelationKey,
                FaultMessage = row.FaultMessage,
                ParentInstanceId = row.ParentInstanceId,
                ParentTokenId = row.ParentTokenId,
                RootInstanceId = row.RootInstanceId,
                CallDepth = row.CallDepth,
                Version = row.Version,
                CreatedUtc = row.CreatedUtc,
                UpdatedUtc = row.UpdatedUtc,
                DefinitionKey = row.DefinitionKey,
                Variables = WorkflowJson.DeserializeVariables(row.VariablesJson),
                Compensations = WorkflowJson.DeserializeCompensations(row.CompensationsJson),
                OutgoingMessages = outboxRows.Select(ToMessage).ToList(),
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
                        : WorkflowJson.DeserializeVariables(t.VariablesJson),
                    SplitTokenId = t.SplitTokenId,
                    BoundaryOwnerTokenId = t.BoundaryOwnerTokenId,
                    BoundaryIteration = t.BoundaryIteration,
                    RaceTokenId = t.RaceTokenId,
                    WaitingCorrelation = t.WaitingCorrelation,
                    WaitingKind = (Model.WaitKind?)t.WaitingKind,
                    ArrivedViaFlowId = t.ArrivedViaFlowId,
                    SubProcessOwnerTokenId = t.SubProcessOwnerTokenId,
                    SplitBranchCount = t.SplitBranchCount,
                    CompensationOwnerTokenId = t.CompensationOwnerTokenId,
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
