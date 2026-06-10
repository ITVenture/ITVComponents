using ITVComponents.EFRepo.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration
{
    public class EntityWriteTracker<TContext>: IEntityWriteTracker<TContext> where TContext : DbContext
    {
        private readonly ConcurrentDictionary<string, DateTime> lastWrites = new(StringComparer.OrdinalIgnoreCase);

        public void MarkWritten(string table)
            => lastWrites[table] = DateTime.UtcNow;

        public DateTime GetLastWrite(string table)
            => lastWrites.TryGetValue(table, out var ts) ? ts : DateTime.MinValue;
    }
}
