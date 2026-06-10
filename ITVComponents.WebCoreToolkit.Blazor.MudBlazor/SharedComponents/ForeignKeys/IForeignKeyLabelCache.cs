using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.ForeignKeys;

/// <summary>
/// Scoped cache (per Blazor circuit / per request) for foreign-key labels resolved through
/// the Toolkit FK infrastructure. Lookups are O(1) after the first request that loads the FK
/// source; staleness is bounded by a TTL and short-circuited by <see cref="ITVComponents.EFRepo.Helpers.IEntityWriteTracker"/>.
/// </summary>
public interface IForeignKeyLabelCache
{
    /// <summary>
    /// Returns the label for the given key, or <c>null</c> if the key cannot be resolved.
    /// First call per (connection, table) loads the full FK list; subsequent calls hit the cache.
    /// </summary>
    Task<string?> GetLabelAsync<T>(string connection, string table, T key);
}
