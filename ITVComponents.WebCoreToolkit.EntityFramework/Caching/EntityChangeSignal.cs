using ITVComponents.EFRepo.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Caching;
using ITVComponents.WebCoreToolkit.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Caching
{
    /// <summary>
    /// EF-backed implementation of <see cref="IEntityChangeSignal"/>. It maps the entity tables of
    /// <typeparamref name="TContext"/> to the topics configured via <see cref="EntitySignalOptions"/> (once,
    /// from the EF model) and reports/raises changes based on the singleton
    /// <see cref="IEntityWriteTracker{TContext}"/> that the EntityWriteTrackerInterceptor feeds. Registered as
    /// a singleton, so a write in one circuit/request invalidates buffered data in all others.
    /// </summary>
    /// <typeparam name="TContext">the DbContext whose writes are tracked</typeparam>
    /// <remarks>
    /// Diese Klasse ist <b>nicht</b> security-spezifisch: sie kennt nur den Schreib-Verfolger, die
    /// Topic-Einstellungen und das EF-Modell. Sie lag zunaechst im TenantSecurity-Paket, weil dort der erste
    /// Verbraucher entstand (Berechtigungs- und Navigations-Puffer) - damit war sie fuer jeden anderen
    /// Kontext (z.B. den Workflow-Kontext) nur ueber eine Security-Abhaengigkeit erreichbar. Welche
    /// Entitaeten zu welchem Thema gehoeren, bringt jeder Verbraucher selbst mit.
    /// </remarks>
    public class EntityChangeSignal<TContext> : IEntityChangeSignal<TContext> where TContext : DbContext
    {
        private readonly IEntityWriteTracker<TContext> tracker;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly EntitySignalOptions options;
        private readonly EntitySignalDebounceSettings configuredDebounce;
        private readonly Lazy<TableTopicMap> map;
        private readonly Lazy<EntitySignalDebounceSettings> debounce;

        /// <summary>Die zuletzt gelesene Fassung - sie gilt, solange keine neue da ist.</summary>
        private volatile EntitySignalDebounceSettings activeDebounce;

        /// <summary>Wann sie gelesen wurde (UTC-Ticks) - Grundlage fuer den Nachlade-Zyklus.</summary>
        private long debounceLoadedTicks;

        /// <summary>1, solange ein Nachladen laeuft: es soll immer nur EINES unterwegs sein.</summary>
        private int reloadingDebounce;

        /// <summary>
        /// Der zuletzt gelesene Text der datenbank-getriebenen Einstellung - nur fuer die Frage, ob sich
        /// etwas GEAENDERT hat. Ohne diesen Vergleich schriebe jeder Nachlade-Zyklus dieselbe Zeile ins Log.
        /// </summary>
        private string lastDebounceJson;

        /// <summary>
        /// Die Themen, deren Sammelfenster gerade laeuft. Der Eintrag IST das Fenster: wer ihn anlegen
        /// konnte, ist der Erste und startet es; alle weiteren Meldungen fallen stillschweigend hinein.
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> collecting =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityChangeSignal{TContext}"/> class.
        /// </summary>
        /// <param name="tracker">the singleton write-tracker for the context</param>
        /// <param name="scopeFactory">used to obtain the EF model once for the table-to-topic mapping</param>
        /// <param name="options">the topic-to-entity-type mapping configured for this context</param>
        /// <param name="debounceSettings">
        /// the per-topic collecting windows - process-wide, hence not bound to the context
        /// </param>
        public EntityChangeSignal(IEntityWriteTracker<TContext> tracker, IServiceScopeFactory scopeFactory,
            IOptions<EntitySignalOptions<TContext>> options,
            IOptions<EntitySignalDebounceSettings> debounceSettings = null)
        {
            this.tracker = tracker;
            this.scopeFactory = scopeFactory;
            this.options = options.Value;
            configuredDebounce = debounceSettings?.Value;
            map = new Lazy<TableTopicMap>(BuildMap, isThreadSafe: true);
            debounce = new Lazy<EntitySignalDebounceSettings>(LoadDebounceSettings, isThreadSafe: true);
            tracker.TablesWritten += OnTablesWritten;
        }

        /// <inheritdoc />
        public event Action<string> Changed;

        /// <inheritdoc />
        public DateTime GetLastChange(string topic)
        {
            if (string.IsNullOrEmpty(topic))
            {
                return DateTime.MinValue;
            }

            var result = DateTime.MinValue;
            foreach (var table in map.Value.TablesFor(topic))
            {
                var lastWrite = tracker.GetLastWrite(table);
                if (lastWrite > result)
                {
                    result = lastWrite;
                }
            }

            return result;
        }

        private void OnTablesWritten(string[] tables)
        {
            // Nothing to do when nobody is actively listening (the pull-based consumers read the already-updated
            // last-write timestamp directly and don't depend on this event).
            if (Changed == null)
            {
                return;
            }

            string[] affectedTopics;
            try
            {
                affectedTopics = tables
                    .SelectMany(t => map.Value.TopicsForTable.TryGetValue(t, out var topics)
                        ? topics
                        : Enumerable.Empty<string>())
                    .Distinct()
                    .ToArray();
            }
            catch (Exception ex)
            {
                // never break the originating SaveChanges because of a refresh-dispatch failure
                LogEnvironment.LogEvent(
                    $"EntityChangeSignal failed to map change for table '{string.Join(",", tables)}': {ex}",
                    LogSeverity.Report);
                return;
            }

            if (affectedTopics.Length == 0)
            {
                return;
            }

            foreach (var topic in affectedTopics)
            {
                Raise(topic);
            }
        }

        /// <summary>
        /// Meldet ein Thema - sofort oder gesammelt, je nach eingestelltem Fenster.
        /// </summary>
        /// <param name="topic">das betroffene Thema</param>
        /// <remarks>
        /// <para>
        /// Ohne Fenster bleibt es beim bisherigen Verhalten: sofort, aber vom schreibenden Thread weg.
        /// </para>
        /// <para>
        /// Mit Fenster gilt: die ERSTE Meldung eroeffnet es und feuert an seinem Ende einmal; alles, was
        /// dazwischen kommt, faellt hinein. Bewusst nicht "bei jeder Meldung neu starten" - unter Dauerlast
        /// (ein Runner, der ununterbrochen Instanzen vortreibt) verhungerte der Weckruf sonst genau dann,
        /// wenn es am meisten zu melden gibt. So ist das Fenster eine Obergrenze fuer die Verzoegerung.
        /// </para>
        /// </remarks>
        private void Raise(string topic)
        {
            TimeSpan window = TimeSpan.Zero;
            try
            {
                window = CurrentDebounce().WindowFor(topic);
            }
            catch (Exception ex)
            {
                // Unlesbare Einstellungen duerfen den Weckruf nicht verschlucken - dann eben ungebremst.
                LogEnvironment.LogEvent(
                    $"EntityChangeSignal could not determine the debounce window for topic '{topic}' - " +
                    $"raising immediately: {ex}", LogSeverity.Report);
            }

            if (window <= TimeSpan.Zero)
            {
                // Fire-and-forget from the writer's perspective: the dispatch is pushed off the thread that ran
                // SaveChanges, so neither a slow nor a failing subscriber (e.g. a circuit being torn down) can add
                // latency to — or abort — the originating write. The last-write timestamp was already set in
                // MarkWritten before this event, so pull-based consumers see the change synchronously regardless.
                ThreadPool.QueueUserWorkItem(_ => Dispatch(topic));
                return;
            }

            if (!collecting.TryAdd(topic, 0))
            {
                // Ein Fenster laeuft bereits - diese Meldung ist damit erledigt.
                return;
            }

            // Der Parameter braucht einen NAMEN: mit '_' waere das 'out _' unten kein Discard mehr, sondern
            // ein Verweis auf diesen Task.
            _ = Task.Delay(window).ContinueWith(elapsed =>
            {
                // Erst freigeben, dann melden: eine Aenderung, die WAEHREND des Meldens eintrifft, eroeffnet
                // damit ein neues Fenster, statt verloren zu gehen.
                collecting.TryRemove(topic, out _);
                Dispatch(topic);
            }, TaskScheduler.Default);
        }

        /// <summary>
        /// Sagt allen Abonnenten Bescheid.
        /// </summary>
        /// <remarks>
        /// Die Abonnenten-Liste wird HIER gelesen und nicht beim Eintreffen der Aenderung: zwischen beidem
        /// liegt womoeglich ein Sammelfenster, in dem einer dazugekommen oder gegangen ist.
        /// Ausserdem wird die Aufrufliste einzeln abgegangen - ein einfaches <c>Changed.Invoke</c> ist ein
        /// Multicast, der beim ersten werfenden Empfaenger abbricht und damit still alle danach uebergeht.
        /// </remarks>
        private void Dispatch(string topic)
        {
            var handler = Changed;
            if (handler == null)
            {
                return;
            }

            foreach (var subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((Action<string>)subscriber)(topic);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"EntityChangeSignal subscriber failed for topic '{topic}': {ex}",
                        LogSeverity.Report);
                }
            }
        }

        /// <summary>
        /// Die aktuell geltenden Sammelfenster - und, wenn ein Nachlade-Zyklus eingestellt ist, der Anstoss
        /// zum Auffrischen.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Nur das erste Lesen ist synchron</b>, alles Weitere laeuft nebenher: diese Methode haengt am
        /// Schreib-Pfad (der Aufrufer ist der Thread, der gerade gespeichert hat), und dort einen
        /// Datenbank-Zugriff einzubauen, waere die falsche Stelle. Bis das Nachladen fertig ist, gilt die
        /// bisherige Fassung - eine Einstellung, die eine Meldung zu frueh oder zu spaet greift, ist
        /// belanglos; ein verzoegertes SaveChanges waere es nicht.
        /// </para>
        /// <para>
        /// Der Zyklus kommt aus der HOST-Einstellung und nie aus der Datenbank (siehe
        /// <see cref="EntitySignalDebounceSettings.RefreshSeconds"/>): ein Eintrag mit 0 wuerde sich sonst
        /// selbst aussperren.
        /// </para>
        /// </remarks>
        private EntitySignalDebounceSettings CurrentDebounce()
        {
            EntitySignalDebounceSettings active = activeDebounce ?? debounce.Value;

            int refreshSeconds = configuredDebounce?.RefreshSeconds ?? 0;
            if (refreshSeconds <= 0)
            {
                return active;
            }

            long age = DateTime.UtcNow.Ticks - Volatile.Read(ref debounceLoadedTicks);
            if (age < TimeSpan.FromSeconds(refreshSeconds).Ticks)
            {
                return active;
            }

            // Nur EIN Nachladen gleichzeitig - ein Schwung Meldungen darf nicht ebenso viele Abfragen
            // ausloesen (dasselbe Anliegen, das die Sammelfenster ueberhaupt verfolgen).
            if (Interlocked.CompareExchange(ref reloadingDebounce, 1, 0) != 0)
            {
                return active;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    activeDebounce = LoadDebounceSettings();
                }
                catch (Exception ex)
                {
                    // LoadDebounceSettings faengt selbst; was hier ankommt, waere unerwartet - und wuerde
                    // sonst als unbeobachtete Ausnahme im ThreadPool verschwinden.
                    LogEnvironment.LogEvent(
                        $"EntityChangeSignal could not refresh its debounce settings: {ex}",
                        LogSeverity.Error);
                }
                finally
                {
                    // Die Zeit auch im Fehlerfall stempeln: sonst liefe der naechste Versuch sofort wieder
                    // und aus dem Zyklus wuerde eine Dauerschleife gegen eine Datenbank, die gerade nicht
                    // antwortet.
                    Volatile.Write(ref debounceLoadedTicks, DateTime.UtcNow.Ticks);
                    Volatile.Write(ref reloadingDebounce, 0);
                }
            });

            return active;
        }

        /// <summary>
        /// Liest die Sammelfenster: was der Host konfiguriert hat, und - wenn es sie gibt - die
        /// datenbank-getriebene Einstellung, die dagegen gewinnt.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Der GlobalSettings-Weg ist fuer Hosts, die ihre Kontexte ueber das Plugin-System bauen: dort gibt
        /// es keine <c>Services.Configure</c>-Gelegenheit mehr. Gelesen wird EINMAL und ueber einen eigenen
        /// Scope (der Anbieter ist scope-gebunden, dieses Signal ein Singleton) - es ist damit eine
        /// prozessweite Einstellung und ausdruecklich keine je Mandant.
        /// </para>
        /// <para>
        /// Jeder Fehler faellt auf die Code-Einstellung zurueck: eine unlesbare Stellschraube darf die
        /// Benachrichtigung nicht abschalten. Sie muss aber im Log stehen, sonst sucht man die Ursache
        /// spaeter beim Verbraucher.
        /// </para>
        /// </remarks>
        private EntitySignalDebounceSettings LoadDebounceSettings()
        {
            var configured = configuredDebounce ?? new EntitySignalDebounceSettings();
            try
            {
                using var scope = scopeFactory.CreateScope();
                if (scope.ServiceProvider.GetService(typeof(IGlobalSettingsProvider)) is not IGlobalSettingsProvider settings)
                {
                    return Adopt(configured, null);
                }

                var json = settings.GetJsonSetting(EntitySignalDebounceSettings.GlobalSettingName);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return Adopt(configured, null);
                }

                var fromDb = JsonSerializer.Deserialize<EntitySignalDebounceSettings>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (fromDb == null)
                {
                    return Adopt(configured, null);
                }

                // Der Nachlade-Zyklus bleibt beim Host: was aus der Datenbank kommt, darf nicht bestimmen,
                // ob die Datenbank noch einmal gefragt wird.
                fromDb.RefreshSeconds = configured.RefreshSeconds;
                return Adopt(fromDb, json);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"EntityChangeSignal could not read the global setting " +
                    $"'{EntitySignalDebounceSettings.GlobalSettingName}' - staying with the configured " +
                    $"settings: {ex}", LogSeverity.Warning);
                return Adopt(configured, null);
            }
        }

        /// <summary>
        /// Uebernimmt eine gelesene Fassung: stempelt die Zeit und meldet sie, wenn sie sich geaendert hat.
        /// </summary>
        /// <param name="settings">die Fassung, die ab jetzt gilt</param>
        /// <param name="json">ihr Text aus der Datenbank, oder null fuer "aus der Host-Einstellung"</param>
        /// <returns>dieselbe Fassung</returns>
        /// <remarks>
        /// Gemeldet wird die AENDERUNG, nicht der Zustand: mit einem Nachlade-Zyklus liefe sonst dieselbe
        /// Zeile im Takt durchs Log und waere in dem Moment wertlos, in dem sie gebraucht wird - naemlich
        /// wenn jemand an den Fenstern dreht und wissen will, ob es gezogen hat.
        /// </remarks>
        private EntitySignalDebounceSettings Adopt(EntitySignalDebounceSettings settings, string json)
        {
            Volatile.Write(ref debounceLoadedTicks, DateTime.UtcNow.Ticks);
            if (string.Equals(lastDebounceJson, json, StringComparison.Ordinal))
            {
                return settings;
            }

            bool first = lastDebounceJson == null && json == null;
            lastDebounceJson = json;
            if (!first)
            {
                LogEnvironment.LogEvent(
                    json == null
                        ? $"EntityChangeSignal<{typeof(TContext).Name}>: the global setting " +
                          $"'{EntitySignalDebounceSettings.GlobalSettingName}' is gone - back to the settings " +
                          "configured in the host."
                        : $"EntityChangeSignal<{typeof(TContext).Name}> took over new debounce settings from " +
                          $"the global setting '{EntitySignalDebounceSettings.GlobalSettingName}': {json}",
                    LogSeverity.Report);
            }

            return settings;
        }

        private TableTopicMap BuildMap()
        {
            var tableToTopics = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var configuredTopics = options.Topics;
                using var scope = scopeFactory.CreateScope();
                if (scope.ServiceProvider.GetService(typeof(TContext)) is DbContext ctx)
                {
                    foreach (var entityType in ctx.Model.GetEntityTypes())
                    {
                        var table = entityType.GetTableName();
                        if (string.IsNullOrEmpty(table))
                        {
                            continue;
                        }

                        foreach (var topic in configuredTopics)
                        {
                            if (topic.Value.Any(configured => Covers(configured, entityType.ClrType)))
                            {
                                if (!tableToTopics.TryGetValue(table, out var set))
                                {
                                    tableToTopics[table] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                }

                                set.Add(topic.Key);
                            }
                        }
                    }
                }
                else
                {
                    LogEnvironment.LogEvent($"EntityChangeSignal could not resolve {typeof(TContext)} to build its table-map.", LogSeverity.Report);
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"EntityChangeSignal failed to build its table-map: {ex}", LogSeverity.Error);
            }

            return new TableTopicMap(tableToTopics);
        }

        /// <summary>
        /// Determines whether a configured type covers the given entity type: equal / assignable, or — for an
        /// open generic type definition — present anywhere in the entity's base- or interface-chain.
        /// </summary>
        private static bool Covers(Type configured, Type entity)
        {
            if (configured.IsGenericTypeDefinition)
            {
                for (var t = entity; t != null && t != typeof(object); t = t.BaseType)
                {
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == configured)
                    {
                        return true;
                    }
                }

                foreach (var iface in entity.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == configured)
                    {
                        return true;
                    }
                }

                return false;
            }

            return configured.IsAssignableFrom(entity);
        }

        private sealed class TableTopicMap
        {
            private readonly Dictionary<string, string[]> topicToTables;

            public TableTopicMap(Dictionary<string, HashSet<string>> tableToTopics)
            {
                TopicsForTable = tableToTopics;
                topicToTables = tableToTopics
                    .SelectMany(kv => kv.Value.Select(topic => (topic, table: kv.Key)))
                    .GroupBy(x => x.topic, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.table).ToArray(), StringComparer.OrdinalIgnoreCase);
            }

            public Dictionary<string, HashSet<string>> TopicsForTable { get; }

            public string[] TablesFor(string topic)
                => topicToTables.TryGetValue(topic, out var tables) ? tables : Array.Empty<string>();
        }
    }
}
