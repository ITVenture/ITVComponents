using System;

namespace ITVComponents.UpdateService.Abstractions.Feed
{
    /// <summary>
    /// A version range in NuGet notation, as <see cref="UpdatePackage.Requires"/> uses it: <c>[1.4.0,2.0.0)</c>,
    /// <c>(1.0,]</c>, <c>[1.4.2]</c> (exactly), <c>1.4.0</c> (at least).
    /// </summary>
    public sealed class VersionRange
    {
        private VersionRange(Version? minimum, bool minimumInclusive, Version? maximum, bool maximumInclusive)
        {
            Minimum = minimum;
            MinimumInclusive = minimumInclusive;
            Maximum = maximum;
            MaximumInclusive = maximumInclusive;
        }

        /// <summary>Gets the lower bound, or null.</summary>
        public Version? Minimum { get; }

        /// <summary>Gets a value indicating whether the lower bound itself belongs to the range.</summary>
        public bool MinimumInclusive { get; }

        /// <summary>Gets the upper bound, or null.</summary>
        public Version? Maximum { get; }

        /// <summary>Gets a value indicating whether the upper bound itself belongs to the range.</summary>
        public bool MaximumInclusive { get; }

        /// <summary>
        /// Reads a range.
        /// </summary>
        /// <exception cref="FormatException">when the text is no range</exception>
        public static VersionRange Parse(string text)
        {
            var t = text?.Trim() ?? throw new FormatException("A version range must not be empty.");
            if (t.Length == 0)
            {
                throw new FormatException("A version range must not be empty.");
            }

            if (t[0] != '[' && t[0] != '(')
            {
                return new VersionRange(ParseVersion(t), true, null, false);
            }

            var last = t[^1];
            if (last != ']' && last != ')')
            {
                throw new FormatException($"The version range '{text}' is not closed.");
            }

            var inner = t.Substring(1, t.Length - 2);
            var comma = inner.IndexOf(',');
            if (comma < 0)
            {
                if (t[0] != '[' || last != ']')
                {
                    throw new FormatException($"The exact version '{text}' needs square brackets.");
                }

                var exact = ParseVersion(inner);
                return new VersionRange(exact, true, exact, true);
            }

            var lower = inner.Substring(0, comma).Trim();
            var upper = inner.Substring(comma + 1).Trim();
            return new VersionRange(lower.Length == 0 ? null : ParseVersion(lower), t[0] == '[',
                upper.Length == 0 ? null : ParseVersion(upper), last == ']');
        }

        /// <summary>
        /// Indicates whether a version lies in the range.
        /// </summary>
        public bool Includes(Version version)
        {
            var v = Normalize(version);
            if (Minimum != null)
            {
                var c = v.CompareTo(Normalize(Minimum));
                if (c < 0 || (c == 0 && !MinimumInclusive))
                {
                    return false;
                }
            }

            if (Maximum != null)
            {
                var c = v.CompareTo(Normalize(Maximum));
                if (c > 0 || (c == 0 && !MaximumInclusive))
                {
                    return false;
                }
            }

            return true;
        }

        private static Version ParseVersion(string text)
        {
            var core = text.Split('-', '+')[0].Trim();
            if (!Version.TryParse(core.Contains('.') ? core : core + ".0", out var version))
            {
                throw new FormatException($"'{text}' is not a version.");
            }

            return version;
        }

        private static Version Normalize(Version v) => new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
    }
}
