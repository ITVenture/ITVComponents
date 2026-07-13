using System;
using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Helpers
{
    /// <summary>
    /// Culture-fallback resolution shared by help content and resources. A requested UI culture is widened
    /// most-specific-first down to the literal <see cref="Default"/> sentinel, so <c>de-CH</c> resolves to a
    /// <c>de-CH</c> row, else a <c>de</c> row, else the <c>DEFAULT</c> row.
    /// </summary>
    public static class HelpCulture
    {
        /// <summary>The literal fallback culture name stored on content/resource rows.</summary>
        public const string Default = "DEFAULT";

        /// <summary>
        /// Ordered candidate cultures for a requested UI culture, most specific first, always ending in
        /// <see cref="Default"/>. E.g. <c>"de-CH"</c> → [<c>de-CH</c>, <c>de</c>, <c>DEFAULT</c>].
        /// </summary>
        public static IReadOnlyList<string> Candidates(string? culture)
        {
            var list = new List<string>();
            var c = culture?.Trim();
            while (!string.IsNullOrWhiteSpace(c))
            {
                list.Add(c);
                var dash = c.LastIndexOf('-');
                c = dash > 0 ? c.Substring(0, dash) : null;
            }

            if (!list.Contains(Default, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(Default);
            }

            return list;
        }

        /// <summary>
        /// Picks the best matching culture from <paramref name="available"/> for the requested one (returning
        /// the value as stored, preserving its casing), or null if not even a <see cref="Default"/> row exists.
        /// </summary>
        public static string? Resolve(string? requested, IEnumerable<string> available)
        {
            var avail = available.ToList();
            foreach (var candidate in Candidates(requested))
            {
                var match = avail.FirstOrDefault(a => string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
