using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Caching
{
    /// <summary>
    /// EF-backed implementation of <see cref="IEntityChangeSignal"/>. It maps the entity tables of
    /// <typeparamref name="TContext"/> to the topics configured via <see cref="EntitySignalOptions"/> (once,
    /// from the EF model) and reports/raises changes based on the singleton
    /// <see cref="IEntityWriteTracker{TContext}"/> that the EntityWriteTrackerInterceptor feeds. Registered as
    /// a singleton, so a write in one circuit/request invalidates buffered data in all others.
    /// </summary>
    /// <typeparam name="TContext">the DbContext whose writes are tracked</typeparam>
    public class EntityChangeSignal<TContext> : IEntityChangeSignal<TContext> where TContext : DbContext
    {
        private readonly IEntityWriteTracker<TContext> tracker;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly EntitySignalOptions options;
        private readonly Lazy<TableTopicMap> map;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityChangeSignal{TContext}"/> class.
        /// </summary>
        /// <param name="tracker">the singleton write-tracker for the context</param>
        /// <param name="scopeFactory">used to obtain the EF model once for the table-to-topic mapping</param>
        /// <param name="options">the topic-to-entity-type mapping configured for this context</param>
        public EntityChangeSignal(IEntityWriteTracker<TContext> tracker, IServiceScopeFactory scopeFactory,
            IOptions<EntitySignalOptions<TContext>> options)
        {
            this.tracker = tracker;
            this.scopeFactory = scopeFactory;
            this.options = options.Value;
            map = new Lazy<TableTopicMap>(BuildMap, isThreadSafe: true);
            tracker.TableWritten += OnTableWritten;
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

        private void OnTableWritten(string table)
        {
            try
            {
                if (map.Value.TopicsForTable.TryGetValue(table, out var topics))
                {
                    foreach (var topic in topics)
                    {
                        Changed?.Invoke(topic);
                    }
                }
            }
            catch (Exception ex)
            {
                // never break the originating SaveChanges because of a refresh-dispatch failure
                LogEnvironment.LogEvent($"EntityChangeSignal failed to dispatch change for table '{table}': {ex}", LogSeverity.Report);
            }
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
