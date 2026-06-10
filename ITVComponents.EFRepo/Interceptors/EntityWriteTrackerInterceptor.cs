using ITVComponents.EFRepo.Helpers;
using ITVComponents.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Interceptors
{
    /// <summary>
    /// Records — per <see cref="IEntityWriteTracker"/> — which tables were written. The affected tables are
    /// collected while the entries still carry their pending states (SavingChanges) but the tracker is only
    /// marked AFTER the save actually completed (SavedChanges). Marking on save-completion is essential: the
    /// <see cref="IEntityWriteTracker.TablesWritten"/> event drives buffer invalidation, and a receiver that
    /// re-selected during SavingChanges would read the still-unpersisted (old) data.
    /// </summary>
    public class EntityWriteTrackerInterceptor : ISaveChangesInterceptor
    {
        private readonly IServiceProvider services;
        private readonly IEntityWriteTracker localTracker;

        // A single interceptor instance can be shared by concurrently-saving context instances, so the
        // collected tables are keyed by the saving context rather than held in an instance field.
        private readonly ConditionalWeakTable<DbContext, HashSet<string>> pendingTables = new();

        public EntityWriteTrackerInterceptor(IServiceProvider services)
        {
            this.services = services;
        }

        public EntityWriteTrackerInterceptor(IEntityWriteTracker tracker)
        {
            this.localTracker = tracker;
        }

        public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            CollectPending(eventData.Context);
            return result;
        }

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = new CancellationToken())
        {
            CollectPending(eventData.Context);
            return ValueTask.FromResult(result);
        }

        public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            FlushPending(eventData.Context);
            return result;
        }

        public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = new CancellationToken())
        {
            FlushPending(eventData.Context);
            return ValueTask.FromResult(result);
        }

        public void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            DiscardPending(eventData.Context);
        }

        public Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
            CancellationToken cancellationToken = new CancellationToken())
        {
            DiscardPending(eventData.Context);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Collects the tables behind the currently pending changes. Must run while the change-tracker entries
        /// still carry their Added/Modified/Deleted states (i.e. before the save accepts the changes).
        /// </summary>
        private void CollectPending(DbContext context)
        {
            if (context == null)
            {
                return;
            }

            var tables = context.ChangeTracker.Entries()
                .Select(n => n.Metadata.GetTableName())
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToArray();
            if (tables.Length == 0)
            {
                return;
            }

            var set = pendingTables.GetValue(context, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            lock (set)
            {
                foreach (var table in tables)
                {
                    set.Add(table);
                }
            }
        }

        /// <summary>
        /// Marks the collected tables as written — invoked after the save completed, so the persisted data is
        /// already visible when receivers of <see cref="IEntityWriteTracker.TablesWritten"/> re-select.
        /// </summary>
        private void FlushPending(DbContext context)
        {
            if (context == null || !pendingTables.TryGetValue(context, out var set))
            {
                return;
            }

            pendingTables.Remove(context);

            var tracker = localTracker ?? ResolveTracker(context);
            if (tracker == null)
            {
                LogEnvironment.LogEvent($"No EntityWriteTracker service was found for {context.GetType()}.",
                    LogSeverity.Error);
                return;
            }

            string[] tables;
            lock (set)
            {
                tables = set.ToArray();
            }

            if (tables.Length != 0)
            {
                tracker.MarkWritten(tables);
            }
        }

        private void DiscardPending(DbContext context)
        {
            if (context != null)
            {
                pendingTables.Remove(context);
            }
        }

        private IEntityWriteTracker ResolveTracker(DbContext context)
        {
            var trackerType = typeof(IEntityWriteTracker<>).MakeGenericType(context.GetType());
            return (IEntityWriteTracker)services?.GetService(trackerType);
        }
    }
}
