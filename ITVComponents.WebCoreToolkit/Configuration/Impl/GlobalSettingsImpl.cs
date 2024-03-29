﻿using System;
using ITVComponents.Helpers;

namespace ITVComponents.WebCoreToolkit.Configuration.Impl
{
    internal class GlobalSettingsImpl<TSettings>:IGlobalSettings<TSettings> where TSettings:class,new()
    {
        /// <summary>
        /// the underlaying settings-provider
        /// </summary>
        private readonly IGlobalSettingsProvider settingsProvider;

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
        /// <param name="settingsProvider"></param>
        public GlobalSettingsImpl(IGlobalSettingsProvider settingsProvider)
        {
            this.settingsProvider = settingsProvider;
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
                var retVal = JsonHelper.FromJsonString<TSettings>(tmp);
                return retVal;
            }

            return default;
        }
    }
}
