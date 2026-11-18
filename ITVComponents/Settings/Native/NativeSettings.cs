using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;
using Microsoft.Extensions.Configuration;

namespace ITVComponents.Settings.Native
{
    public static class NativeSettings
    {
        private static IConfigurationBuilder builder;
        private static IConfiguration configuration;
        private static IDisposable reloadToken;
        private static readonly List<IConfiguration> attachedConfigurations = new();
        private static readonly object attachLock = new();
        public static IConfiguration Configuration
        {
            get
            {
                if (configuration == null)
                {
                    configuration = Builder.Build();
                    reloadToken = configuration.GetReloadToken().RegisterChangeCallback(ConfigReload, null);
                }

                return configuration; 

            }
            private set => configuration = value;
        }

        public static IConfigurationBuilder Builder
        {
            get
            {
                if (builder == null)
                {
                    Init();
                }
                
                return builder;
            }
        }

        public static string RawSettingsName { get; private set;}

        public static string SettingsBasePath { get; private set; }

        /// <summary>
        /// Die Datei, in die zur Laufzeit geschrieben wird (<see cref="JsonUserSettings"/>), oder <c>null</c>,
        /// wenn keine registriert ist (<see cref="AddUserSettings(string)"/>).
        /// </summary>
        public static string UserSettingsFile { get; private set; }

        /// <summary>
        /// Die Abschnitte, die <see cref="UserSettingsFile"/> beisteuern darf, oder <c>null</c>, wenn die Datei
        /// alles uebersteuern darf (<see cref="AddUserSettings(string, string[])"/>).
        /// </summary>
        public static string[] UserSettingsSections { get; private set; }

        /// <summary>
        /// Haengt die Datei an, in die zur Laufzeit geschrieben wird, und merkt sie sich - wer schreibt, findet
        /// sie unter <see cref="UserSettingsFile"/>, und was er schreibt, ist ueber <see cref="GetSection{T}"/>
        /// lesbar. Die Datei darf jeden Abschnitt der Konfiguration uebersteuern.
        /// </summary>
        /// <param name="fileName">die Datei; sie darf (noch) fehlen</param>
        /// <remarks>
        /// <para>
        /// Muss vor dem ersten Lesen der Einstellungen laufen: was erst danach angehaengt wird, sieht die
        /// bereits gebaute <see cref="Configuration"/> nicht.
        /// </para>
        /// <para>
        /// Duerfen mehr Konten in die Datei schreiben als die Anwendung selbst (z.B. unter <c>ProgramData</c>),
        /// gehoert sie mit <see cref="AddUserSettings(string, string[])"/> auf ihre Abschnitte beschraenkt: sonst
        /// uebersteuert jedes dieser Konten auch <c>ITVenture:AssemblyResolving</c> oder
        /// <c>ITVenture:Plugins:Constants</c> - und damit, welcher Code in der Anwendung eines anderen Kontos laeuft.
        /// </para>
        /// </remarks>
        public static void AddUserSettings(string fileName)
        {
            AddUserSettings(fileName, null);
        }

        /// <summary>
        /// Wie <see cref="AddUserSettings(string)"/>, aber die Datei steuert nur Schluessel unter den angegebenen
        /// Abschnitten bei (Vergleich an der <c>:</c>-Grenze, ohne Ruecksicht auf Gross-/Kleinschreibung). Alles
        /// andere darin wird beim Laden und bei jedem Neuladen verworfen und je Laden einmal als Warnung gemeldet.
        /// </summary>
        /// <param name="fileName">die Datei; sie darf (noch) fehlen</param>
        /// <param name="allowedSections">die Abschnitte, z.B. <c>ITVenture:AgentServices:Devices</c>; keine = alles erlaubt</param>
        /// <remarks>
        /// Ein Abschnitt, den das Toolkit nur einmal liest (z.B. <c>ITVenture:AssemblyResolving</c> beim ersten
        /// Aufloesen einer Assembly), ist nur geschuetzt, wenn die Datei vorher angehaengt wurde.
        /// </remarks>
        public static void AddUserSettings(string fileName, params string[] allowedSections)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (configuration != null)
            {
                LogEnvironment.LogEvent($"The user settings file {fileName} is added after the configuration was built; its values are not visible until NativeSettings is reset.",
                    LogSeverity.Warning);
            }

            var sections = SectionFilteredJsonConfigurationSource.NormalizeSections(allowedSections);
            if (allowedSections != null && allowedSections.Length != 0 && sections.Length == 0)
            {
                // Only blank entries: almost certainly a mistake, and "everything" would be the opposite of what was asked.
                throw new ArgumentException("The allowed sections contain no section name.", nameof(allowedSections));
            }

            UserSettingsFile = Path.GetFullPath(fileName);
            if (sections.Length == 0)
            {
                UserSettingsSections = null;
                Builder.AddJsonFile(UserSettingsFile, optional: true, reloadOnChange: true);
                return;
            }

