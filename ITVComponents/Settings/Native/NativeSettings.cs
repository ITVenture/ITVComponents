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
        /// wenn keine registriert ist (<see cref="AddUserSettings"/>).
        /// </summary>
        public static string UserSettingsFile { get; private set; }

        /// <summary>
        /// Haengt die Datei an, in die zur Laufzeit geschrieben wird, und merkt sie sich - wer schreibt, findet
        /// sie unter <see cref="UserSettingsFile"/>, und was er schreibt, ist ueber <see cref="GetSection{T}"/>
        /// lesbar.
        /// </summary>
        /// <param name="fileName">die Datei; sie darf (noch) fehlen</param>
        /// <remarks>
        /// Muss vor dem ersten Lesen der Einstellungen laufen: was erst danach angehaengt wird, sieht die
        /// bereits gebaute <see cref="Configuration"/> nicht.
        /// </remarks>
        public static void AddUserSettings(string fileName)
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

            UserSettingsFile = Path.GetFullPath(fileName);
            Builder.AddJsonFile(UserSettingsFile, optional: true, reloadOnChange: true);
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
