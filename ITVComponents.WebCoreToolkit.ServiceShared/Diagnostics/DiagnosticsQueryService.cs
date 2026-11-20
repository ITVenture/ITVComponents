using System;
using System.Collections;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;

namespace ITVComponents.WebCoreToolkit.ServiceShared.Diagnostics
{
    /// <summary>
    /// Default <see cref="IDiagnosticsQueryService"/> implementation. Stateless: every dependency is taken from the
    /// passed-in <see cref="IDiagnosticsQueryContext"/>, so the same instance is safe to share as a singleton.
    /// </summary>
    public class DiagnosticsQueryService : IDiagnosticsQueryService
    {
        /// <inheritdoc/>
        public IEnumerable Execute(string queryName, string area, IDiagnosticsQueryContext context, IDictionary<string, string> arguments)
        {
            var dataSource = context.Services.ContextForDiagnosticsQuery(queryName, area, out var query);
            if (dataSource == null)
            {
                return null;
            }

            IEnumerable result;
            try
            {
                result = dataSource.RunDiagnosticsQuery(query, context.User, context.Services, arguments);
            }
            catch
            {
                // Nothing was handed out yet, so nobody else would ever release the source.
                dataSource.Dispose();
                throw;
            }

            return TakeOwnership(dataSource, result);
        }

        /// <summary>
        /// Indicates whether a query result is a single value rather than a sequence of rows. Such a result is
        /// <see cref="IEnumerable"/> only by accident of its type: a dictionary would fall apart into its
        /// key/value-pairs, a string into its characters.
        /// </summary>
        /// <param name="result">the result of a Diagnostics-Query</param>
        /// <returns>true for a dictionary or a string, false for everything else</returns>
        public static bool IsSingleValue(object result)
        {
            return result is IDictionary || result is string;
        }

        /// <summary>
        /// Hands the result of a query out and takes care that its data-source is released exactly once.
        /// </summary>
        /// <param name="owner">the data-source (and the scope it may own) the result was read from</param>
        /// <param name="result">the raw result of the query</param>
        /// <returns>the result in the form the callers receive it</returns>
        /// <remarks>
        /// <para>
        /// A sequence of rows is streamed and the source is released at the end of the enumeration, so a lazily
        /// evaluated query still has its context while it is read.
        /// </para>
        /// <para>
        /// A single value (see <see cref="IsSingleValue"/>) is returned as it is and the source is released at once.
        /// Running it through the iterator lost its shape: a dictionary came out as <c>[{Key,Value}]</c> instead of an
        /// object. A dictionary is copied into a <see cref="Dictionary{TKey,TValue}"/> with string keys, so it
        /// serializes as an object whatever its key type. The copy is shallow: a value that still reads lazily from
        /// the context (an <c>IQueryable</c>, an unmaterialized LINQ expression) is invalid after the release. A
        /// query that returns a dictionary must materialize its values.
        /// </para>
        /// </remarks>
        public static IEnumerable TakeOwnership(IDisposable owner, IEnumerable result)
        {
            if (!IsSingleValue(result))
            {
                return EnumerateAndDispose(owner, result);
            }

            try
            {
                if (result is IDictionary dict)
                {
                    var copy = new Dictionary<string, object>(dict.Count);
                    foreach (DictionaryEntry entry in dict)
                    {
                        copy[entry.Key?.ToString() ?? string.Empty] = entry.Value;
                    }

                    return copy;
                }

                return result;
            }
            finally
            {
                owner.Dispose();
            }
        }

        private static IEnumerable EnumerateAndDispose(IDisposable owner, IEnumerable source)
        {
            try
            {
                foreach (var item in source)
                {
                    yield return item;
                }
            }
            finally
            {
                owner.Dispose();
            }
        }
    }
}
