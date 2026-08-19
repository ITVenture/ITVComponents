using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Logging;
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

        private readonly ConcurrentDictionary<string, int> versions = new ConcurrentDictionary<string, int>();

        /// <summary>Die materialisierten Ausloeser, nach ihrem technischen Schluessel.</summary>
        private readonly ConcurrentDictionary<int, WorkflowStartTrigger> triggers =
            new ConcurrentDictionary<int, WorkflowStartTrigger>();

        /// <summary>Der Zaehler fuer die Ausloeser-Schluessel - wie bei den Definitionen.</summary>
        private int nextTriggerKey;

        /// <summary>
        /// Die Aktivierungen, nach ihrem technischen Schluessel. Sie ueberdauern das Neuaufbauen der
        /// Ausloeser - deshalb eine eigene Ablage und keine Liste am Ausloeser.
        /// </summary>
        private readonly ConcurrentDictionary<int, WorkflowStartTriggerActivation> activations =
            new ConcurrentDictionary<int, WorkflowStartTriggerActivation>();

        /// <summary>Der Zaehler fuer die Aktivierungs-Schluessel.</summary>
        private int nextActivationKey;

        /// <summary>
        /// Der Zaehler fuer die technischen Kennungen. Auch die Ablage im Speicher vergibt sie - sonst
        /// verhielte sie sich anders als eine echte Ablage, und ein Test wuerde beweisen, was im Betrieb
        /// nicht gilt.
        /// </summary>
        private int nextDefinitionKey;

        /// <inheritdoc/>
        public void SaveDefinition(WorkflowDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.IsPublic && !string.IsNullOrEmpty(definition.TenantId))
            {
                throw new InvalidOperationException(
                    $"Definition '{definition.Id}' v{definition.Version} is marked public but also names " +
                    $"the tenant '{definition.TenantId}'. Decide one - public means no tenant.");
            }

            if (definition.IsPublic)
            {
                definition.TenantId = null;
            }

            string key = Key(definition.Id, definition.Version, definition.TenantId);
            if (definitions.TryGetValue(key, out WorkflowDefinition existing))
            {
                // Aktualisieren: die technische Kennung bleibt, worauf verwiesen wird, aendert sich nicht.
                definition.Key = existing.Key;
            }
            else if (definition.Key == 0)
            {
                definition.Key = Interlocked.Increment(ref nextDefinitionKey);
            }

            definitions[key] = definition;
            SyncTriggers(definition);
        }

        /// <summary>
        /// Baut die Ausloeser dieser Definition neu auf - nur fuer die HOECHSTE Version, damit nicht jede
        /// alte Fassung weiter mitfeuert.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Die <b>Aktivierungen</b> bleiben dabei stehen: sie haengen an der fachlichen Identitaet des
        /// Ausloesers, nicht an seiner Zeilennummer, und ueberdauern deshalb jedes Speichern. Genau
        /// deshalb liegt der Lauf-Zustand bei ihnen und nicht hier - sonst finge jedes Speichern der
        /// Definition, auch eine Aenderung an ganz anderer Stelle, den Zeitplan von vorne an.
        /// </para>
        /// <para>
        /// Eine <b>mandanteneigene</b> Definition bekommt ihre eine Aktivierung von selbst: fuer sie ist
        /// "wer faehrt das?" keine Frage. Eine oeffentliche bekommt keine - dort ist es eine.
        /// </para>
        /// </remarks>
        private void SyncTriggers(WorkflowDefinition definition)
        {
            int highest = definitions.Values
                .Where(d => d.Id == definition.Id && d.TenantId == definition.TenantId)
                .Select(d => d.Version)
                .DefaultIfEmpty(definition.Version)
                .Max();
            WorkflowDefinition newest = highest == definition.Version
                ? definition
                : definitions.Values.First(d => d.Id == definition.Id && d.TenantId == definition.TenantId
                                                && d.Version == highest);

            var kept = new List<WorkflowStartTrigger>();
            foreach (WorkflowStartTrigger fresh in WorkflowStartTriggerFactory.FromDefinition(newest))
            {
                WorkflowStartTrigger previous = triggers.Values.FirstOrDefault(
                    t => t.TenantId == fresh.TenantId && t.DefinitionId == fresh.DefinitionId
                         && t.NodeId == fresh.NodeId && t.Kind == fresh.Kind);

                fresh.TriggerKey = previous?.TriggerKey ?? Interlocked.Increment(ref nextTriggerKey);
                kept.Add(fresh);

                if (fresh.Kind == WorkflowStartTriggerKind.Schedule
                    && previous != null && previous.Pattern != fresh.Pattern)
                {
                    // Ein umgeschriebener Zeitplan ist ein ANDERER Plan, und sein erster Lauf gehoert
                    // ihm - sonst greift ein "sofort"-Kennzeichen nie, weil "schon mal gelaufen" aus der
                    // Zeit davor stammt. Betroffen sind nur die, die auch wirklich auf dem zentralen
                    // Muster laufen.
                    ResetActivationsAfterPatternChange(fresh);
                }
            }

            foreach (WorkflowStartTrigger stale in triggers.Values
                         .Where(t => t.TenantId == definition.TenantId && t.DefinitionId == definition.Id
                                     && kept.All(k => k.TriggerKey != t.TriggerKey))
                         .ToList())
            {
                WarnAboutOrphans(stale);
                triggers.TryRemove(stale.TriggerKey, out _);
            }

            foreach (WorkflowStartTrigger trigger in kept)
            {
                triggers[trigger.TriggerKey] = trigger;
                EnsureOwnActivation(trigger);
            }
        }

        /// <summary>
        /// Legt fuer eine <b>mandanteneigene</b> Definition die Aktivierung ihres eigenen Mandanten an,
        /// falls es sie noch nicht gibt. Fuer eine oeffentliche geschieht nichts - dort entscheidet der
        /// Mandant selbst.
        /// </summary>
        private void EnsureOwnActivation(WorkflowStartTrigger trigger)
        {
            // Ueber IsPublic und NICHT ueber "TenantId ist null": in einem Ein-Mandanten-Host traegt
            // alles null, und dort muss die Aktivierung sehr wohl entstehen - sonst laeuft nach diesem
            // Umbau gar kein Zeitplan mehr.
            if (trigger.IsPublic)
            {
                return; // Oeffentlich: wer sie faehrt, entscheidet der Mandant selbst.
            }

            if (FindActivation(trigger, trigger.TenantId) != null)
            {
                return;
            }

            SaveActivation(new WorkflowStartTriggerActivation
            {
                OwnerTenantId = trigger.TenantId,
                DefinitionId = trigger.DefinitionId,
                NodeId = trigger.NodeId,
                Kind = trigger.Kind,
                TenantId = trigger.TenantId,
                Enabled = true,
                NextDueUtc = trigger.Kind != WorkflowStartTriggerKind.Schedule
                    ? null
                    : WorkflowStartTriggerFactory.FirstDueUtc(trigger.Pattern, DateTime.UtcNow,
                        $"'{trigger.DefinitionId}', Knoten '{trigger.NodeId}'"),
                ActivatedBy = "(automatisch)",
                ActivatedUtc = DateTime.UtcNow
            });
        }

        /// <summary>Setzt den Lauf-Zustand der Aktivierungen zurueck, die dem zentralen Muster folgen.</summary>
        private void ResetActivationsAfterPatternChange(WorkflowStartTrigger trigger)
        {
            foreach (WorkflowStartTriggerActivation activation in ActivationsOf(trigger))
            {
                bool followsOwnPattern = trigger.AllowReschedule
                                         && !string.IsNullOrWhiteSpace(activation.PatternOverride);
                if (followsOwnPattern)
                {
                    continue;
                }

                activation.LastRunUtc = null;
                activation.LastInstanceId = null;
                activation.NextDueUtc = WorkflowStartTriggerFactory.FirstDueUtc(trigger.Pattern,
                    DateTime.UtcNow,
                    $"'{trigger.DefinitionId}', Knoten '{trigger.NodeId}', Mandant "
                    + $"'{activation.TenantId ?? "-"}'");
            }
        }

        /// <summary>
        /// Sagt, welche Aktivierungen durch das Wegfallen eines Ausloesers ins Leere laufen. Sie werden
        /// NICHT geloescht - die Zustimmung bleibt, falls der Knoten zurueckkommt -, aber ein Zeitplan,
        /// der ab jetzt schweigt, darf das nicht unbemerkt tun.
        /// </summary>
        private void WarnAboutOrphans(WorkflowStartTrigger stale)
        {
            var affected = ActivationsOf(stale).Where(a => a.Enabled).Select(a => a.TenantId ?? "-").ToList();
            if (affected.Count == 0)
            {
                return;
            }

            LogEnvironment.LogEvent(
                $"Der Ausloeser '{stale.DefinitionId}' (Knoten '{stale.NodeId}', {stale.Kind}) ist mit dem "
                + $"Speichern der Definition weggefallen. {affected.Count} aktive Uebernahme(n) laufen ab "
                + $"jetzt ins Leere: {string.Join(", ", affected)}.", LogSeverity.Warning);
        }

        /// <summary>Die Aktivierungen eines Ausloesers - ueber seine fachliche Identitaet.</summary>
        private List<WorkflowStartTriggerActivation> ActivationsOf(WorkflowStartTrigger trigger)
        {
            return activations.Values
                .Where(a => a.OwnerTenantId == trigger.TenantId && a.DefinitionId == trigger.DefinitionId
                            && a.NodeId == trigger.NodeId && a.Kind == trigger.Kind)
                .ToList();
        }

        /// <summary>Die Aktivierung eines Ausloesers fuer EINEN Mandanten, oder null.</summary>
        private WorkflowStartTriggerActivation FindActivation(WorkflowStartTrigger trigger, string tenantId)
        {
            return ActivationsOf(trigger).FirstOrDefault(a => a.TenantId == tenantId);
        }

        /// <summary>Der Ausloeser zu einer Aktivierung, oder null (verwaist).</summary>
        private WorkflowStartTrigger TriggerOf(WorkflowStartTriggerActivation activation)
        {
            return triggers.Values.FirstOrDefault(
                t => t.TenantId == activation.OwnerTenantId && t.DefinitionId == activation.DefinitionId
                     && t.NodeId == activation.NodeId && t.Kind == activation.Kind);
        }

        /// <inheritdoc/>
        public WorkflowDefinition GetDefinition(string definitionId, int? version = null,
            string tenantId = null)
        {
            // Die eigene Definition des Mandanten schlaegt die oeffentliche - eine mandanteneigene
            // Fassung ist die Verfeinerung und soll die allgemeine ueberdecken.
            IEnumerable<WorkflowDefinition> candidates = definitions.Values
                .Where(d => d.Id == definitionId
                            && (d.TenantId == tenantId || string.IsNullOrEmpty(d.TenantId)));
            if (version.HasValue)
            {
                candidates = candidates.Where(d => d.Version == version.Value);
            }

            return candidates
                .OrderByDescending(d => d.Version)
                .ThenByDescending(d => string.IsNullOrEmpty(d.TenantId) ? 0 : 1)
                .FirstOrDefault();
        }

        /// <inheritdoc/>
        public WorkflowDefinition GetDefinition(int definitionKey)
            => definitions.Values.FirstOrDefault(d => d.Key == definitionKey);

        /// <inheritdoc/>
        public void SaveInstance(WorkflowInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            instance.UpdatedUtc = DateTime.UtcNow;
            // Force-Write: Version fortschreiben (neu = 0, bestehend = +1).
            instance.Version = versions.AddOrUpdate(instance.Id, 0, (_, old) => old + 1);
            instances[instance.Id] = instance;
        }

        /// <inheritdoc/>
        public bool TryCommitInstance(WorkflowInstance instance, int baseVersion)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            // Atomarer Compare-and-Swap auf der Version: nur wenn der Stand noch baseVersion ist.
            if (!versions.TryUpdate(instance.Id, baseVersion + 1, baseVersion))
            {
                return false;
            }

            instance.UpdatedUtc = DateTime.UtcNow;
            instance.Version = baseVersion + 1;
            instances[instance.Id] = instance;
            return true;
        }

        /// <inheritdoc/>
        public WorkflowInstance GetInstance(string instanceId)
        {
            return instanceId != null && instances.TryGetValue(instanceId, out WorkflowInstance instance)
                ? instance
                : null;
        }

        /// <inheritdoc/>
        public int? GetInstancePriority(string instanceId)
        {
            return GetInstance(instanceId)?.Priority;
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindWaitingForSignal(string signalName, string correlationKey = null)
        {
            return instances.Values
                .Where(i => i.Status == WorkflowStatus.Waiting)
                .Where(i => i.WaitingTokens.Any(t => t.WaitingSignal == signalName
                                                     && Correlates(i, t, correlationKey)))
                .ToList();
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindWaitingForBroadcast(string signalName)
        {
            return instances.Values
                .Where(i => i.Status == WorkflowStatus.Waiting)
                .Where(i => i.WaitingTokens.Any(t => t.WaitingSignal == signalName
                                                     && t.WaitingKind == Model.WaitKind.Signal))
                .ToList();
        }

        /// <summary>
        /// Passt der Schluessel zu diesem Wartepunkt? Der Schluessel am Token schlaegt den der Instanz -
        /// dieselbe Regel wie in der Engine.
        /// </summary>
        private static bool Correlates(WorkflowInstance instance, Token token, string correlationKey)
        {
            if (correlationKey == null)
            {
                return true;
            }

            return token.WaitingCorrelation != null
                ? token.WaitingCorrelation == correlationKey
                : instance.CorrelationKey == correlationKey || instance.Id == correlationKey;
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindDueTimers(DateTime nowUtc)
        {
            return instances.Values
                .Where(i => i.Status == WorkflowStatus.Waiting && !i.Suspended)
                .Where(i => i.WaitingTokens.Any(t => t.DueUtc.HasValue && t.DueUtc.Value <= nowUtc))
                .OrderBy(i => i.Priority)
                .ToList();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Ohne verteilte Sicht gibt es nichts zu beanspruchen: dieser Store lebt in EINEM Prozess und
        /// liefert dieselben Objekt-Referenzen an alle Aufrufer - ein Stempel haette darauf keine
        /// Wirkung. Liefert daher schlicht die faelligen Instanzen (auf <paramref name="maxInstances"/>
        /// begrenzt). Dass ein Timer trotzdem genau einmal feuert, sichert hier wie ueberall der
        /// Versions-Check beim Commit.
        /// </remarks>
        public IEnumerable<WorkflowInstance> ClaimDueTimers(DateTime nowUtc, string owner, TimeSpan lease,
            int maxInstances)
        {
            return maxInstances <= 0
                ? new List<WorkflowInstance>()
                : FindDueTimers(nowUtc).Take(maxInstances).ToList();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Ohne verteilte Sicht gibt es nichts zu beanspruchen: was noch vorgemerkt ist, ist liegen
        /// geblieben und wird geliefert.
        /// </remarks>
        public IReadOnlyList<OutgoingMessage> ClaimOutgoingMessages(string owner, TimeSpan lease,
            int maxMessages)
        {
            var result = new List<OutgoingMessage>();
            foreach (WorkflowInstance instance in instances.Values)
            {
                foreach (OutgoingMessage message in instance.OutgoingMessages.ToList())
                {
                    message.InstanceId = instance.Id;
                    message.Attempts++;
                    result.Add(message);
                    if (result.Count >= maxMessages)
                    {
                        return result;
                    }
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public void CompleteOutgoingMessage(string instanceId, string messageId)
        {
            if (instances.TryGetValue(instanceId, out WorkflowInstance instance))
            {
                instance.OutgoingMessages.RemoveAll(m => m.Id == messageId);
            }
        }

        /// <inheritdoc/>
        public DateTime? PeekNextTimerDueUtc(DateTime nowUtc)
        {
            var future = instances.Values
                .SelectMany(i => i.WaitingTokens)
                .Where(t => t.DueUtc.HasValue && t.DueUtc.Value > nowUtc)
                .Select(t => t.DueUtc.Value)
                .ToList();
            return future.Count == 0 ? (DateTime?)null : future.Min();
        }

        /// <inheritdoc/>
        public WorkflowMessageTriggerLookup FindMessageTriggers(string signalName, string originTenantId)
        {
            var result = new WorkflowMessageTriggerLookup();
            if (string.IsNullOrEmpty(signalName))
            {
                return result;
            }

            var matches = new List<WorkflowStartTriggerMatch>();
            var suppressed = new List<string>();

            foreach (WorkflowStartTrigger trigger in triggers.Values
                         .Where(t => t.Kind == WorkflowStartTriggerKind.Message && t.SignalName == signalName))
            {
                // Der Ursprung entscheidet. Ohne Ursprung springt nur an, was das ausdruecklich erlaubt -
                // sonst eroeffnete eine einzige namenlose Nachricht in jedem Mandanten einen Vorgang.
                // Im Ein-Mandanten-Betrieb traegt alles null und die Regel faellt von selbst zusammen.
                var relevant = ActivationsOf(trigger)
                    .Where(a => a.Enabled
                                && (a.TenantId == originTenantId
                                    || (originTenantId == null && trigger.AllowTenantlessStart)))
                    .ToList();

                if (relevant.Count == 0)
                {
                    suppressed.Add($"'{trigger.DefinitionId}' (Knoten '{trigger.NodeId}', Besitzer "
                                   + $"'{trigger.TenantId ?? "<oeffentlich>"}')");
                    continue;
                }

                matches.AddRange(relevant.Select(a => new WorkflowStartTriggerMatch
                {
                    Trigger = trigger,
                    Activation = a
                }));
            }

            result.Matches = matches;
            result.SuppressedByTenant = suppressed;
            return result;
        }

        /// <inheritdoc/>
        public IReadOnlyList<WorkflowStartTriggerMatch> ClaimDueScheduleTriggers(DateTime nowUtc, string owner,
            TimeSpan lease, int maxTriggers)
        {
            if (string.IsNullOrEmpty(owner))
            {
                throw new ArgumentNullException(nameof(owner));
            }

            // Die Ablage im Speicher kennt keine nebenlaeufigen Runner - der Anspruch waere hier ein
            // Formalismus ohne Gegenueber. Die Auswahl ist dieselbe wie in der Datenbank, damit ein Test
            // dasselbe sieht.
            if (maxTriggers <= 0)
            {
                return new List<WorkflowStartTriggerMatch>();
            }

            return activations.Values
                .Where(a => a.Enabled && a.Kind == WorkflowStartTriggerKind.Schedule
                            && a.NextDueUtc != null && a.NextDueUtc <= nowUtc)
                .OrderBy(a => a.NextDueUtc)
                .Select(a => new WorkflowStartTriggerMatch { Trigger = TriggerOf(a), Activation = a })
                // Verwaiste Aktivierungen finden keinen Ausloeser - sie feuern nie. Gemeldet wurden sie,
                // als der Ausloeser wegfiel; hier still zu ueberspringen ist deshalb in Ordnung.
                .Where(m => m.Trigger != null)
                .Take(maxTriggers)
                .ToList();
        }

        /// <inheritdoc/>
        public int? ResolveDefinitionKey(string ownerTenantId, string definitionId, int? version = null)
        {
            IEnumerable<WorkflowDefinition> candidates = definitions.Values
                .Where(d => d.Id == definitionId && d.TenantId == ownerTenantId);
            if (version.HasValue)
            {
                candidates = candidates.Where(d => d.Version == version.Value);
            }

            WorkflowDefinition found = candidates.OrderByDescending(d => d.Version).FirstOrDefault();
            return found?.Key;
        }

        /// <inheritdoc/>
        public IReadOnlyList<WorkflowStartTrigger> FindActivatableTriggers(string tenantId)
            => triggers.Values
                .Where(t => t.IsPublic && t.AllowLocalActivation)
                .ToList();

        /// <inheritdoc/>
        public IReadOnlyList<WorkflowStartTriggerActivation> GetActivations(string tenantId)
            => activations.Values.Where(a => a.TenantId == tenantId).ToList();

        /// <inheritdoc/>
        public void SaveActivation(WorkflowStartTriggerActivation activation)
        {
            if (activation == null)
            {
                throw new ArgumentNullException(nameof(activation));
            }

            WorkflowStartTriggerActivation existing = activations.Values.FirstOrDefault(
                a => a.OwnerTenantId == activation.OwnerTenantId
                     && a.DefinitionId == activation.DefinitionId && a.NodeId == activation.NodeId
                     && a.Kind == activation.Kind && a.TenantId == activation.TenantId);

            if (existing == null)
            {
                activation.ActivationKey = Interlocked.Increment(ref nextActivationKey);
                activations[activation.ActivationKey] = activation;
                return;
            }

            // Der Lauf-Zustand der bestehenden Zeile bleibt: haekelt jemand ein halbes Jahr spaeter
            // wieder an, soll der Zeitplan da weitermachen, wo er war - und nicht ein
            // "sofort"-Kennzeichen ein zweites Mal ausloesen.
            existing.Enabled = activation.Enabled;
            existing.PatternOverride = activation.PatternOverride;
            existing.VariablesJsonOverride = activation.VariablesJsonOverride;
            if (activation.NextDueUtc != null)
            {
                existing.NextDueUtc = activation.NextDueUtc;
            }

            existing.ActivatedBy = activation.ActivatedBy ?? existing.ActivatedBy;
            activation.ActivationKey = existing.ActivationKey;
        }

        /// <inheritdoc/>
        public void UpdateScheduleActivation(int activationKey, DateTime? nextDueUtc, DateTime? lastRunUtc,
            string lastInstanceId)
        {
            if (!activations.TryGetValue(activationKey, out WorkflowStartTriggerActivation trigger))
            {
                LogEnvironment.LogEvent(
                    $"UpdateScheduleActivation: activation '{activationKey}' no longer exists - it was "
                    + "probably removed in the meantime. Nothing updated.", LogSeverity.Report);
                return;
            }

            trigger.NextDueUtc = nextDueUtc;
            if (lastRunUtc != null)
            {
                trigger.LastRunUtc = lastRunUtc;
                trigger.LastInstanceId = lastInstanceId;
            }
        }

        /// <inheritdoc/>
        public DateTime? PeekNextScheduleDueUtc(DateTime nowUtc)
        {
            var future = activations.Values
                .Where(a => a.Enabled && a.Kind == WorkflowStartTriggerKind.Schedule && a.NextDueUtc != null
                            && a.NextDueUtc > nowUtc)
                .Select(a => a.NextDueUtc.Value)
                .ToList();
            return future.Count == 0 ? (DateTime?)null : future.Min();
        }

        /// <inheritdoc/>
        public bool HasRunningInstance(int definitionKey, string correlationKey)
            => correlationKey != null
               && instances.Values.Any(i => i.DefinitionKey == definitionKey
                                            && i.CorrelationKey == correlationKey
                                            && (i.Status == WorkflowStatus.Running
                                                || i.Status == WorkflowStatus.Waiting));

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindBranchesWaitingForTarget(IEnumerable<string> targets)
        {
            var targetSet = new HashSet<string>(targets ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            if (targetSet.Count == 0)
            {
                return new List<WorkflowInstance>();
            }

            // Rein am Token-Zustand orientiert (nicht am Instanz-Status): ein ziel-wartender Zweig kann neben
            // aktiven Geschwister-Zweigen bestehen, dann laeuft die Instanz noch.
            return instances.Values
                .Where(i => !i.Suspended)
                .Where(i => i.Tokens.Any(t => t.Status == TokenStatus.WaitingForTarget
                                              && t.WaitingTarget != null && targetSet.Contains(t.WaitingTarget)))
                .ToList();
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindRunnable()
        {
            // Die dringendsten zuerst - dieselbe Zusage wie beim EF-Store, damit ein Test nicht auf einer
            // Reihenfolge fusst, die es nur hier gibt.
            return instances.Values
                .Where(i => i.Status == WorkflowStatus.Running && !i.Suspended)
                .OrderBy(i => i.Priority)
                .ToList();
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindChildInstances(string parentInstanceId)
        {
            if (string.IsNullOrEmpty(parentInstanceId))
            {
                return new List<WorkflowInstance>();
            }

            return instances.Values.Where(i => i.ParentInstanceId == parentInstanceId).ToList();
        }

        /// <inheritdoc/>
        public IEnumerable<WorkflowInstance> FindFinishedChildrenWithWaitingParent()
        {
            var awaited = instances.Values
                .SelectMany(i => i.Tokens)
                .Where(t => t.Status == TokenStatus.Waiting && t.WaitingForChildInstanceId != null)
                .Select(t => t.WaitingForChildInstanceId)
                .ToHashSet(StringComparer.Ordinal);
            if (awaited.Count == 0)
            {
                return new List<WorkflowInstance>();
            }

            return instances.Values
                .Where(i => awaited.Contains(i.Id)
                            && (i.Status == WorkflowStatus.Completed || i.Status == WorkflowStatus.Faulted))
                .ToList();
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

        /// <summary>
        /// Der Ablage-Schluessel einer Definition: fachliche Id, Version <b>und Mandant</b>. Der Mandant
        /// gehoert dazu, sonst verdraengte die Definition eines Mandanten die gleichnamige eines anderen.
        /// </summary>
        private static string Key(string id, int version, string tenantId)
        {
            return $"{id}#{version}#{tenantId ?? "<public>"}";
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
