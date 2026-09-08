using System;
using ITVComponents.Json;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Configuration.Impl
{
    internal class GlobalSettingsImpl<TSettings>:IGlobalSettings<TSettings> where TSettings:class,new()
    {
        /// <summary>
        /// the underlaying settings-provider
        /// </summary>
        private readonly IGlobalSettingsProvider settingsProvider;

        /// <summary>
        /// the logger used to make a missing or unusable setting visible
        /// </summary>
        private readonly ILogger<GlobalSettingsImpl<TSettings>> logger;

        /// <summary>
        /// the configured value
        /// </summary>
        private TSettings value;

        /// <summary>
        /// the configured value or null
        /// </summary>
        private TSettings valueOrDefault;

        /// <summary>
        /// Injector Constructor for this scoped settings
        /// </summary>
        /// <param name="settingsProvider">the provider that reads the raw setting</param>
        /// <param name="logger">the logger that records whether a setting was found and usable</param>
        public GlobalSettingsImpl(IGlobalSettingsProvider settingsProvider, ILogger<GlobalSettingsImpl<TSettings>> logger)
        {
            this.settingsProvider = settingsProvider;
            this.logger = logger;
        }

        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, an object is constructed, using the Default-Constructor.
        /// </summary>
        public TSettings Value => value ??= ValueOrDefault ?? new TSettings();

        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, null is returned (-> default(TSettings)).
        /// </summary>
        public TSettings ValueOrDefault => valueOrDefault ??= GetSettingsValue(null);

        public TSettings GetValue(string explicitSettingName)
        {
            return GetValueOrDefault(explicitSettingName) ?? new TSettings();
        }

        public TSettings GetValueOrDefault(string explicitSettingName)
        {
            return GetSettingsValue(explicitSettingName);
        }

        /// <summary>
        /// Reads the settings-value from the underlaying provider
        /// </summary>
        /// <returns>the configured settings-instance or its default-value</returns>
        private TSettings GetSettingsValue(string? explicitSettingName)
        {
            var typeName = explicitSettingName;
            if (string.IsNullOrEmpty(typeName))
            {
                typeName = typeof(TSettings).Name;
                var att = (SettingNameAttribute)Attribute.GetCustomAttribute(typeof(TSettings),
                    typeof(SettingNameAttribute), true);
                if (att != null)
                {
                    typeName = att.SettingsKeyName;
                }

            }

            var tmp = settingsProvider.GetJsonSetting(typeName);
            if (!string.IsNullOrEmpty(tmp))
            {
                var retVal = JsonHelper.FromJsonString<TSettings>(tmp, SerializationTypingMode.StaticTyping);
                if (retVal == null)
                {
                    logger?.LogWarning(
                        "Global setting '{SettingsKey}' was found ({Length} characters) but deserialized to nothing. The stored value does not match {SettingsType}.",
                        typeName, tmp.Length, typeof(TSettings).FullName);
                }
                else
                {
                    logger?.LogDebug("Global setting '{SettingsKey}' resolved from the global provider ({Length} characters).", typeName, tmp.Length);
                }

                return retVal;
            }

            logger?.LogDebug("Global setting '{SettingsKey}' is not configured on the global provider ({Provider}).",
                typeName, settingsProvider?.GetType().FullName ?? "<none>");
            return default;
        }
    }
}
