using System;
using System.Collections.Concurrent;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.ForeignKeys;

internal sealed class ForeignKeyWriteTracker : IForeignKeyWriteTracker
{
    private readonly ConcurrentDictionary<string, DateTime> lastWrites = new(StringComparer.OrdinalIgnoreCase);

    public void MarkWritten(string table)
        => lastWrites[table] = DateTime.UtcNow;

    public DateTime GetLastWrite(string table)
        => lastWrites.TryGetValue(table, out var ts) ? ts : DateTime.MinValue;
}
