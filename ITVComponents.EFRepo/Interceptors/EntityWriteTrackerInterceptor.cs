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

        // Per-async-flow tracking-suppression. A save that runs inside a SuppressTracking() scope still persists
        // normally but is NOT reported to the write-tracker, so it raises no change-signal. Used by bootstrap-style
        // writes (e.g. auto-permission-registration) whose rows no active session is waiting on: reporting them
        // would force every circuit to drop its memoized permission scope and re-resolve inline on the render
        // thread, turning a single background seed into a synchronous permission-read storm.
        private static readonly AsyncLocal<bool> suppressed = new();

        /// <summary>
        /// Suppresses write-tracking (and thereby the derived change-signals) for the writes saved within the
        /// returned scope on the current async flow. Dispose to restore the previous state. Nesting-safe.
        /// </summary>
        public static IDisposable SuppressTracking()
        {
            var previous = suppressed.Value;
            suppressed.Value = true;
            return new SuppressionScope(previous);
        }

        private sealed class SuppressionScope : IDisposable
        {
            private readonly bool previous;
            private bool disposed;

            public SuppressionScope(bool previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    disposed = true;
                    suppressed.Value = previous;
                }
            }
        }

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

            // A save that ran inside a SuppressTracking() scope is persisted but deliberately not reported, so it
            // raises no change-signal (the pending set is still cleared above to avoid leaking it).
            if (suppressed.Value)
            {
                return;
            }

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
