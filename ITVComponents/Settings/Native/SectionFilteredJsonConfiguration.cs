using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ITVComponents.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace ITVComponents.Settings.Native
{
    /// <summary>
    /// A JSON file that may only contribute keys below certain sections. Everything else in the file is dropped when it
    /// is loaded (and on every reload), and reported once per load - by section, never with its values.
    /// </summary>
    /// <remarks>
    /// Meant for a file that more accounts may write than the application itself (e.g. below <c>ProgramData</c>): without
    /// the restriction, any of them could override every section of the configuration, among them
    /// <c>ITVenture:AssemblyResolving</c>, which maps assembly names to arbitrary files.
    /// </remarks>
    public class SectionFilteredJsonConfigurationSource : JsonConfigurationSource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SectionFilteredJsonConfigurationSource"/> class
        /// </summary>
        /// <param name="allowedSections">the sections this file may contribute to; at least one</param>
        public SectionFilteredJsonConfigurationSource(IEnumerable<string> allowedSections)
        {
            AllowedSections = NormalizeSections(allowedSections);
            if (AllowedSections.Length == 0)
            {
                throw new ArgumentException("At least one section is required.", nameof(allowedSections));
            }
        }

        /// <summary>
        /// The sections this file may contribute to, without leading or trailing <c>:</c>.
        /// </summary>
        public string[] AllowedSections { get; }

        /// <inheritdoc />
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);
            return new SectionFilteredJsonConfigurationProvider(this);
        }

        /// <summary>
        /// Whether the given key lies within one of the sections - equal to one, or below it on a <c>:</c> boundary,
        /// ignoring case like <see cref="IConfiguration"/> itself.
        /// </summary>
        /// <param name="key">the configuration key</param>
        /// <param name="sections">the allowed sections (normalized, see <see cref="NormalizeSections"/>)</param>
        public static bool IsWithin(string key, IEnumerable<string> sections)
        {
            return sections.Any(s => key.Equals(s, StringComparison.OrdinalIgnoreCase)
                                     || (key.Length > s.Length && key[s.Length] == ':' && key.StartsWith(s, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Trims surrounding blanks and <c>:</c> and drops empty entries.
        /// </summary>
        public static string[] NormalizeSections(IEnumerable<string> sections)
        {
            return (sections ?? Array.Empty<string>())
                .Select(s => s?.Trim().Trim(':').Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    /// <summary>
    /// Loads a JSON file like <see cref="JsonConfigurationProvider"/> and keeps only the keys within
    /// <see cref="SectionFilteredJsonConfigurationSource.AllowedSections"/>.
    /// </summary>
    public class SectionFilteredJsonConfigurationProvider : JsonConfigurationProvider
    {
        private readonly SectionFilteredJsonConfigurationSource source;

        /// <summary>
        /// Initializes a new instance of the <see cref="SectionFilteredJsonConfigurationProvider"/> class
        /// </summary>
        public SectionFilteredJsonConfigurationProvider(SectionFilteredJsonConfigurationSource source) : base(source)
        {
            this.source = source;
        }

        /// <inheritdoc />
        public override void Load(Stream stream)
        {
            base.Load(stream);
            var dropped = Data.Keys.Where(k => !SectionFilteredJsonConfigurationSource.IsWithin(k, source.AllowedSections)).ToList();
            if (dropped.Count == 0)
            {
                return;
            }

            foreach (var key in dropped)
            {
                Data.Remove(key);
            }

            // The sections, not the keys or values: the values may be secrets, and one line per section is enough to see
            // why an entry has no effect.
            var sections = dropped.Select(OutsideSection)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
            var file = source.FileProvider?.GetFileInfo(source.Path)?.PhysicalPath ?? source.Path;
            LogEnvironment.LogEvent($"The settings file {file} may only contribute to {string.Join(", ", source.AllowedSections)}; ignored {dropped.Count} value(s) in: {string.Join(", ", sections)}.",
                LogSeverity.Warning);
        }

        /// <summary>
        /// The shortest part of the key that leaves all allowed sections: for <c>ITVenture:AssemblyResolving:FixedAssemblies:0:Name</c>
        /// and the allowed <c>ITVenture:AgentServices:Devices</c>, that is <c>ITVenture:AssemblyResolving</c>.
        /// </summary>
        private string OutsideSection(string key)
        {
            var parts = key.Split(':');
            for (var i = 1; i <= parts.Length; i++)
            {
                var prefix = string.Join(":", parts.Take(i));
                if (!source.AllowedSections.Any(s => s.Equals(prefix, StringComparison.OrdinalIgnoreCase) || s.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase)))
                {
                    return prefix;
                }
            }

            return key;
        }
    }
}
