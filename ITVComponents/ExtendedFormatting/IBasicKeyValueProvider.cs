using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.ExtendedFormatting
{
    /// <summary>
    /// Provides an interface that enables an object to return a value to a specific key. Objects implementing this interface can provide the indexer data to the RegexFormat.Format Method.
    /// </summary>
    public interface IBasicKeyValueProvider
    {
        /// <summary>
        /// Gets the value that is associated with the given name
        /// </summary>
        /// <param name="name">the name requested by an object</param>
        /// <returns>the value associated with the given key</returns>
        object this[string name] { get; }

        /// <summary>
        /// Gets all MemberNames that are associated with this basicKeyValue provider instance
        /// </summary>
        string[] Keys { get; }

        /// <summary>
        /// Gets a value indicating whether a specific key is present in this basicKeyValueProvider instance
        /// </summary>
        /// <param name="key">the Key for which to check</param>
        /// <returns>a value indicating whether the specified key exists in this provider</returns>
        bool ContainsKey(string key);
    }
}
