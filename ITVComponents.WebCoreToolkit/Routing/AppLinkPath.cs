using System;
using ITVComponents.WebCoreToolkit.Globalization;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;

namespace ITVComponents.WebCoreToolkit.Routing
{
    /// <summary>
    /// The shape rules both <see cref="IAppLink"/> implementations share, in one place so the host-specific
    /// halves can not drift apart on what a "module url" looks like.
    /// </summary>
    public static class AppLinkPath
    {
        /// <summary>
        /// Brings a stored or observed path into the canonical module form: exactly one leading slash, no
        /// trailing one, and empty when there is nothing. <c>"/"</c> - the application root - stays
        /// <c>"/"</c>, because that is a page and not "no url".
        /// </summary>
        /// <param name="url">the url to normalize, in any of the spellings that occur in the wild</param>
        /// <returns>the normalized module url</returns>
        public static string Normalize(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            var trimmed = url.Trim().TrimEnd('/');
            if (trimmed.Length == 0)
            {
                return "/";
            }

            return trimmed[0] == '/' ? trimmed : "/" + trimmed;
        }

        /// <summary>
        /// Removes every prefix a request path can carry in front of the module part - culture, shared asset,
        /// tenant, in that order, which is the order they appear in the URL. The tenant is only removed when
        /// it is actually the leading segment; on a host that keeps it in a cookie there is nothing to strip.
        /// </summary>
        /// <param name="path">an absolute path, with or without the prefixes</param>
        /// <param name="tenant">the current tenant, or null when the scope is not explicit</param>
        /// <returns>the normalized module url</returns>
        public static string StripPrefixes(string path, string tenant)
        {
            var rest = Normalize(path);
            if (rest.Length == 0)
            {
                return string.Empty;
            }

            rest = Normalize(CulturePath.Strip(rest));

            var assetSegment = SharedAssetPath.FirstSegment(rest);
            if (SharedAssetPath.IsAssetSegment(assetSegment))
            {
                rest = Normalize(rest.Substring(assetSegment.Length + 1));
            }

            if (!string.IsNullOrEmpty(tenant)
                && rest.StartsWith("/" + tenant, StringComparison.OrdinalIgnoreCase)
                && (rest.Length == tenant.Length + 1 || rest[tenant.Length + 1] == '/'))
            {
                rest = Normalize(rest.Substring(tenant.Length + 1));
            }

            return rest.Length == 0 ? "/" : rest;
        }
    }
}
