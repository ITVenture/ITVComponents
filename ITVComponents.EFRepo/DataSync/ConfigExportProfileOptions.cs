using System;
using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.EFRepo.DataSync
{
    /// <summary>
    /// Named export profiles for the system-configuration download. A profile decides which parts of the export
    /// are actually produced: the base system data and/or a selection of the registered
    /// <see cref="IConfigExtension"/> sections. Configured through the WebPart config or
    /// <c>services.Configure&lt;ConfigExportProfileOptions&gt;()</c> — deliberately NOT through the settings stored in
    /// the database, because the profiles are needed exactly when a system's configuration is exported (a fresh
    /// installation would have no settings to read them from).
    /// </summary>
    public class ConfigExportProfileOptions
    {
        /// <summary>
        /// Configured profiles by name (case-insensitive). Merged over the built-in defaults by
        /// <see cref="EffectiveProfiles"/>; a configured profile of the same name wins.
        /// </summary>
        public Dictionary<string, ConfigExportProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The configured profiles merged over the built-in <c>Full</c> and <c>BasicOnly</c>. Always contains at
        /// least those two, so the export UI keeps working on a system that configures nothing at all.
        /// </summary>
        public IReadOnlyDictionary<string, ConfigExportProfile> EffectiveProfiles()
        {
            var result = new Dictionary<string, ConfigExportProfile>(StringComparer.OrdinalIgnoreCase)
            {
                { ConfigExportProfiles.Full, new ConfigExportProfile { Description = "Complete configuration (base data and all sections)" } },
                { ConfigExportProfiles.BasicOnly, new ConfigExportProfile { ActiveExtensions = Array.Empty<string>(), Description = "Base data only, without any contributed section" } }
            };

            foreach (var kv in Profiles ?? new Dictionary<string, ConfigExportProfile>())
            {
                if (kv.Value != null)
                {
                    result[kv.Key] = kv.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves a profile by name. An unknown or empty name yields <c>Full</c> — an export that silently
        /// contains everything is recoverable, one that silently contains nothing is not.
        /// </summary>
        public ConfigExportProfile Resolve(string profileName)
        {
            var profiles = EffectiveProfiles();
            if (!string.IsNullOrWhiteSpace(profileName) && profiles.TryGetValue(profileName, out var found))
            {
                return found;
            }

            return profiles[ConfigExportProfiles.Full];
        }
    }

    /// <summary>One export profile: which sections the produced file claims.</summary>
    public class ConfigExportProfile
    {
        /// <summary>
        /// Section keys of the <see cref="IConfigExtension"/>s to include. <c>null</c> = every registered
        /// extension (so a newly installed feature library is covered without touching the configuration);
        /// an empty array = none. Uses the stable section key, not the handler type name.
        /// </summary>
        public string[] ActiveExtensions { get; set; }

        /// <summary>
        /// When true the base system data (plugins, permissions, navigation, settings, …) is left out entirely
        /// and the produced file states so, so a compare of that file does not read the absence as "delete all".
        /// </summary>
        public bool OmitBasicData { get; set; }

        /// <summary>Optional text shown next to the profile name in the download picker.</summary>
        public string Description { get; set; }

        /// <summary>True when this profile includes the given section key.</summary>
        public bool IncludesExtension(string sectionKey)
            => ActiveExtensions == null || ActiveExtensions.Contains(sectionKey, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Built-in profile names plus the encoding of a profile into the file-identifier that reaches
    /// <c>IConfigurationHandler.DescribeConfig</c>.
    /// </summary>
    public static class ConfigExportProfiles
    {
        /// <summary>Everything — the behaviour of the export before profiles existed.</summary>
        public const string Full = "Full";

        /// <summary>Base data without any contributed section.</summary>
        public const string BasicOnly = "BasicOnly";

        /// <summary>
        /// Separates the file-type from the profile name in a file-identifier (<c>sysCfg@Help</c>). Chosen to be
        /// safe inside a URL path segment: the same identifier travels through routed file endpoints, where a
        /// slash would split the route.
        /// </summary>
        public const char Separator = '@';

        /// <summary>Builds the file-identifier for a profile (<c>sysCfg</c> stays plain for <c>Full</c>).</summary>
        public static string Compose(string fileType, string profileName)
            => string.IsNullOrWhiteSpace(profileName) || string.Equals(profileName, Full, StringComparison.OrdinalIgnoreCase)
                ? fileType
                : $"{fileType}{Separator}{profileName}";

        /// <summary>Splits a file-identifier into its file-type and profile name (profile null when none is present).</summary>
        public static string Split(string fileType, out string profileName)
        {
            profileName = null;
            if (string.IsNullOrEmpty(fileType))
            {
                return fileType;
            }

            var idx = fileType.IndexOf(Separator);
            if (idx == -1)
            {
                return fileType;
            }

            profileName = fileType.Substring(idx + 1);
            return fileType.Substring(0, idx);
        }
    }
}
