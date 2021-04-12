using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace ITVComponents.Settings
{
    public class JsonSettings
    {
        private static JsonSettings defaultInstance;

        private string configName;

        private bool configReady = false;

        private Dictionary<string, JsonSettingsSection> settings;

        private DateTime lastReadingTime;

        private static List<IConfigurableComponent> configComponents;

        private static object componentLock;

        private object locker = new object();

        static JsonSettings()
        {
            componentLock = new object();
            configComponents = new List<IConfigurableComponent>();
            defaultInstance = new JsonSettings();
            defaultInstance.Read(true);
        }

        private JsonSettings()
        {
            settings = new Dictionary<string, JsonSettingsSection>();
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null)
            {
                return;
            }

            configName = entryAssembly.Location + ".jsonConfig";
            configReady = true;
        }

        public static JsonSettings Default => defaultInstance;

        /// <summary>
        /// Gets a list of available settings on the current instance
        /// </summary>
        public string[] SettingsNames => settings.Keys.ToArray();

        /// <summary>
        /// Initializes the settings with a different config-file
        /// </summary>
        /// <param name="settingsLocation">the location of the config-file</param>
        public static void Initialize(string settingsLocation)
        {
            defaultInstance.configReady = true;
            defaultInstance.configName = settingsLocation;
            defaultInstance.Read(true);
        }

        /// <summary>
        /// Registers a Settings-Consumer object
        /// </summary>
        /// <param name="component">the component that is a consumer-object and does depend runtime-changable settings</param>
        public static void RegisterSettingsConsumer(IConfigurableComponent component)
        {
            lock (componentLock)
            {
                configComponents.Add(component);
            }
        }

        /// <summary>
        /// Removes a Settings-Consumer object from the list of available consumers
        /// </summary>
        /// <param name="component">the component that is a consumer-object and does depend runtime-changable settings</param>
        public static void UnRegisterSettingsConsumer(IConfigurableComponent component)
        {
            lock (componentLock)
            {
                configComponents.Remove(component);
            }
        }

        /// <summary>
        /// ReLoads the Json defnitions from the configfile
        /// </summary>
        public void Read(bool renewRequired)
        {
            lock (locker)
            {
                if (configReady)
                {
                    if (renewRequired)
                    {
                        EnterSettingsRenewMode();
                    }

                    try
                    {
                        settings.Clear();
                        if (File.Exists(configName))
                        {
                            string s;
                            using (var fst = new FileStream(configName, FileMode.Open, FileAccess.Read))
                            {
                                settings = ReadSettingsFromStream(fst);
                            }

                            lastReadingTime = DateTime.Now;
                        }
                    }
                    finally
                    {
                        if (renewRequired)
                        {
                            LeaveSettingsRenewMode();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Saves the Json definitions back to the configfile
        /// </summary>
        public void Save()
        {
            lock (locker)
            {
                if (configReady)
                {
                    using (var fst = new FileStream(configName, FileMode.Create, FileAccess.Write))
                    {
                        SaveSettingsToStream(settings, fst);
                    }

                    Read(false);
                }
            }
        }

        /// <summary>
        /// /Exposes the default-functions for writing a settings-collection to a file so that it can be used from elsewhere
        /// </summary>
        /// <param name="settings">the custom settings object you want to save</param>
        /// <param name="targetStream">the target stream to write the settings to</param>
        public static void SaveSettingsToStream(Dictionary<string, JsonSettingsSection> settings, Stream targetStream)
        {
            JsonHelper.WriteObjectStrongTyped(settings, targetStream);
        }

        /// <summary>
        /// /Exposes the default-functions for reading a settings-collection from a file so that it can be used from elsewhere
        /// </summary>
        /// <param name="sourceStream">the source stream that is expected to contain json settings</param>
        public static Dictionary<string,JsonSettingsSection> ReadSettingsFromStream(Stream sourceStream)
        {
            return JsonHelper.ReadStrongTypedObject<Dictionary<string, JsonSettingsSection>>(sourceStream);
        }

        /// <summary>
        /// Replaces an entire setting with a new value
        /// </summary>
        /// <param name="name">the name of the desired setting</param>
        /// <param name="value">the new configuration value</param>
        public void ReplaceSetting(string name, JsonSettingsSection value)
        {
            lock (locker)
            {
                if (value == null)
                {
                    throw new NullReferenceException("Setting a configuration to null is not supported.");
                }

                bool checkType = true;
                if (!settings.ContainsKey(name))
                {
                    LogEnvironment.LogDebugEvent(null, "The setting is unknown, so the value may remain unused!", (int) LogSeverity.Warning, null);
                    checkType = false;
                    //throw new InvalidOperationException("Unable to replace a setting, that is unknown!");
                }

                if (checkType && settings[name].GetType() != value.GetType())
                {
                    throw new InvalidCastException("The new value is not of the same type as the original.");
                }

                EnterSettingsRenewMode(name, value.GetType());
                try
                {
                    settings[name] = value;
                    Save();
                }
                finally
                {
                    LeaveSettingsRenewMode();
                }
            }
        }

        /// <summary>
        /// Gets a specific setting from the file-configuration
        /// </summary>
        /// <param name="name">the name of the desired setting</param>
        /// <returns>a settings-object with the requesnted identifier</returns>
        public JsonSettingsSection GetSetting(string name)
        {
            lock (locker)
            {
                CheckSettingsReload();
                if (settings.ContainsKey(name))
                {
                    return settings[name];
                }

                return null;
            }
        }

        /// <summary>
        /// Gets a specific setting that was loaded from the json configfile
        /// </summary>
        /// <typeparam name="T">the type of the desired config-setting</typeparam>
        /// <param name="name">the name of the desired config-setting</param>
        /// <param name="defaultValueCallback">a default callback that is called when no such value exists</param>
        /// <returns>the desired configuration section</returns>
        internal T GetSetting<T>(string name, Func<T> defaultValueCallback) where T : JsonSettingsSection, new()
        {
            lock (locker)
            {
                CheckSettingsReload();
                if (!settings.ContainsKey(name))
                {
                    settings.Add(name, defaultValueCallback());
                }

                return (T) settings[name];
            }
        }

        /// <summary>
        /// Checks whether the settings file has changed since the last reading of the settings
        /// </summary>
        private void CheckSettingsReload()
        {
            if (configReady)
            {
                FileInfo fi = new FileInfo(configName);
                if (fi.Exists && fi.LastWriteTime > lastReadingTime)
                {
                    Read(true);
                }
            }
        }

        /// <summary>
        /// Enters the configurationMode on configurable Components for either a specific section or globally for all components
        /// </summary>
        /// <param name="renewSettingName">the section that is being renewed</param>
        /// <param name="renewSettingType">the type of section that is being renewed</param>
        private static void EnterSettingsRenewMode(string renewSettingName = null, Type renewSettingType = null)
        {
            lock (componentLock)
            {
                foreach (var component in configComponents.Where(n => string.IsNullOrEmpty(renewSettingName) || (n.ConsumedSections?.Any(s => s.Name == renewSettingName && s.SettingsType == renewSettingType) ?? false)))
                {
                    component.EnterConfigurationMode();
                }
            }
        }

        /// <summary>
        /// Leaves the configurationMode on all components with an active configuration mode
        /// </summary>
        private static void LeaveSettingsRenewMode()
        {
            lock (componentLock)
            {
                foreach (var component in configComponents.Where(n => n.Configuring))
                {
                    component.LeaveConfigurationMode();
                }
            }
        }
    }
}