using System;

namespace ITVComponents.WebCoreToolkit.Configuration
{
    /// <summary>
    /// Attribute for decorating Settings objects when they are injected using the IScopedSettings class
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false,Inherited=true)]
    public class SettingNameAttribute:Attribute
    {
        /// <summary>
        /// Gets the SettingsKey for the decorated settings class
        /// </summary>
        public string SettingsKeyName { get; }
        
        /// <summary>
        /// Initializes a new instance of the ScopedSettingAttribute class
        /// </summary>
        /// <param name="settingsKeyName">the Settingskey for the decorated settings-class</param>
        public SettingNameAttribute(string settingsKeyName)
        {
            SettingsKeyName = settingsKeyName;
        }
    }
}
