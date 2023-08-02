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
