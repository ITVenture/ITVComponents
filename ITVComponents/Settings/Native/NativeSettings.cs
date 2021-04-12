using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ITVComponents.Settings.Native
{
    public static class NativeSettings
    {
        private static IConfigurationBuilder builder;
        public static IConfiguration Configuration { get; private set; }

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
                var startAssembly = Assembly.GetEntryAssembly();
                var settingsFileName = "appsettings.json";
                if (startAssembly != null)
                {
                    string tmp = Path.GetFileNameWithoutExtension(startAssembly.Location);
                    settingsFileName = $"{tmp}.appsettings.json";
                }

                RawSettingsName = Path.GetFileNameWithoutExtension(settingsFileName);
                builder = new ConfigurationBuilder().AddJsonFile(settingsFileName, optional:true);
            }
        }

        public static T GetSection<T>(string path, Action<T> configureDefaults = null) where T : class, new()
        {
            if (Configuration == null)
            {
                Configuration = Builder.Build();
            }

            var retVal = new T();
            var cfg = Configuration.GetSection(path);
            if (cfg.Exists())
            {
                cfg.Bind(retVal);
            }
            else
            {
                configureDefaults?.Invoke(retVal);
            }

            return retVal;
        }
    }
}
