using System;

namespace ITVComponents.WebCoreToolkit.Caching
{
    /// <summary>
    /// Well-known topic names used by the toolkit itself. Topics are plain strings, so any consumer
    /// (also outside the toolkit) can define its own via <see cref="EntitySignalOptions"/>.
    /// </summary>
    public static class EntityChangeTopics
    {
        /// <summary>
        /// Security-relevant entities — a change here makes a user's effective permissions/features stale.
        /// </summary>
        public const string Security = "Security";

        /// <summary>
        /// Navigation-relevant entities. The toolkit also maps the security entities onto this topic,
        /// because menu visibility derives from permissions/features.
        /// </summary>
        public const string Navigation = "Navigation";
    }

    /// <summary>
    /// Host-neutral signal that tells buffered consumers (permission cache, navigation cache, custom
    /// components, …) when the data behind a given <em>topic</em> last changed, so they can re-select
    /// instead of serving stale data. Which entity types belong to which topic is configured through
    /// <see cref="EntitySignalOptions"/>. The concrete implementation is backed by the EF entity-write-tracker;
    /// when no tracker is active it reports <see cref="DateTime.MinValue"/> and never raises
    /// <see cref="Changed"/>, preserving the previous (TTL-only) behaviour.
    /// </summary>
    public interface IEntityChangeSignal
    {
        /// <summary>
        /// Gets the UTC time at which any entity belonging to the given topic was last written, or
        /// <see cref="DateTime.MinValue"/> when nothing relevant has been observed (or tracking is off).
        /// </summary>
        /// <param name="topic">the topic to query (e.g. <see cref="EntityChangeTopics.Security"/>)</param>
        /// <returns>the last-change time in UTC</returns>
        DateTime GetLastChange(string topic);

        /// <summary>
        /// Raised right after a write to an entity belonging to the reported topic. Enables active refresh
        /// (e.g. re-rendering an already displayed navigation menu) instead of refreshing only on next access.
        /// Handlers run on the writer's thread and must marshal to their own synchronization context.
        /// </summary>
        event Action<string> Changed;
    }

    /// <summary>
    /// Per-DbContext change-signal. Resolve <c>IEntityChangeSignal&lt;MyContext&gt;</c> to react to writes in a
    /// specific context; topics for that context are configured via <see cref="EntitySignalOptions{TContext}"/>.
    /// The toolkit additionally registers the non-generic <see cref="IEntityChangeSignal"/> as an alias for the
    /// security context, so context-agnostic consumers (navigation, permission scope, UI) keep working.
    /// </summary>
    /// <typeparam name="TContext">the DbContext whose writes this signal reports</typeparam>
    public interface IEntityChangeSignal<TContext> : IEntityChangeSignal
    {
    }
}
