using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.ForeignKeys;

internal sealed class ForeignKeyLabelCache : IForeignKeyLabelCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly Dictionary<(string Connection, string Table), Entry> entries = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly IServiceProvider services;
    private readonly IForeignKeyWriteTracker tracker;

    public ForeignKeyLabelCache(IServiceProvider services, IForeignKeyWriteTracker tracker)
    {
        this.services = services;
        this.tracker = tracker;
    }

    public async Task<string?> GetLabelAsync<T>(string connection, string table, T key)
    {
        if (key is null) return null;
        var entry = await GetOrLoadAsync<T>(connection, table).ConfigureAwait(false);
        return entry is not null && entry.Labels.TryGetValue(key, out var label) ? label : null;
    }

    private async Task<Entry?> GetOrLoadAsync<T>(string connection, string table)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var cacheKey = (connection, table);
            if (entries.TryGetValue(cacheKey, out var existing))
            {
                var lastWrite = tracker.GetLastWrite(table);
                if (existing.LoadedAtUtc >= lastWrite && DateTime.UtcNow - existing.LoadedAtUtc <= Ttl)
                {
                    return existing;
                }
            }

            var src = services.ContextForFkQuery(connection, null);
            if (src is null) return null;

            var raw = src.ReadForeignKey<T>(table);
            if (raw is null) return null;

            var dict = new Dictionary<object, string>();
            foreach (var item in raw)
            {
                if (item.Key is null) continue;
                dict[item.Key] = item.Label;
            }

            var fresh = new Entry(dict, DateTime.UtcNow);
            entries[cacheKey] = fresh;
            return fresh;
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed record Entry(Dictionary<object, string> Labels, DateTime LoadedAtUtc);
}
