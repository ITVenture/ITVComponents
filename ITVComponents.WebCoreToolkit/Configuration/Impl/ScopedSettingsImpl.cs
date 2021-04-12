using System;
using ITVComponents.Helpers;

namespace ITVComponents.WebCoreToolkit.Configuration.Impl
{
    internal class ScopedSettingsImpl<TSettings>:IScopedSettings<TSettings> where TSettings:class,new()
    {
        /// <summary>
        /// the underlaying settings-provider
        /// </summary>
        private readonly IScopedSettingsProvider settingsProvider;

        /// <summary>
        /// the configured value
        /// </summary>
        private TSettings value;

        /// <summary>
        /// Injector Constructor for this scoped settings
        /// </summary>
        /// <param name="settingsProvider"></param>
        public ScopedSettingsImpl(IScopedSettingsProvider settingsProvider)
        {
            this.settingsProvider = settingsProvider;
        }

        /// <summary>
        /// Gets the deserialized Settings-value
        /// </summary>
        public TSettings Value => value ??= GetSettingsValue();

        /// <summary>
        /// Reads the settings-value from the underlaying provider
        /// </summary>
        /// <returns>the configured settings-instance or its default-value</returns>
        private TSettings GetSettingsValue()
        {
            var typeName = typeof(TSettings).Name;
            var att = (SettingNameAttribute)Attribute.GetCustomAttribute(typeof(TSettings), typeof(SettingNameAttribute), true);
            if (att != null)
            {
                typeName = att.SettingsKeyName;
            }

            var tmp = settingsProvider.GetJsonSetting(typeName);
            if (!string.IsNullOrEmpty(tmp))
            {
                var retVal = JsonHelper.FromJsonString<TSettings>(tmp);
                return retVal;
            }

            return new TSettings();
        }
    }
}