            UserSettingsSections = sections;
            var source = new SectionFilteredJsonConfigurationSource(sections)
            {
                Path = UserSettingsFile,
                Optional = true,
                ReloadOnChange = true
            };
            source.ResolveFileProvider();
            Builder.Add(source);
        }

        /// <summary>
        /// Ob <see cref="UserSettingsFile"/> unter diesem Pfad etwas beisteuern darf - immer, wenn die Datei nicht
        /// beschraenkt ist.
        /// </summary>
        /// <param name="path">der Pfad eines Abschnitts oder Schluessels</param>
        public static bool UserSettingsAllow(string path)
        {
            var sections = UserSettingsSections;
            return sections == null || SectionFilteredJsonConfigurationSource.IsWithin(path ?? string.Empty, sections);
        }

        /// <summary>
        /// Liest die Konfiguration sofort neu ein - nach einem Schreiben in <see cref="UserSettingsFile"/>
        /// gilt der neue Stand so ab dem naechsten Lesen und nicht erst, wenn die Dateiueberwachung anschlaegt.
        /// </summary>
        public static void Reload()
        {
            (Configuration as IConfigurationRoot)?.Reload();
        }

        public static void Init(bool reset = false)
        {
            if (reset || builder == null)
            {
                Configuration = null;
                reloadToken?.Dispose();
                reloadToken = null;
                UserSettingsFile = null;
                UserSettingsSections = null;
                var startAssembly = Assembly.GetEntryAssembly();
                var settingsFileName = "appsettings.json";
                if (startAssembly != null)
                {
                    string tmp = Path.GetFileNameWithoutExtension(startAssembly.Location);
                    settingsFileName = $"{tmp}.appsettings.json";
                }

                RawSettingsName = Path.GetFileNameWithoutExtension(settingsFileName);
                // Die Basis ist das Anwendungsverzeichnis, nicht das Arbeitsverzeichnis: dorthin loest die
                // Konfiguration einen relativen Dateinamen ohnehin auf. SetBasePath macht das ausdruecklich und
                // bindet SettingsBasePath an DIESELBE Stelle - beim Autostart, als Dienst oder mit dotnet run
                // fallen die beiden sonst auseinander, und daneben abgelegte Dateien landen anderswo.
                SettingsBasePath = AppContext.BaseDirectory;
                builder = new ConfigurationBuilder().SetBasePath(SettingsBasePath).AddJsonFile(settingsFileName,
                    optional: true, reloadOnChange: true);
                lock (attachLock)
                {
                    attachedConfigurations.ForEach(n => builder.AddConfiguration(n, false));
                }
            }
        }

        /// <summary>
        /// Attaches a host-configuration (e.g. the configuration of an ASP.NET application) to the NativeSettings, so values
        /// that are only available in the host-configuration (appsettings.json, appsettings.{Env}.json, environment-variables, ...)
        /// can be read through <see cref="Configuration"/> as well. Values of the host-configuration take precedence over the
        /// values in the &lt;EntryAssembly&gt;.appsettings.json file. An already built <see cref="Configuration"/> is rebuilt on next access.
        /// </summary>
        /// <param name="hostConfiguration">the configuration to attach</param>
        public static void AttachConfiguration(IConfiguration hostConfiguration)
        {
            if (hostConfiguration == null)
            {
                throw new ArgumentNullException(nameof(hostConfiguration));
            }

            lock (attachLock)
            {
                if (attachedConfigurations.Any(n => ReferenceEquals(n, hostConfiguration)))
                {
                    LogEnvironment.LogDebugEvent("The provided host-configuration is already attached to the Native-Settings.", LogSeverity.Report);
                    return;
                }

                attachedConfigurations.Add(hostConfiguration);
                Builder.AddConfiguration(hostConfiguration, false);
                if (configuration != null)
                {
                    reloadToken?.Dispose();
                    reloadToken = null;
                    configuration = null;
                    LogEnvironment.LogDebugEvent("Native-Settings will be rebuilt because a host-configuration was attached.", LogSeverity.Report);
                }
            }
        }

        public static T GetSection<T>(string path, Action<T> configureDefaults = null) where T : class, new()
        {
            return Configuration.GetSection(path, configureDefaults);
        }

        /// <summary>
        /// Wie <see cref="GetSection{T}"/>, fuer einen Typ, der erst zur Laufzeit feststeht.
        /// </summary>
        /// <param name="path">der Pfad des Abschnitts</param>
        /// <param name="type">der Typ, in den gelesen wird; braucht einen parameterlosen Konstruktor</param>
        public static object GetSection(string path, Type type)
        {
            return Configuration.GetSection(path, type);
        }

        /// <summary>Ob es einen Abschnitt unter diesem Pfad gibt.</summary>
        /// <param name="path">der Pfad des Abschnitts</param>
        public static bool SectionExists(string path)
        {
            return Configuration.GetSection(path).Exists();
        }

        public static string ReadSection(string path)
        {
            return configuration.GetSection(path).Value;
        }



        private static void ConfigReload(object obj)
        {
            LogEnvironment.LogDebugEvent("Native-Settings reload occurred.", LogSeverity.Report);
        }
    }
}
