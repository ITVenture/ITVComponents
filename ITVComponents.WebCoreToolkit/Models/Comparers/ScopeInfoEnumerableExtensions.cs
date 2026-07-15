using System;
using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.WebCoreToolkit.Models.Comparers
{
    public static class ScopeInfoEnumerableExtensions
    {
        /// <summary>
        /// Collapses eligible scopes to one entry per <see cref="ScopeInfo.ScopeName"/> (case-insensitive).
        /// When the same scope is reachable in multiple ways, the entry with the higher-weighted
        /// <see cref="ScopeAccessMode"/> is kept (Direct outranks Inherited). This replaces a plain
        /// <c>Distinct(new ScopeInfoComparer())</c>, which would keep an arbitrary (first-seen) representative.
        /// </summary>
        /// <param name="source">the eligible scopes to de-duplicate</param>
        /// <returns>one <see cref="ScopeInfo"/> per scope-name, preferring Direct access</returns>
        public static IEnumerable<ScopeInfo> DistinctPreferDirectAccess(this IEnumerable<ScopeInfo> source)
        {
            return source
                .GroupBy(n => n.ScopeName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(n => n.AccessMode).First());
        }
    }
}
