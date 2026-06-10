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

        public event Action<string[]> TablesWritten;

        public void MarkWritten(params string[] tables)
        {
            var nw = DateTime.UtcNow;
            foreach (var table in tables)
            {
                lastWrites[table] = nw;
            }

            TablesWritten?.Invoke(tables);
        }

        public DateTime GetLastWrite(string table)
            => lastWrites.TryGetValue(table, out var ts) ? ts : DateTime.MinValue;
    }
}
