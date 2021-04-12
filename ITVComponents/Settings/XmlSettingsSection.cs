using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ITVComponents.Settings
{
    [Serializable]
    public abstract class XmlSettingsSection
    {
        [XmlIgnore]
        private bool isInitial = false;

        [NonSerialized]
        private ConcurrentDictionary<string, object> bufferedObjects = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// Gets the name of this Settings section
        /// </summary>
        [XmlIgnore]
        public string Name { get; private set; }

        /// <summary>
        /// Reads a value from the settings
        /// </summary>
        /// <typeparam name="T">the Type of the setting</typeparam>
        /// <param name="name">the name of the setting</param>
        /// <param name="defaultValueFactory">the default-value factory</param>
        /// <returns>the configured value</returns>
        protected T ReadProperty<T>(string name, Func<T> defaultValueFactory) where T: class, new ()
        {
            T retVal = (T)bufferedObjects.GetOrAdd(name, n => isInitial?defaultValueFactory():new T());
            if (retVal == null)
            {
                retVal = new T();
                SetProperty(name, retVal);
            }

            return retVal;
        }

        /// <summary>
        /// Reads a value from the settings
        /// </summary>
        /// <typeparam name="T">the Type of the setting</typeparam>
        /// <param name="name">the name of the setting</param>
        /// <returns>the configured value</returns>
        protected T ReadProperty<T>(string name, T defaultValue = default(T))
        {
            T retVal = (T)bufferedObjects.GetOrAdd(name, n => defaultValue);
            return retVal;
        }

        /// <summary>
        /// Sets a Property in the settings
        /// </summary>
        /// <typeparam name="T">the Type to read from the settings</typeparam>
        /// <param name="name">the name of the setting</param>
        /// <param name="value">the value of the setting</param>
        protected void SetProperty<T>(string name, T value)
        {
            bufferedObjects.AddOrUpdate(name, n => value, (n, o) => value);
        }

        /// <summary>
        /// Saves this section to the target settings file
        /// </summary>
        public void Save()
        {
            XmlSettings.Default.WriteSetting(this, Name,GetType());
            XmlSettings.Default.Save();
        }

        /// <summary>
        /// Reads a Configuration from the configuration file
        /// </summary>
        /// <typeparam name="T">the configuration entity type</typeparam>
        /// <param name="name">the name of the configuration</param>
        /// <returns>the entire configuration section</returns>
        public static T ReadConfig<T>(string name) where T:XmlSettingsSection, new()
        {
            T retVal = XmlSettings.Default.GetSetting<T>(name, typeof(T));
            if (retVal == default(T))
            {
                retVal = new T();
                retVal.isInitial = true;
            }

            retVal.Name = name;
            return retVal;
        }
    }
}
