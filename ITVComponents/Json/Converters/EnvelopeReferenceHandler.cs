using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace ITVComponents.Json.Converters
{
    /// <summary>
    /// Reference preservation for <see cref="SerializationTypingMode.EnvelopePolymorphism"/>: every
    /// <see cref="JsonSerializer"/> call made inside the envelope converter gets the resolver of the running top-level
    /// call, so <c>$id</c>/<c>$ref</c> work across envelope boundaries (cycles included).
    /// </summary>
    /// <remarks>
    /// System.Text.Json creates a resolver per <see cref="JsonSerializer"/> call, and the built-in preserve handler cannot
    /// hand out its resolver. The strategy therefore opens a scope around each top-level call (<see cref="ScopeFor"/>),
    /// and <see cref="CreateResolver"/> returns the resolver of that scope. Without a scope it throws: a fresh resolver per
    /// nested call would silently write copies instead of references.
    /// </remarks>
    internal sealed class EnvelopeReferenceHandler : ReferenceHandler
    {
        public static readonly EnvelopeReferenceHandler Instance = new EnvelopeReferenceHandler();

        private static readonly AsyncLocal<ReferenceResolver> current = new AsyncLocal<ReferenceResolver>();

        private EnvelopeReferenceHandler()
        {
        }

        /// <summary>
        /// Opens a reference scope for one top-level call if the options use this handler, otherwise returns null.
        /// Scopes nest: an independent document serialized from within a converter gets its own resolver.
        /// </summary>
        public static IDisposable ScopeFor(JsonSerializerOptions options)
        {
            if (options?.ReferenceHandler is not EnvelopeReferenceHandler)
            {
                return null;
            }

            var previous = current.Value;
            current.Value = new ScopedReferenceResolver();
            return new Restore(previous);
        }

        /// <summary>The resolver of the running call if the options preserve references, otherwise null.</summary>
        public static ReferenceResolver ResolverFor(JsonSerializerOptions options)
        {
            return options.ReferenceHandler is EnvelopeReferenceHandler ? Instance.CreateResolver() : null;
        }

        public override ReferenceResolver CreateResolver()
        {
            return current.Value ?? throw new InvalidOperationException(
                "Reference preservation with SerializationTypingMode.EnvelopePolymorphism needs a serialization scope. " +
                "The serializer options were used outside of JsonHelper - use JsonHelper.ToJson/FromJsonString instead.");
        }

        private sealed class Restore : IDisposable
        {
            private readonly ReferenceResolver previous;
            private bool done;

            public Restore(ReferenceResolver previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (!done)
                {
                    current.Value = previous;
                    done = true;
                }
            }
        }

        /// <summary>Resolver for one document: ids on write by reference identity, objects by id on read.</summary>
        private sealed class ScopedReferenceResolver : ReferenceResolver
        {
            private readonly Dictionary<string, object> byId = new Dictionary<string, object>(StringComparer.Ordinal);

            private readonly Dictionary<object, string> byObject = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);

            private int next;

            public override void AddReference(string referenceId, object value)
            {
                if (!byId.TryAdd(referenceId, value))
                {
                    throw new JsonException($"The reference id '{referenceId}' occurs more than once in the document.");
                }
            }

            public override string GetReference(object value, out bool alreadyExists)
            {
                if (byObject.TryGetValue(value, out string id))
                {
                    alreadyExists = true;
                    return id;
                }

                alreadyExists = false;
                id = (++next).ToString(System.Globalization.CultureInfo.InvariantCulture);
                byObject.Add(value, id);
                return id;
            }

            public override object ResolveReference(string referenceId)
            {
                return byId.TryGetValue(referenceId, out object value)
                    ? value
                    : throw new JsonException($"The reference '{referenceId}' points to no object written before it.");
            }
        }
    }
}
