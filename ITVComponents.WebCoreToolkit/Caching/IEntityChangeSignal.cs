using System;

namespace ITVComponents.WebCoreToolkit.Caching
{
    /// <summary>
    /// Logical group of entities whose change must invalidate buffered data.
    /// </summary>
    public enum EntityChangeScope
    {
        /// <summary>
        /// Security-relevant entities (permissions, role-permissions, roles, role-inheritance, user-roles,
        /// global-roles/-permissions, tenant-users, tenants, feature-activations). A change here makes a
        /// user's effective permissions/features stale.
        /// </summary>
        Security,

        /// <summary>
        /// Navigation-relevant entities (navigation-menu + tenant-navigation) PLUS everything in
        /// <see cref="Security"/>, because menu visibility is derived from permissions and features.
        /// </summary>
        Navigation
    }

    /// <summary>
    /// Host-neutral signal that tells buffered consumers (permission cache, navigation cache, …) when the
    /// data behind a given <see cref="EntityChangeScope"/> last changed, so they can re-select instead of
    /// serving stale data. The concrete implementation is backed by the EF entity-write-tracker; when no
    /// tracker is active it reports <see cref="DateTime.MinValue"/> and never raises <see cref="Changed"/>,
    /// preserving the previous (TTL-only) behaviour.
    /// </summary>
    public interface IEntityChangeSignal
    {
        /// <summary>
        /// Gets the UTC time at which any entity belonging to the given scope was last written, or
        /// <see cref="DateTime.MinValue"/> when nothing relevant has been observed (or tracking is off).
        /// </summary>
        /// <param name="scope">the scope to query</param>
        /// <returns>the last-change time in UTC</returns>
        DateTime GetLastChange(EntityChangeScope scope);

        /// <summary>
        /// Raised right after a write to an entity belonging to the reported scope. Enables active refresh
        /// (e.g. re-rendering an already displayed navigation menu) instead of refreshing only on next access.
        /// Handlers run on the writer's thread and must marshal to their own synchronization context.
        /// </summary>
        event Action<EntityChangeScope> Changed;
    }
}
