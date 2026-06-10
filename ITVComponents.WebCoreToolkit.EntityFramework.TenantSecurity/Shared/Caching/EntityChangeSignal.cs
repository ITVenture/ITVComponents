using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Caching
{
    /// <summary>
    /// EF-backed implementation of <see cref="IEntityChangeSignal"/>. It maps the security- and
    /// navigation-relevant entity tables of <typeparamref name="TContext"/> to their
    /// <see cref="EntityChangeScope"/> (once, from the EF model) and reports/raises changes based on the
    /// singleton <see cref="IEntityWriteTracker{TContext}"/> that the EntityWriteTrackerInterceptor feeds.
    /// Registered as a singleton, so a write in one circuit/request invalidates buffered data in all others.
    /// </summary>
    /// <typeparam name="TContext">the security DbContext whose writes are tracked</typeparam>
    public class EntityChangeSignal<TContext> : IEntityChangeSignal where TContext : DbContext
    {
        private const string BaseNs = "ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base";
        private const string ModelsNs = "ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models";

        // Base types whose tables make a user's effective permissions/features stale. Matched by
        // "namespace.name" (arity-stripped) anywhere in an entity's inheritance chain, so it works for
        // both the open-generic bases and concrete derivations across the Flat/Tree/CoreIdentity strategies.
        private static readonly HashSet<string> securityBases = new(StringComparer.Ordinal)
        {
            $"{BaseNs}.Role", $"{BaseNs}.Permission", $"{BaseNs}.UserRole", $"{BaseNs}.RolePermission",
            $"{BaseNs}.RoleRole", $"{BaseNs}.GlobalRole", $"{BaseNs}.GlobalRolePermission", $"{BaseNs}.GRoleLRole",
            $"{BaseNs}.TenantUser", $"{ModelsNs}.Tenant", $"{ModelsNs}.TenantFeatureActivation",
            "ITVComponents.WebCoreToolkit.Models.Feature"
        };

        // Navigation-only base types. Security tables additionally count towards navigation (see Classify).
        private static readonly HashSet<string> navigationBases = new(StringComparer.Ordinal)
        {
            $"{BaseNs}.NavigationMenu", $"{BaseNs}.TenantNavigationMenu"
        };

        private readonly IEntityWriteTracker<TContext> tracker;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly Lazy<TableTopicMap> map;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityChangeSignal{TContext}"/> class.
        /// </summary>
        /// <param name="tracker">the singleton write-tracker for the security context</param>
        /// <param name="scopeFactory">used to obtain the EF model once for the table-to-scope mapping</param>
        public EntityChangeSignal(IEntityWriteTracker<TContext> tracker, IServiceScopeFactory scopeFactory)
        {
            this.tracker = tracker;
            this.scopeFactory = scopeFactory;
            map = new Lazy<TableTopicMap>(BuildMap, isThreadSafe: true);
            tracker.TableWritten += OnTableWritten;
        }

        /// <inheritdoc />
        public event Action<EntityChangeScope> Changed;

        /// <inheritdoc />
        public DateTime GetLastChange(EntityChangeScope scope)
        {
            var result = DateTime.MinValue;
            foreach (var table in map.Value.TablesFor(scope))
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
                if (map.Value.Scopes.TryGetValue(table, out var scopes))
                {
                    foreach (var scope in scopes)
                    {
                        Changed?.Invoke(scope);
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
            var scopes = new Dictionary<string, HashSet<EntityChangeScope>>(StringComparer.OrdinalIgnoreCase);
            try
            {
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

                        var classified = Classify(entityType.ClrType);
                        if (classified == null)
                        {
                            continue;
                        }

                        if (!scopes.TryGetValue(table, out var set))
                        {
                            scopes[table] = set = new HashSet<EntityChangeScope>();
                        }

                        set.UnionWith(classified);
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

            return new TableTopicMap(scopes);
        }

        private static HashSet<EntityChangeScope> Classify(Type clrType)
        {
            for (var t = clrType; t != null && t != typeof(object); t = t.BaseType)
            {
                var raw = RawName(t);
                if (securityBases.Contains(raw))
                {
                    // A security change also invalidates navigation: menu visibility derives from permissions/features.
                    return new HashSet<EntityChangeScope> { EntityChangeScope.Security, EntityChangeScope.Navigation };
                }

                if (navigationBases.Contains(raw))
                {
                    return new HashSet<EntityChangeScope> { EntityChangeScope.Navigation };
                }
            }

            return null;
        }

        private static string RawName(Type t)
        {
            var name = t.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0)
            {
                name = name.Substring(0, tick);
            }

            return $"{t.Namespace}.{name}";
        }

        private sealed class TableTopicMap
        {
            private readonly string[] securityTables;
            private readonly string[] navigationTables;

            public TableTopicMap(Dictionary<string, HashSet<EntityChangeScope>> scopes)
            {
                Scopes = scopes;
                securityTables = scopes.Where(kv => kv.Value.Contains(EntityChangeScope.Security)).Select(kv => kv.Key).ToArray();
                navigationTables = scopes.Where(kv => kv.Value.Contains(EntityChangeScope.Navigation)).Select(kv => kv.Key).ToArray();
            }

            public Dictionary<string, HashSet<EntityChangeScope>> Scopes { get; }

            public string[] TablesFor(EntityChangeScope scope)
                => scope == EntityChangeScope.Security ? securityTables : navigationTables;
        }
    }
}
