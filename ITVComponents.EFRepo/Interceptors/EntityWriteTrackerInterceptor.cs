using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Interceptors
{
    public class EntityWriteTrackerInterceptor : ISaveChangesInterceptor 
    {
        private readonly IServiceProvider services;
        private IEntityWriteTracker localTracker;
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
            if (eventData.Context != null)
            {
                var ct = eventData.Context.GetType();
                var trackerType = typeof(IEntityWriteTracker<>).MakeGenericType(ct);
                var tracker = localTracker ??
                                   (IEntityWriteTracker)services.GetService(trackerType);
                if (tracker != null)
                {
                    var l = eventData.Context.ChangeTracker.Entries().ToList();
                    var tables = l.GroupBy(n => n.Metadata.GetTableName()).Select(n => n.Key).ToArray();
                    foreach (var table in tables)
                    {
                        tracker.MarkWritten(table);
                    }
                }
                else
                {
                    LogEnvironment.LogEvent($"No Service of Type {trackerType} was found.", LogSeverity.Error);
                }
            }

            return result;
        }

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = new CancellationToken())
        {
            return ValueTask.FromResult(SavingChanges(eventData, result));
        }
    }
}
