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
            // Der Besitzer VOR dem Speichern. Er entscheidet, welche Ausloeser-Zeilen in den Neuaufbau
            // gehoeren - und zwar nur bei einer BESTEHENDEN Zeile: bei einer neuen ist "null" nicht
            // "war oeffentlich", sondern "gab es nicht". Der Unterschied ist wesentlich, sonst raeumte
            // das Anlegen einer mandanteneigenen Definition die Ausloeser der oeffentlichen gleichen
            // Namens ab.
            bool tenantChanged = false;
            string previousTenantId = null;
            if (row == null)
            {
                row = new WorkflowDefinitionRow { Id = definition.Id, Version = definition.Version };
                ctx.WorkflowDefinitions.Add(row);
            }
            else
            {
                previousTenantId = row.TenantId;
                tenantChanged = previousTenantId != definition.TenantId;
                if (row.Version != definition.Version)
                {
                    // Erlaubt (eine falsch gesetzte Versionsnummer soll korrigierbar sein), aber nie
                    // still: dieselbe Zeile traegt danach eine andere Version, die vorige gibt es nicht
                    // mehr. Wer eine NEUE Version wollte, muss den Schluessel loslassen - kommt er hier
                    // mit, ist das der Weg, auf dem eine Fassung unbemerkt verschwindet.
                    LogEnvironment.LogEvent(
                        $"Definition '{definition.Id}': die bestehende Zeile (Schluessel {row.DefinitionKey}) "
                        + $"wechselt von Version {row.Version} auf {definition.Version}. Es entsteht KEINE "
                        + "zweite Fassung - war eine neue Version gemeint, muss die technische Kennung 0 sein.",
                        LogSeverity.Warning);
                }

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

            SyncTriggers(ctx, definition, tenantChanged, previousTenantId);
        }

        /// <summary>
        /// Baut die Ausloeser dieser Definition neu auf - und zwar aus ihrer HOECHSTEN Version.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Nur die hoechste Version loest aus. Sonst horchte jede jemals gespeicherte Fassung weiter mit,
        /// und eine eingehende Nachricht startete so viele Instanzen, wie es Versionen gibt.
        /// </para>
        /// <para>
        /// Der <b>Stand</b> eines unveraendert gebliebenen Zeitplans wird uebernommen (naechste
        /// Faelligkeit, letzter Lauf). Ohne das finge jedes Speichern der Definition - auch eine Aenderung
        /// an ganz anderer Stelle - den Zeitplan von vorn an, und ein Muster mit "sofort"-Kennzeichen
        /// liefe bei jedem Speichern erneut los. Als "unveraendert" gilt derselbe Knoten mit demselben
        /// Muster; wer das Muster aendert, meint einen anderen Plan und bekommt einen frischen Anlauf.
        /// </para>
        /// <para>
        /// <paramref name="tenantChanged"/> deckt den einen Fall ab, in dem eine Definition den Besitzer
        /// wechselt: die Sichtbarkeit wird umgestellt (mandanteneigen &lt;-&gt; oeffentlich). Dann gehoeren
        /// auch die Zeilen des VORIGEN Besitzers in den Neuaufbau - siehe unten.
        /// </para>
        /// </remarks>
        /// <param name="ctx">der Kontext</param>
        /// <param name="definition">die eben gespeicherte Definition</param>
        /// <param name="tenantChanged">ob die Definition gerade den Besitzer gewechselt hat</param>
        /// <param name="previousTenantId">
        /// der vorige Besitzer; nur aussagekraeftig, wenn <paramref name="tenantChanged"/> gilt
        /// </param>
        private static void SyncTriggers(WorkflowContext ctx, WorkflowDefinition definition,
            bool tenantChanged, string previousTenantId)
        {
            // Ohne Query-Filter und ausdruecklich auf den Mandanten der Definition - wie beim Schreiben
            // der Definition selbst: der gerade aktive Kontext darf nicht entscheiden, welche Zeilen
            // aufgeraeumt werden.
            // Max ueber ein NULLABLE int: das ist die uebersetzbare Form von "hoechste Version, und wenn
            // es keine gibt, meine eigene". DefaultIfEmpty(wert) laesst sich nicht nach SQL uebersetzen -
            // und weil dieser Weg in JEDEM Speichern einer Definition steckt, faellt so etwas nicht an
            // einer Stelle auf, sondern ueberall zugleich.
            int? highest = ctx.WorkflowDefinitions.IgnoreQueryFilters()
                .Where(d => d.Id == definition.Id && d.TenantId == definition.TenantId)
                .Max(d => (int?)d.Version);
            int highestVersion = highest ?? definition.Version;

            WorkflowDefinition newest = definition;
            if (highestVersion != definition.Version)
            {
                WorkflowDefinitionRow newestRow = ctx.WorkflowDefinitions.IgnoreQueryFilters()
                    .FirstOrDefault(d => d.Id == definition.Id && d.TenantId == definition.TenantId
                                         && d.Version == highestVersion);
                newest = Materialize(newestRow);
                if (newest == null)
                {
                    LogEnvironment.LogEvent(
                        $"Die Ausloeser der Definition '{definition.Id}' konnten nicht aufgebaut werden: "
                        + $"Version {highestVersion} ist nicht lesbar. Die Ausloeser bleiben, wie sie sind.",
                        LogSeverity.Error);
                    return;
                }
            }

            // Welche Ausloeser-Zeilen in den Neuaufbau gehoeren. Drei Bedingungen, weil eine nicht reicht:
            //
            // 1. die des jetzigen Besitzers - der Normalfall;
            // 2. beim Wechsel der Sichtbarkeit auch die des VORIGEN. Ohne sie faenden sie sich nie
            //    wieder in dieser Auswahl und wuerden damit nie mehr aktualisiert oder abgeraeumt: der
            //    Zeitplan des alten Besitzers liefe fuer immer auf dem Muster von heute weiter, und
            //    daneben entstuende eine zweite Zeile, ueber die alles ein zweites Mal feuert;
            // 3. alles, was ueber den DefinitionKey auf DIESE Definition zeigt. Das ist der Fang fuer
            //    Altbestand: eine Definition, die vor dieser Korrektur gewechselt hat, hat ihre alte
            //    Zeile liegen lassen, und welcher Mandant das einmal war, steht nirgends mehr. Ueber den
            //    Schluessel ist sie trotzdem eindeutig zuzuordnen - jede Zeile mit abweichendem Besitzer
            //    ist per Definition eine Leiche, denn der Neuaufbau setzt ihn immer mit.
            //
            // Bewusst als getrennte Abfragen statt als eine mit ODER ueber gefangene Bedingungen: so
            // haengt nichts davon ab, wie der Provider ein "konstantes" bool im Ausdrucksbaum uebersetzt.
            // Zusammengefuehrt wird ueber den Schluessel - die Bedingungen ueberschneiden sich, und
            // dieselbe Zeile zweimal in der Liste hiesse, sie zweimal zu bearbeiten.
            var byKey = new Dictionary<int, WorkflowStartTriggerRow>();
            void Collect(IEnumerable<WorkflowStartTriggerRow> rows)
            {
                foreach (WorkflowStartTriggerRow row in rows)
                {
                    byKey[row.TriggerKey] = row;
                }
            }

            Collect(ctx.WorkflowStartTriggers
                .Where(t => t.TenantId == definition.TenantId && t.DefinitionId == definition.Id));
            if (tenantChanged)
            {
                Collect(ctx.WorkflowStartTriggers
                    .Where(t => t.TenantId == previousTenantId && t.DefinitionId == definition.Id));
            }

            if (newest.Key != 0)
            {
                int newestKey = newest.Key;
                Collect(ctx.WorkflowStartTriggers.Where(t => t.DefinitionKey == newestKey));
            }

            List<WorkflowStartTriggerRow> existing = byKey.Values.ToList();

            // Die Aktivierungen dieser Definition - ueber die fachliche Identitaet, nicht ueber
            // TriggerKey: der ist gleich ein anderer.
            List<WorkflowStartTriggerActivationRow> activations = ctx.WorkflowStartTriggerActivations
                .Where(a => a.OwnerTenantId == definition.TenantId && a.DefinitionId == definition.Id)
                .ToList();

            if (tenantChanged)
            {
                activations.AddRange(RehomeActivations(ctx, definition, previousTenantId, activations));
            }

            var keptKeys = new HashSet<int>();
            var now = DateTime.UtcNow;

            foreach (WorkflowStartTrigger fresh in WorkflowStartTriggerFactory.FromDefinition(newest))
            {
                // Wiedererkannt ueber Knoten und Art - NICHT mehr zusaetzlich ueber das Muster. Das
                // Muster gehoert jetzt in den Vergleich, der den Lauf-Zustand zuruecksetzt, und nicht in
                // die Identitaet: sonst waere ein umgeschriebener Zeitplan ein anderer AUSLOESER, und
                // saemtliche Uebernahmen der Mandanten haetten ihn verloren.
                WorkflowStartTriggerRow target = existing.FirstOrDefault(
                    t => t.NodeId == fresh.NodeId && t.Kind == (int)fresh.Kind
                         && !keptKeys.Contains(t.TriggerKey));
                string previousPattern = target?.Pattern;
                if (target == null)
                {
                    target = new WorkflowStartTriggerRow();
                    ctx.WorkflowStartTriggers.Add(target);
                }

                target.DefinitionKey = fresh.DefinitionKey;
                target.DefinitionId = fresh.DefinitionId;
                target.DefinitionVersion = fresh.DefinitionVersion;
                target.TenantId = fresh.TenantId;
                target.IsPublic = fresh.IsPublic;
                target.NodeId = fresh.NodeId;
                target.Kind = (int)fresh.Kind;
                target.RequiredFeature = fresh.RequiredFeature;
                target.RequiredPermission = fresh.RequiredPermission;
                target.AllowLocalActivation = fresh.AllowLocalActivation;
                target.SignalName = fresh.SignalName;
                target.Mode = (int)fresh.Mode;
                target.AdoptCorrelationKey = fresh.AdoptCorrelationKey;
                target.AllowTenantlessStart = fresh.AllowTenantlessStart;
                target.Pattern = fresh.Pattern;
                target.VariablesJson = fresh.VariablesJson;
                target.SkipWhilePreviousRuns = fresh.SkipWhilePreviousRuns;
                target.AllowReschedule = fresh.AllowReschedule;
                target.AllowOwnVariables = fresh.AllowOwnVariables;
                if (target.TriggerKey != 0)
                {
                    keptKeys.Add(target.TriggerKey);
                }

                SyncActivations(ctx, fresh, activations, previousPattern, now);
            }

            foreach (WorkflowStartTriggerRow stale in existing.Where(t => !keptKeys.Contains(t.TriggerKey)))
            {
                WarnAboutOrphans(stale, activations);
                ctx.WorkflowStartTriggers.Remove(stale);
            }

            ctx.SaveChanges();
        }

        /// <summary>
        /// Zieht die Uebernahmen mit, wenn eine Definition den Besitzer wechselt - mandanteneigen wird
        /// oeffentlich oder umgekehrt.
        /// </summary>
        /// <param name="ctx">der Kontext</param>
        /// <param name="definition">die Definition in ihrem NEUEN Zustand</param>
        /// <param name="previousTenantId">der vorige Besitzer</param>
        /// <param name="existingAtNewOwner">
        /// die Uebernahmen, die beim neuen Besitzer schon stehen - gegen sie wird auf Dubletten geprueft
        /// </param>
        /// <returns>die umgehaengten Zeilen (die entfernten sind nicht dabei)</returns>
        /// <remarks>
        /// <para>
        /// Wird die Definition <b>oeffentlich</b>, bleiben alle Uebernahmen gueltig: wer sie bisher fuhr,
        /// faehrt sie weiter - sie haengt ab jetzt nur an der oeffentlichen Fassung. Ohne das Umhaengen
        /// findet sie weder der Aufgriff noch die Uebersicht wieder (beide suchen ueber
        /// <c>OwnerTenantId</c>), und der Mandant bekaeme unter "Zentrale Workflows" einen Prozess zum
        /// Anhaken angeboten, den er in Wahrheit schon faehrt. Hakt er an, laeuft er doppelt.
        /// </para>
        /// <para>
        /// Wird sie <b>mandanteneigen</b>, gilt das nur noch fuer den neuen Besitzer. Die Uebernahmen der
        /// anderen zeigen auf einen Prozess, den sie ab jetzt nicht mehr sehen duerfen - sie werden
        /// entfernt, und das wird gemeldet: ein Zeitplan, der ab jetzt schweigt, darf das nicht still tun.
        /// </para>
        /// </remarks>
        private static List<WorkflowStartTriggerActivationRow> RehomeActivations(WorkflowContext ctx,
            WorkflowDefinition definition, string previousTenantId,
            List<WorkflowStartTriggerActivationRow> existingAtNewOwner)
        {
            List<WorkflowStartTriggerActivationRow> previous = ctx.WorkflowStartTriggerActivations
                .Where(a => a.OwnerTenantId == previousTenantId && a.DefinitionId == definition.Id)
                .ToList();
            var kept = new List<WorkflowStartTriggerActivationRow>();
            foreach (WorkflowStartTriggerActivationRow activation in previous)
            {
                // Gross-/Kleinschreibung bewusst egal: hier steht Loeschen gegen Behalten, und ein
                // Mandantenname, der sich nur in der Schreibweise unterscheidet, ist derselbe Mandant.
                // Ein Fehlurteil kostete hier eine Uebernahme - unwiederbringlich.
                bool belongsToNewOwner = definition.IsPublic
                                         || string.Equals(activation.TenantId, definition.TenantId,
                                             StringComparison.OrdinalIgnoreCase);
                if (!belongsToNewOwner)
                {
                    LogEnvironment.LogEvent(
                        $"Die Uebernahme von '{definition.Id}' (Knoten '{activation.NodeId}') durch den "
                        + $"Mandanten '{activation.TenantId ?? "-"}' wurde entfernt: die Definition gehoert "
                        + $"jetzt dem Mandanten '{definition.TenantId}' und steht ihm nicht mehr offen.",
                        LogSeverity.Warning);
                    ctx.WorkflowStartTriggerActivations.Remove(activation);
                    continue;
                }

                // Steht beim neuen Besitzer schon eine Uebernahme fuer denselben Knoten und denselben
                // fahrenden Mandanten, kann die alte nicht umgehaengt werden - die fachliche Identitaet
                // ist eindeutig. Das passiert nur bei Altbestand aus der Zeit, als der Wechsel die alten
                // Zeilen liegen liess; gemeldet wird es trotzdem, sonst verschwaende hier still ein
                // Lauf-Zustand.
                bool duplicate = existingAtNewOwner.Any(
                    a => a.NodeId == activation.NodeId && a.Kind == activation.Kind
                         && string.Equals(a.TenantId, activation.TenantId,
                             StringComparison.OrdinalIgnoreCase));
                if (duplicate)
                {
                    LogEnvironment.LogEvent(
                        $"Die Uebernahme von '{definition.Id}' (Knoten '{activation.NodeId}') durch den "
                        + $"Mandanten '{activation.TenantId ?? "-"}' konnte nicht mitgezogen werden: beim "
                        + "neuen Besitzer steht dafuer bereits eine Zeile. Die aeltere wurde entfernt, es "
                        + "gilt die bestehende.", LogSeverity.Warning);
                    ctx.WorkflowStartTriggerActivations.Remove(activation);
                    continue;
                }

                activation.OwnerTenantId = definition.TenantId;
                kept.Add(activation);
            }

            return kept;
        }

        /// <summary>
        /// Zieht die Aktivierungen eines eben aufgebauten Ausloesers nach: legt fuer eine
        /// mandanteneigene Definition die eine Aktivierung an, und setzt den Lauf-Zustand zurueck, wenn
        /// sich das <b>zentrale Muster</b> geaendert hat.
        /// </summary>
        /// <remarks>
        /// Der Reset ist die Fortschreibung einer bewussten Entscheidung von frueher: ein umgeschriebener
        /// Zeitplan ist ein ANDERER Plan, und sein erster Lauf gehoert ihm - sonst greift ein
        /// "sofort"-Kennzeichen nie, weil "schon mal gelaufen" aus der Zeit davor stammt. Betroffen sind
        /// nur die Aktivierungen, die auch wirklich auf dem zentralen Muster laufen; wer ein eigenes
        /// setzen darf und gesetzt hat, bleibt unberuehrt.
        /// </remarks>
        private static void SyncActivations(WorkflowContext ctx, WorkflowStartTrigger fresh,
            List<WorkflowStartTriggerActivationRow> activations, string previousPattern, DateTime nowUtc)
        {
            List<WorkflowStartTriggerActivationRow> mine = activations
                .Where(a => a.NodeId == fresh.NodeId && a.Kind == (int)fresh.Kind)
                .ToList();

            // Ueber IsPublic und nicht ueber "TenantId ist null": im Ein-Mandanten-Betrieb traegt alles
            // null, und dort MUSS die Aktivierung entstehen - sonst laeuft danach kein Zeitplan mehr.
            if (!fresh.IsPublic && mine.All(a => a.TenantId != fresh.TenantId))
            {
                var own = new WorkflowStartTriggerActivationRow
                {
                    OwnerTenantId = fresh.TenantId,
                    DefinitionId = fresh.DefinitionId,
                    NodeId = fresh.NodeId,
                    Kind = (int)fresh.Kind,
                    TenantId = fresh.TenantId,
                    Enabled = true,
                    NextDueUtc = fresh.Kind != WorkflowStartTriggerKind.Schedule
                        ? null
                        : WorkflowStartTriggerFactory.FirstDueUtc(fresh.Pattern, nowUtc,
                            $"'{fresh.DefinitionId}', Knoten '{fresh.NodeId}'"),
                    ActivatedBy = "(automatisch)",
                    ActivatedUtc = nowUtc
                };
                ctx.WorkflowStartTriggerActivations.Add(own);
                activations.Add(own);
                return;
            }

            if (fresh.Kind != WorkflowStartTriggerKind.Schedule || previousPattern == fresh.Pattern)
            {
                return;
            }

            foreach (WorkflowStartTriggerActivationRow activation in mine)
            {
                bool followsOwnPattern = fresh.AllowReschedule
                                         && !string.IsNullOrWhiteSpace(activation.PatternOverride);
                if (followsOwnPattern)
                {
                    continue;
                }

                activation.LastRunUtc = null;
                activation.LastInstanceId = null;
                activation.NextDueUtc = WorkflowStartTriggerFactory.FirstDueUtc(fresh.Pattern, nowUtc,
                    $"'{fresh.DefinitionId}', Knoten '{fresh.NodeId}', Mandant "
                    + $"'{activation.TenantId ?? "-"}'");
            }
        }

        /// <summary>
        /// Sagt, welche Uebernahmen durch das Wegfallen eines Ausloesers ins Leere laufen. Die Zeilen
        /// bleiben - die Zustimmung soll erhalten sein, falls der Knoten zurueckkommt -, aber ein
        /// Zeitplan, der ab jetzt schweigt, darf das nicht unbemerkt tun.
        /// </summary>
        private static void WarnAboutOrphans(WorkflowStartTriggerRow stale,
            List<WorkflowStartTriggerActivationRow> activations)
        {
            var affected = activations
                .Where(a => a.NodeId == stale.NodeId && a.Kind == stale.Kind && a.Enabled)
                .Select(a => a.TenantId ?? "-")
                .ToList();
            if (affected.Count == 0)
            {
                return;
            }

            LogEnvironment.LogEvent(
                $"Der Ausloeser '{stale.DefinitionId}' (Knoten '{stale.NodeId}', Art {stale.Kind}) ist mit "
                + $"dem Speichern der Definition weggefallen. {affected.Count} aktive Uebernahme(n) laufen "
                + $"ab jetzt ins Leere: {string.Join(", ", affected)}.", LogSeverity.Warning);
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
            row.Suspended = instance.Suspended;
            row.SuspendedReason = instance.SuspendedReason;
            row.Priority = instance.Priority;
            row.CorrelationKey = instance.CorrelationKey;
            row.FaultMessage = instance.FaultMessage;
            row.FaultCode = instance.FaultCode;
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
                    // Der Ursprung der Nachricht - im Regelfall der Mandant der sendenden Instanz. Er
                    // muss die Vormerkung ueberleben: der Nachhol-Lauf kann in einem anderen Prozess
                    // stattfinden, und dort waere der Absender nicht mehr zu ermitteln.
                    TenantId = message.OriginTenantId ?? row.TenantId,
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

            // Ohne Query-Filter, und hier haengt mehr daran als Sichtbarkeit: was diese Abfrage nicht
            // findet, legt die Schleife darunter als NEUE Zeile an - und laeuft beim Speichern in eine
            // Schluesselverletzung. Die Mandanten-Grenze ist an dieser Stelle laengst gezogen (die
            // Instanz kam durch ihren eigenen Filter); alles, was an ihr haengt, gehoert dazu.
            //
            // Das ist zugleich die Stelle, die den denormalisierten Tenant nachzieht (weiter unten
            // tr.TenantId = row.TenantId) - eine Zeile mit noch leerem Tenant muss dafuer sichtbar sein.
            List<TokenRow> existing = ctx.Tokens.IgnoreQueryFilters()
                .Where(t => t.InstanceId == instance.Id).ToList();
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

            // Die Instanz kam durch ihren Filter - ab hier ist der Mandant entschieden, und die Instanz
            // muss VOLLSTAENDIG geladen werden. Ein Token, das ein zweiter Filter unterschlaegt, ergibt
            // eine Instanz, die anders weiterlaeuft als sie steht.
            List<TokenRow> tokens = ctx.Tokens.IgnoreQueryFilters()
                .Where(t => t.InstanceId == instanceId).ToList();
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
            int faulted = (int)WorkflowStatus.Faulted;
            // Angehaltene Instanzen bleiben aussen vor - ihre Timer werden zwar faellig, aber niemand
            // soll sie deswegen vorantreiben. Beim Fortsetzen sind sie ueberfaellig und kommen dran.
            // Gefaultete ebenso: dort bleiben die Tokens fuer den Wiederaufsatz stehen, und den loest
            // ausschliesslich ein ausdruecklicher Retry aus (WorkflowEngine.MayResumeOnEvent). Die
            // Bedingung steht hier, damit der Poll sie nicht bei JEDEM Takt aufgreift und abweist.
            List<string> ids = ctx.Tokens
                .Where(t => t.Status == waiting && t.DueUtc != null && t.DueUtc <= nowUtc
                            && !ctx.WorkflowInstances.Any(i => i.Id == t.InstanceId
                                                               && (i.Suspended || i.Status == faulted)))
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
            int faulted = (int)WorkflowStatus.Faulted;
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
                // Der Join auf die Instanz stand hier ohnehin (fuer die Dringlichkeit) - die Bedingungen
                // "angehalten" und "gefaultet" kosten daher nichts extra. Beide heissen hier dasselbe:
                // faellig ja, aufgreifen nein (siehe FindDueTimers). Ohne sie verbraeuchte eine
                // gefaultete Instanz bei jedem Poll einen Platz von maxInstances.
                .Join(ctx.WorkflowInstances.Where(r => !r.Suspended && r.Status != faulted),
                    x => x.InstanceId, r => r.Id,
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
            //    Ohne Query-Filter: der Anspruch ist Sache des Runners und nicht eines Mandanten. Die
            //    Auswahl darueber begrenzt ohnehin schon, WAS gestempelt wird - der Filter koennte hier
            //    nur noch einen Teil der ausgewaehlten Zeilen stillschweigend auslassen.
            _ = ctx.Tokens.IgnoreQueryFilters()
                .Where(t => candidates.Contains(t.InstanceId)
                            && t.Status == waiting && t.DueUtc != null && t.DueUtc <= nowUtc
                            && (t.TimerLeaseUntilUtc == null || t.TimerLeaseUntilUtc <= nowUtc))
                .ExecuteUpdate(s => s
                    .SetProperty(t => t.TimerLeaseOwner, claim)
                    .SetProperty(t => t.TimerLeaseUntilUtc, until));

            // 3. Was DIESER Aufruf bekommen hat - erkennbar an der Aufruf-Guid. Nur dessen Instanzen
            //    werden geladen; das ist die eigentliche Ersparnis gegenueber FindDueTimers.
            //    Ebenfalls filterfrei: es soll genau das zurueckkommen, was Schritt 2 gestempelt hat.
            //    Die Mandanten-Grenze zieht danach LoadInstances.
            List<string> ids = ctx.Tokens.IgnoreQueryFilters()
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
            //
            // Ohne Query-Filter: die Antwort steuert, wann der Runner das naechste Mal aufwacht - eine
            // mandantenweise Antwort liesse ihn an den Terminen aller anderen vorbeischlafen. Anders als
            // die uebrigen Suchlaeufe geht diese Zahl NICHT durch LoadInstances, wo die Mandanten-Grenze
            // sonst gezogen wird.
            return ctx.Tokens.IgnoreQueryFilters()
                .Where(t => t.Status == waiting && t.DueUtc != null && t.DueUtc > nowUtc)
                .Min(t => t.DueUtc);
        }

        /// <inheritdoc/>
        public WorkflowMessageTriggerLookup FindMessageTriggers(string signalName, string originTenantId)
        {
            var result = new WorkflowMessageTriggerLookup();
            if (string.IsNullOrEmpty(signalName))
            {
                return result;
            }

            using WorkflowContext ctx = contextFactory();
            int message = (int)WorkflowStartTriggerKind.Message;
            // Ohne Query-Filter: eine Nachricht kommt von aussen. WELCHE Mandanten sie anlaufen laesst,
            // entscheidet der Ursprung - nicht der gerade aktive Kontext.
            List<WorkflowStartTriggerRow> rows = ctx.WorkflowStartTriggers.AsNoTracking().IgnoreQueryFilters()
                .Where(t => t.Kind == message && t.SignalName == signalName)
                .ToList();
            if (rows.Count == 0)
            {
                return result;
            }

            var definitionIds = rows.Select(t => t.DefinitionId).Distinct().ToList();
            List<WorkflowStartTriggerActivationRow> activations = ctx.WorkflowStartTriggerActivations
                .AsNoTracking().IgnoreQueryFilters()
                .Where(a => a.Enabled && a.Kind == message && definitionIds.Contains(a.DefinitionId))
                .ToList();

            var matches = new List<WorkflowStartTriggerMatch>();
            var suppressed = new List<string>();
            foreach (WorkflowStartTriggerRow row in rows)
            {
                WorkflowStartTrigger trigger = ToTrigger(row);

                // Die Regel in einer Zeile: der Ursprung entscheidet; ohne Ursprung springt nur an, was
                // es ausdruecklich erlaubt. Im Ein-Mandanten-Betrieb traegt alles null, dort faellt
                // beides zusammen und die Regel wirkt nicht.
                List<WorkflowStartTriggerActivationRow> relevant = activations
                    .Where(a => a.OwnerTenantId == row.TenantId && a.DefinitionId == row.DefinitionId
                                && a.NodeId == row.NodeId
                                && (a.TenantId == originTenantId
                                    || (originTenantId == null && row.AllowTenantlessStart)))
                    .ToList();

                if (relevant.Count == 0)
                {
                    suppressed.Add($"'{row.DefinitionId}' (Knoten '{row.NodeId}', Besitzer "
                                   + $"'{row.TenantId ?? "<oeffentlich>"}')");
                    continue;
                }

                matches.AddRange(relevant.Select(a => new WorkflowStartTriggerMatch
                {
                    Trigger = trigger,
                    Activation = ToActivation(a)
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

            if (maxTriggers <= 0)
            {
                return new List<WorkflowStartTriggerMatch>();
            }

            using WorkflowContext ctx = contextFactory();
            int schedule = (int)WorkflowStartTriggerKind.Schedule;
            DateTime until = nowUtc.Add(lease);
            string claim = owner + "#" + Guid.NewGuid().ToString("N");

            // Dasselbe zweistufige Verfahren wie beim Timer-Anspruch: auswaehlen, stempeln, das
            // Gestempelte zurueckholen. Die Bedingung steht im Update ein zweites Mal - genau darin liegt
            // der Ausschluss gegen einen zweiten Runner, der zwischen Auswahl und Update dazwischenfunkt.
            // Hier ist er nicht nur eine Optimierung: es gibt noch keine Instanz, deren Version die
            // doppelte Anlage verhindern koennte.
            //
            // Gestempelt wird die AKTIVIERUNG: fahren drei Mandanten denselben zentralen Zeitplan, sind
            // das drei unabhaengige Laeufe, die einander nicht ausbremsen duerfen.
            List<int> candidates = ctx.WorkflowStartTriggerActivations.IgnoreQueryFilters()
                .Where(a => a.Enabled && a.Kind == schedule && a.NextDueUtc != null && a.NextDueUtc <= nowUtc
                            && (a.LeaseUntilUtc == null || a.LeaseUntilUtc <= nowUtc))
                .OrderBy(a => a.NextDueUtc)
                .Take(maxTriggers)
                .Select(a => a.ActivationKey)
                .ToList();
            if (candidates.Count == 0)
            {
                return new List<WorkflowStartTriggerMatch>();
            }

            _ = ctx.WorkflowStartTriggerActivations.IgnoreQueryFilters()
                .Where(a => candidates.Contains(a.ActivationKey)
                            && a.Enabled && a.Kind == schedule && a.NextDueUtc != null && a.NextDueUtc <= nowUtc
                            && (a.LeaseUntilUtc == null || a.LeaseUntilUtc <= nowUtc))
                .ExecuteUpdate(s => s
                    .SetProperty(a => a.LeaseOwner, claim)
                    .SetProperty(a => a.LeaseUntilUtc, until));

            List<WorkflowStartTriggerActivationRow> claimed = ctx.WorkflowStartTriggerActivations
                .AsNoTracking().IgnoreQueryFilters()
                .Where(a => a.LeaseOwner == claim)
                .ToList();
            if (claimed.Count == 0)
            {
                return new List<WorkflowStartTriggerMatch>();
            }

            var definitionIds = claimed.Select(a => a.DefinitionId).Distinct().ToList();
            List<WorkflowStartTriggerRow> triggerRows = ctx.WorkflowStartTriggers.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(t => t.Kind == schedule && definitionIds.Contains(t.DefinitionId))
                .ToList();

            var result = new List<WorkflowStartTriggerMatch>();
            foreach (WorkflowStartTriggerActivationRow activation in claimed)
            {
                WorkflowStartTriggerRow trigger = triggerRows.FirstOrDefault(
                    t => t.TenantId == activation.OwnerTenantId && t.DefinitionId == activation.DefinitionId
                         && t.NodeId == activation.NodeId);
                if (trigger == null)
                {
                    // Verwaist: der Knoten wurde umbenannt oder entfernt. Gemeldet wurde das beim
                    // Speichern; hier den Anspruch aufloesen und die Faelligkeit abraeumen, sonst laeuft
                    // die Zeile bei jedem Poll erneut auf.
                    ReleaseOrphanedActivation(activation.ActivationKey);
                    continue;
                }

                result.Add(new WorkflowStartTriggerMatch
                {
                    Trigger = ToTrigger(trigger),
                    Activation = ToActivation(activation)
                });
            }

            return result;
        }

        /// <summary>
        /// Nimmt einer verwaisten Aktivierung Anspruch und Faelligkeit - sonst waere sie bei jedem Poll
        /// wieder faellig und liefe in eine Dauerschleife. Die Zeile selbst bleibt: die Zustimmung soll
        /// erhalten sein, falls der Knoten zurueckkommt.
        /// </summary>
        private void ReleaseOrphanedActivation(int activationKey)
        {
            using WorkflowContext ctx = contextFactory();
            _ = ctx.WorkflowStartTriggerActivations.IgnoreQueryFilters()
                .Where(a => a.ActivationKey == activationKey)
                .ExecuteUpdate(s => s
                    .SetProperty(a => a.LeaseOwner, (string)null)
                    .SetProperty(a => a.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(a => a.NextDueUtc, (DateTime?)null));
        }

        /// <inheritdoc/>
        public void UpdateScheduleActivation(int activationKey, DateTime? nextDueUtc, DateTime? lastRunUtc,
            string lastInstanceId)
        {
            using WorkflowContext ctx = contextFactory();
            WorkflowStartTriggerActivationRow row = ctx.WorkflowStartTriggerActivations.IgnoreQueryFilters()
                .FirstOrDefault(a => a.ActivationKey == activationKey);
            if (row == null)
            {
                // Kein Fehler: waehrend der Lauf lief, kann die Uebernahme zurueckgenommen worden sein.
                // Stillschweigen darf es trotzdem nicht - sonst sucht man spaeter, warum ein Zeitplan
                // seinen Stand nicht fortgeschrieben hat.
                LogEnvironment.LogEvent(
                    $"UpdateScheduleActivation: Aktivierung '{activationKey}' existiert nicht mehr - "
                    + "vermutlich wurde sie zwischenzeitlich entfernt. Nichts fortgeschrieben.",
                    LogSeverity.Report);
                return;
            }

            row.NextDueUtc = nextDueUtc;
            if (lastRunUtc != null)
            {
                row.LastRunUtc = lastRunUtc;
                row.LastInstanceId = lastInstanceId;
            }

            // Den Anspruch freigeben: der Lauf ist vorbei. Ohne das bliebe die Aktivierung bis zum Ablauf
            // der Frist gesperrt - bei einem Minutentakt waere das jeder zweite Termin.
            row.LeaseOwner = null;
            row.LeaseUntilUtc = null;
            ctx.SaveChanges();
        }

        /// <inheritdoc/>
        public int? ResolveDefinitionKey(string ownerTenantId, string definitionId, int? version = null)
        {
            if (string.IsNullOrEmpty(definitionId))
            {
                return null;
            }

            using WorkflowContext ctx = contextFactory();
            // Ohne Query-Filter und AUSDRUECKLICH auf den genannten Besitzer: hier soll gerade NICHT die
            // "eigene schlaegt oeffentliche"-Regel greifen. Wer diesen Weg nimmt, weiss, wessen
            // Definition er meint.
            IQueryable<WorkflowDefinitionRow> q = ctx.WorkflowDefinitions.AsNoTracking().IgnoreQueryFilters()
                .Where(d => d.Id == definitionId && d.TenantId == ownerTenantId);
            if (version.HasValue)
            {
                q = q.Where(d => d.Version == version.Value);
            }

            return q.OrderByDescending(d => d.Version)
                .Select(d => (int?)d.DefinitionKey)
                .FirstOrDefault();
        }

        /// <inheritdoc/>
        public IReadOnlyList<WorkflowStartTrigger> FindActivatableTriggers(string tenantId)
        {
            using WorkflowContext ctx = contextFactory();
            // IsPublic und nicht "TenantId ist null": im Ein-Mandanten-Betrieb traegt alles null, und
            // dort gibt es nichts zu uebernehmen.
            return ctx.WorkflowStartTriggers.AsNoTracking().IgnoreQueryFilters()
                .Where(t => t.IsPublic && t.AllowLocalActivation)
                .ToList()
                .Select(ToTrigger)
                .ToList();
        }

        /// <inheritdoc/>
        public IReadOnlyList<WorkflowStartTriggerActivation> GetActivations(string tenantId)
        {
            using WorkflowContext ctx = contextFactory();
            return ctx.WorkflowStartTriggerActivations.AsNoTracking().IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId)
                .ToList()
                .Select(ToActivation)
                .ToList();
        }

        /// <inheritdoc/>
        public void SaveActivation(WorkflowStartTriggerActivation activation)
        {
            if (activation == null)
            {
                throw new ArgumentNullException(nameof(activation));
            }

            using WorkflowContext ctx = contextFactory();
            int kind = (int)activation.Kind;
            WorkflowStartTriggerActivationRow row = ctx.WorkflowStartTriggerActivations.IgnoreQueryFilters()
                .FirstOrDefault(a => a.OwnerTenantId == activation.OwnerTenantId
                                     && a.DefinitionId == activation.DefinitionId
                                     && a.NodeId == activation.NodeId && a.Kind == kind
                                     && a.TenantId == activation.TenantId);
            if (row == null)
            {
                row = new WorkflowStartTriggerActivationRow
                {
                    OwnerTenantId = activation.OwnerTenantId,
                    DefinitionId = activation.DefinitionId,
                    NodeId = activation.NodeId,
                    Kind = kind,
                    TenantId = activation.TenantId,
                    ActivatedUtc = activation.ActivatedUtc == default
                        ? DateTime.UtcNow
                        : activation.ActivatedUtc
                };
                ctx.WorkflowStartTriggerActivations.Add(row);
                row.NextDueUtc = activation.NextDueUtc;
            }
            else if (activation.NextDueUtc != null)
            {
                // Der Lauf-Zustand einer BESTEHENDEN Zeile bleibt sonst unangetastet - genau deshalb
                // loescht Abhaken nicht: wer ein halbes Jahr spaeter wieder anhaekelt, soll da
                // weitermachen, wo er war, statt ein "sofort"-Kennzeichen ein zweites Mal auszuloesen.
                row.NextDueUtc = activation.NextDueUtc;
            }

            row.Enabled = activation.Enabled;
            row.PatternOverride = activation.PatternOverride;
            row.VariablesJsonOverride = activation.VariablesJsonOverride;
            row.ActivatedBy = activation.ActivatedBy ?? row.ActivatedBy;
            ctx.SaveChanges();
            activation.ActivationKey = row.ActivationKey;
        }

        /// <inheritdoc/>
        public DateTime? PeekNextScheduleDueUtc(DateTime nowUtc)
        {
            using WorkflowContext ctx = contextFactory();
            int schedule = (int)WorkflowStartTriggerKind.Schedule;
            return ctx.WorkflowStartTriggerActivations.IgnoreQueryFilters()
                .Where(a => a.Enabled && a.Kind == schedule && a.NextDueUtc != null && a.NextDueUtc > nowUtc)
                .Min(a => a.NextDueUtc);
        }

        /// <inheritdoc/>
        public bool HasRunningInstance(int definitionKey, string correlationKey)
        {
            if (correlationKey == null)
            {
                return false;
            }

            using WorkflowContext ctx = contextFactory();
            int running = (int)WorkflowStatus.Running;
            int waiting = (int)WorkflowStatus.Waiting;
            return ctx.WorkflowInstances.AsNoTracking().IgnoreQueryFilters()
                .Any(i => i.DefinitionKey == definitionKey && i.CorrelationKey == correlationKey
                          && (i.Status == running || i.Status == waiting));
        }

        /// <summary>Uebersetzt eine Ausloeser-Zeile in das store-neutrale Modell.</summary>
        private static WorkflowStartTrigger ToTrigger(WorkflowStartTriggerRow row)
            => new WorkflowStartTrigger
            {
                TriggerKey = row.TriggerKey,
                DefinitionKey = row.DefinitionKey,
                DefinitionId = row.DefinitionId,
                DefinitionVersion = row.DefinitionVersion,
                TenantId = row.TenantId,
                IsPublic = row.IsPublic,
                NodeId = row.NodeId,
                Kind = (WorkflowStartTriggerKind)row.Kind,
                RequiredFeature = row.RequiredFeature,
                RequiredPermission = row.RequiredPermission,
                AllowLocalActivation = row.AllowLocalActivation,
                SignalName = row.SignalName,
                Mode = (MessageStartMode)row.Mode,
                AdoptCorrelationKey = row.AdoptCorrelationKey,
                AllowTenantlessStart = row.AllowTenantlessStart,
                Pattern = row.Pattern,
                VariablesJson = row.VariablesJson,
                SkipWhilePreviousRuns = row.SkipWhilePreviousRuns,
                AllowReschedule = row.AllowReschedule,
                AllowOwnVariables = row.AllowOwnVariables
            };

        /// <summary>Uebersetzt eine Aktivierungs-Zeile in das store-neutrale Modell.</summary>
        private static WorkflowStartTriggerActivation ToActivation(WorkflowStartTriggerActivationRow row)
            => new WorkflowStartTriggerActivation
            {
                ActivationKey = row.ActivationKey,
                OwnerTenantId = row.OwnerTenantId,
                DefinitionId = row.DefinitionId,
                NodeId = row.NodeId,
                Kind = (WorkflowStartTriggerKind)row.Kind,
                TenantId = row.TenantId,
                Enabled = row.Enabled,
                PatternOverride = row.PatternOverride,
                VariablesJsonOverride = row.VariablesJsonOverride,
                NextDueUtc = AsUtc(row.NextDueUtc),
                LastRunUtc = AsUtc(row.LastRunUtc),
                LastInstanceId = row.LastInstanceId,
                ClaimedBy = row.LeaseOwner,
                ClaimedUntil = AsUtc(row.LeaseUntilUtc),
                ActivatedBy = row.ActivatedBy,
                ActivatedUtc = AsUtc(row.ActivatedUtc) ?? row.ActivatedUtc
            };

        /// <summary>
        /// Kennzeichnet einen aus der Datenbank gelesenen Zeitpunkt ausdruecklich als UTC.
        /// </summary>
        /// <remarks>
        /// Die Datenbank speichert Zeitpunkte ohne Zeitzone (<c>datetime2</c>), und EF gibt sie mit
        /// <see cref="DateTimeKind.Unspecified"/> zurueck. Fuer den Vergleich ist das egal - nicht aber
        /// fuer den naechsten, der auf einem solchen Wert <c>ToUniversalTime()</c> aufruft: der liest ihn
        /// dann als ORTSZEIT und verschiebt ihn um die Zonendifferenz. Ein Feld, dessen Name auf Utc
        /// endet, soll auch einen Wert liefern, der das von sich sagt.
        /// </remarks>
        private static DateTime? AsUtc(DateTime? value)
            => value == null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

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
            int faulted = (int)WorkflowStatus.Faulted;
            // Angehalten und gefaultet heissen auch hier "vorantreiben nein" (siehe FindDueTimers) - und
            // auch dieser Suchlauf wird gepollt.
            List<string> ids = ctx.Tokens
                .Where(t => t.Status == waitingForTarget
                            && t.WaitingTarget != null && targetList.Contains(t.WaitingTarget)
                            && !ctx.WorkflowInstances.Any(i => i.Id == t.InstanceId
                                                               && (i.Suspended || i.Status == faulted)))
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
                .Where(r => r.Status == running && !r.Suspended)
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
            OriginTenantId = row.TenantId,
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
            // Ohne Query-Filter, und das ist hier keine Formsache: der Anspruch gehoert einem RUNNER,
            // nicht einem Mandanten. Ein halb aufgeraeumter Runner ist schlechter als ein gar nicht
            // aufgeraeumter - die uebrigen Ansprueche blieben bis zum Ablauf der Frist liegen, und
            // niemand bekaeme davon etwas zu sehen.
            ctx.Tokens.IgnoreQueryFilters()
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
            // HIER wird die Mandanten-Grenze gezogen - fuer alle Suchlaeufe, die vorher nur Kandidaten-Ids
            // gesammelt haben. Die Token-Abfrage danach laeuft filterfrei: zu einer sichtbaren Instanz
            // gehoeren ALLE ihre Tokens, sonst laeuft sie unvollstaendig weiter.
            List<WorkflowInstanceRow> rows = ctx.WorkflowInstances.Where(r => ids.Contains(r.Id)).ToList();
            List<string> foundIds = rows.Select(r => r.Id).ToList();
            Dictionary<string, List<TokenRow>> tokensByInstance = ctx.Tokens.IgnoreQueryFilters()
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
                Suspended = row.Suspended,
                SuspendedReason = row.SuspendedReason,
                Priority = row.Priority,
                CorrelationKey = row.CorrelationKey,
                FaultMessage = row.FaultMessage,
                FaultCode = row.FaultCode,
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
