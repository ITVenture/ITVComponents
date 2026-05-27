using System;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.ForeignKeys;

/// <summary>
/// Singleton service that records the last write-time per foreign-key source table.
/// <see cref="IForeignKeyLabelCache"/> consults it on every lookup to invalidate caches
/// whose load-time predates the tracked write — that is how a Scoped (per-circuit) cache
/// can react to writes that happened in a different circuit without an in-memory bus.
/// </summary>
public interface IForeignKeyWriteTracker
{
    /// <summary>
    /// Record that the given FK source table was just written to. Called by handlers
    /// after Create/Update/Delete on entities that other views resolve as FK labels.
    /// </summary>
    void MarkWritten(string table);

    /// <summary>
    /// Returns the most recent write-time for the table, or <see cref="DateTime.MinValue"/>
    /// if it was never marked. Cache entries loaded before this timestamp are considered stale.
    /// </summary>
    DateTime GetLastWrite(string table);
}
