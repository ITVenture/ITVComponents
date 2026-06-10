using System;
using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.WebCoreToolkit.Caching
{
    /// <summary>
    /// Configures which entity types belong to which <see cref="IEntityChangeSignal"/> topic. Register
    /// additively from anywhere via <c>services.Configure&lt;EntitySignalOptions&gt;(o =&gt; o.Add("MyTopic",
    /// typeof(T1), typeof(T2)))</c>; a consumer can thus subscribe its own topic to the entities whose change
    /// must trigger a refresh — also from outside the toolkit.
    /// </summary>
    /// <remarks>
    /// Matching against the EF model is by assignability: a configured type covers an entity when the entity
    /// equals it, derives from / implements it, or — for an open generic type definition (e.g.
    /// <c>typeof(Role&lt;&gt;)</c>) — has it anywhere in its base-/interface-chain. So both base types and
    /// concrete entity types can be registered.
    /// </remarks>
    public class EntitySignalOptions
    {
        private readonly Dictionary<string, HashSet<Type>> topics = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Adds the given entity types to a topic (creating it if necessary). Repeated calls for the same
        /// topic accumulate. Null entries are ignored.
        /// </summary>
        /// <param name="topic">the topic name</param>
        /// <param name="entityTypes">the entity types (concrete, base, interface, or open generic definition)</param>
        /// <returns>this instance for chaining</returns>
        public EntitySignalOptions Add(string topic, params Type[] entityTypes)
        {
            if (string.IsNullOrEmpty(topic) || entityTypes == null)
            {
                return this;
            }

            if (!topics.TryGetValue(topic, out var set))
            {
                topics[topic] = set = new HashSet<Type>();
            }

            foreach (var type in entityTypes)
            {
                if (type != null)
                {
                    set.Add(type);
                }
            }

            return this;
        }

        /// <summary>
        /// Gets a snapshot of the configured topic-to-types mapping.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyCollection<Type>> Topics
            => topics.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyCollection<Type>)kv.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }
}
