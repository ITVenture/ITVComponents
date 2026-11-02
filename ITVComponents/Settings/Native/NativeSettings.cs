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

        public static void Init(bool reset = false)
        {
            if (reset || builder == null)
            {
                Configuration = null;
                reloadToken?.Dispose();
                reloadToken = null;
                var startAssembly = Assembly.GetEntryAssembly();
                var settingsFileName = "appsettings.json";
                if (startAssembly != null)
                {
                    string tmp = Path.GetFileNameWithoutExtension(startAssembly.Location);
                    settingsFileName = $"{tmp}.appsettings.json";
                }

                RawSettingsName = Path.GetFileNameWithoutExtension(settingsFileName);
                builder = new ConfigurationBuilder().AddJsonFile(settingsFileName, optional: true,
                    reloadOnChange: true);
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
