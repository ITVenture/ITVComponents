using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Settings
{
    [Serializable]
    public abstract class JsonSettingsSection
    {
        public void Save()
        {
            JsonSettings.Default.Save();
        }

        /// <summary>
        /// Offers a derived class to define default-configuration-settings
        /// </summary>
        protected virtual void LoadDefaults()
        {
        }

        /// <summary>
        /// Gets the requested configuration section from the default-configuration of the current application/appDomain
        /// </summary>
        /// <typeparam name="T">the requested section-type</typeparam>
        /// <param name="name">the name of the requested section</param>
        /// <returns>a configuration section object</returns>
        public static T GetSection<T>(string name) where T : JsonSettingsSection, new()
        {
            return JsonSettings.Default.GetSetting(name, () =>
            {
                var retVal = new T();
                retVal.LoadDefaults();
                return retVal;
            });
        }
    }
}
