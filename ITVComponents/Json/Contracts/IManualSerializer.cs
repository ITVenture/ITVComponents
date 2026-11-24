using System;
using System.Collections.Generic;

namespace ITVComponents.Json.Contracts
{
    /// <summary>
    /// A type that writes and reads its state itself, as a list of named entries. Supported by
    /// <see cref="SerializationTypingMode.EnvelopePolymorphism"/> through a bridge: the entries are written as an object
    /// keyed by their names, every value an open place that carries its own type.
    /// </summary>
    /// <remarks>
    /// Superseded: a plain class with <c>object</c> (or base-typed) members keeps every type through
    /// <see cref="SerializationTypingMode.EnvelopePolymorphism"/> on its own, without the state held in <see cref="Data"/>
    /// during serialization (which is not safe when one instance is serialized by two threads at once).
    /// </remarks>
    [Obsolete("Use a plain class with object (or base-typed) members and SerializationTypingMode.EnvelopePolymorphism - the envelope keeps the runtime types on every level.")]
    public interface IManualSerializer
    {
        /// <summary>The entries; filled by <see cref="GetObjectData"/> for writing and before <see cref="ApplyObjectData"/> for reading.</summary>
        public IList<ManualSerializationData> Data { get; set; }

        /// <summary>Adds the state of the object to <see cref="Data"/>.</summary>
        void GetObjectData();

        /// <summary>Restores the state of the object from <see cref="Data"/>.</summary>
        void ApplyObjectData();
    }
}
