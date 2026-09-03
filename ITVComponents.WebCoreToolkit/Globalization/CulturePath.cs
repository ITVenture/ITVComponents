using System;
using System.Text.RegularExpressions;

namespace ITVComponents.WebCoreToolkit.Globalization
{
    /// <summary>
    /// The single place that knows how a culture appears in a URL. The middleware that strips the prefix,
    /// the base-href of a Blazor host and everything that canonicalizes a request path use these methods,
    /// so stripping and link-building can not drift apart - the same arrangement <c>SharedAssetPath</c>
    /// has for the asset segment.
    /// <para>
    /// Form: <c>/c/{culture}/~{asset}/{tenant}/rest</c> - two segments, and they lead the URL in front of
    /// everything else. The order follows what depends on what: the language is a property of the READER
    /// and is decided before we know who they are and where they are going; asset and tenant answer "what
    /// may you see" and "where are you". Whoever sends a link can therefore prepend a language to any URL
    /// without knowing anything about the rest of it.
    /// </para>
    /// <para>
    /// A word instead of a marker character (<c>~</c> for the asset, for instance) is deliberate: <c>@</c>
    /// carries the userinfo connotation in URLs and is Razor's escape character on top of that, and every
    /// other free punctuation character is awkward in either a shell or a Razor file. The price is that the
    /// segment name has to be excluded as a tenant name - which is why the culture prefix is only recognized
    /// when the SECOND segment has the shape of a culture (see <see cref="LooksLikeCulture"/>). A tenant
    /// called "c" keeps working; only a tenant called "c" that also has a top-level page named like a
    /// culture ("/c/de") would collide.
    /// </para>
    /// </summary>
    public static class CulturePath
    {
        /// <summary>
        /// The default name of the leading segment that introduces the culture.
        /// </summary>
        public const string DefaultSegmentName = "c";

        /// <summary>
        /// The shape a culture segment has to have to be recognized: a two- or three-letter language,
        /// optionally followed by script/region/variant subtags. Deliberately structural rather than a
        /// lookup against the configured cultures, because the same decision has to be reproducible where
        /// no configuration is at hand - in a Blazor circuit, for instance, which sees nothing but its base
        /// URI. Whether the culture is actually SUPPORTED is a separate question and is answered once, by
        /// the middleware.
        /// </summary>
        private static readonly Regex CultureShape = new(
            @"^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static string segmentName = DefaultSegmentName;

        /// <summary>
        /// Gets or sets the name of the leading segment. Process-wide, because the URL shape is a property
        /// of the deployment and not of a request - and because the places that have to recognize it (a
        /// static path canonicalization, a Blazor circuit without an <c>HttpContext</c>) have no options
        /// object within reach. <c>UseCulturePath()</c> sets it from
        /// <c>CulturePathOptions.SegmentName</c> at startup; setting it later has no legitimate use.
        /// </summary>
        public static string SegmentName
        {
            get => segmentName;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("The culture segment name must not be empty.", nameof(value));
                }

                if (value.IndexOf('/') >= 0)
                {
                    throw new ArgumentException("The culture segment name is a single path segment and must not contain a slash.", nameof(value));
                }

                segmentName = value;
            }
        }

        /// <summary>
        /// Indicates whether the given segment has the shape of a culture name.
        /// </summary>
        /// <param name="segment">a single path segment, without slashes</param>
        /// <returns>true when the segment could be a culture</returns>
        public static bool LooksLikeCulture(string segment)
            => !string.IsNullOrEmpty(segment) && CultureShape.IsMatch(segment);

        /// <summary>
        /// Builds the prefix (with a leading slash, without a trailing one) for the given culture, or an
        /// empty string when there is none.
        /// </summary>
        /// <param name="culture">the culture name, e.g. <c>de-CH</c>, or null</param>
        /// <returns>the prefix, e.g. <c>/c/de-CH</c></returns>
        public static string BuildPrefix(string culture)
            => string.IsNullOrEmpty(culture) ? string.Empty : $"/{SegmentName}/{culture}";

        /// <summary>
        /// Reads the culture prefix off the beginning of the given path.
        /// </summary>
        /// <param name="path">the path to look at</param>
        /// <param name="culture">the culture, exactly as it was written in the URL</param>
        /// <param name="prefix">the prefix that was recognized, e.g. <c>/c/de-CH</c></param>
        /// <returns>true when the path starts with a culture prefix</returns>
        public static bool TryRead(string path, out string culture, out string prefix)
        {
            culture = null;
            prefix = null;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var first = SegmentAt(path, 0);
            if (!string.Equals(first, SegmentName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var second = SegmentAt(path, 1);
            if (!LooksLikeCulture(second))
            {
                return false;
            }

            culture = second;
            prefix = $"/{first}/{second}";
            return true;
        }

        /// <summary>
        /// Puts the given culture in front of the path, REPLACING one that is already there. This is what a
        /// language switch is: the same page, a different language - so the prefix is exchanged rather than
        /// stacked, and everything behind it (tenant, asset, page path) is left exactly as it is.
        /// </summary>
        /// <param name="path">the path to rewrite, with or without a culture prefix</param>
        /// <param name="culture">the culture to put in front, or null/empty to remove the prefix and leave
        /// the language to the cookie / Accept-Language</param>
        /// <returns>the rewritten path, always starting with a slash</returns>
        public static string Replace(string path, string culture)
        {
            var stripped = Strip(path);
            if (string.IsNullOrEmpty(culture))
            {
                return stripped;
            }

            return BuildPrefix(culture) + stripped;
        }

        /// <summary>
        /// Removes a leading culture prefix from the given path. Safe to call on a path that carries none.
        /// </summary>
        /// <param name="path">the path to reduce</param>
        /// <returns>the path without its culture prefix, always starting with a slash</returns>
        public static string Strip(string path)
        {
            if (!TryRead(path, out _, out var prefix))
            {
                return string.IsNullOrEmpty(path) ? "/" : path;
            }

            var rest = path.Substring(prefix.Length);
            return rest.Length == 0 ? "/" : rest;
        }

        /// <summary>
        /// Returns the first segment of the given path, or an empty string when it has none. Used to tell
        /// "this path has nothing to do with the culture prefix" from "it leads with the reserved segment
        /// name but carries no culture" - two cases that deserve very different reactions.
        /// </summary>
        /// <param name="path">the path to look at</param>
        /// <returns>the first segment, without slashes</returns>
        public static string FirstSegment(string path)
            => string.IsNullOrEmpty(path) ? string.Empty : SegmentAt(path, 0);

        /// <summary>
        /// Returns the path segment at the given index, or an empty string when the path has none.
        /// </summary>
        /// <param name="path">the path to look at</param>
        /// <param name="index">the zero-based index of the wanted segment</param>
        /// <returns>the segment, without slashes</returns>
        private static string SegmentAt(string path, int index)
        {
            var start = 0;
            var current = 0;
            while (start < path.Length)
            {
                while (start < path.Length && path[start] == '/')
                {
                    start++;
                }

                if (start >= path.Length)
                {
                    return string.Empty;
                }

                var end = path.IndexOf('/', start);
                if (end < 0)
                {
                    end = path.Length;
                }

                if (current == index)
                {
                    return path.Substring(start, end - start);
                }

                current++;
                start = end;
            }

            return string.Empty;
        }
    }
}
