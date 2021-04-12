using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.SimpleReporting.Config
{
    public class DocumentConfiguration
    {
        /// <summary>
        /// Identifier of the current configuration object
        /// </summary>
        private string configTag;

        /// <summary>
        /// the parent configuration of this config object
        /// </summary>
        private DocumentConfiguration parent;

        /// <summary>
        /// Holds all configurations that are provided by a consumer object
        /// </summary>
        private Dictionary<string, object> configuration = new Dictionary<string, object>();

        /// <summary>
        /// Holds all configuration identifiers and their expected types for a specific renderer
        /// </summary>
        private Dictionary<string, Type> types = new Dictionary<string, Type>();

        /// <summary>
        /// Initializes a new instance of the DocumentConfiguration class
        /// </summary>
        /// <param name="configTag">Tag that helps identifying this configuration object</param>
        /// <param name="keys">the keys that are expected by the current renderer</param>
        /// <param name="types">the types that are expected for the provided keys</param>
        internal DocumentConfiguration(string configTag, string[] keys, Type[] types)
        {
            this.configTag = configTag;
            for (int i = 0; i < keys.Length; i++)
            {
                this.types.Add(keys[i], types[i]);
            }
        }

        /// <summary>
        /// Initializes a new instance of the DocumentConfiguration class
        /// </summary>
        /// <param name="configTag">Tag that helps identifying this configuration object</param>
        /// <param name="baseConfiguration">the base configuration to use for the provided configuration</param>
        /// <param name="keys">the keys that must be used</param>
        /// <param name="types">the Types that must be used for the specified settings names</param>
        public DocumentConfiguration(string configTag, DocumentConfiguration baseConfiguration, string[] keys, Type[] types):this(configTag, keys,types)
        {
            foreach (var item in baseConfiguration.types)
            {
                if (!this.types.ContainsKey(item.Key))
                {
                    this.types.Add(item.Key,item.Value);
                }

                if (baseConfiguration.configuration.ContainsKey(item.Key))
                {
                    /*LogEnvironment.LogEvent($"Copying Config {item.Key} -> {baseConfiguration.configuration[item.Key]}",
                        LogSeverity.Report);*/
                    configuration[item.Key] = baseConfiguration.configuration[item.Key];
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the DocumentConfiguration class
        /// </summary>
        /// <param name="configTag">Tag that helps identifying this configuration object</param>
        /// <param name="baseConfiguration">the base configuration to use for the provided configuration</param>
        /// <param name="parent">the parent that can be used when requested configurations are not set on this object</param>
        /// <param name="keys">the keys that must be used</param>
        /// <param name="types">the Types that must be used for the specified settings names</param>
        public DocumentConfiguration(string configTag, DocumentConfiguration baseConfiguration, DocumentConfiguration parent, string[] keys,
            Type[] types) : this(configTag, baseConfiguration, keys, types)
        {
            this.parent = parent;
        }

        /// <summary>
        /// Gets a list of expected settings
        /// </summary>
        public string[] AvailableSettingNames { get { return types.Keys.ToArray(); } }

        /// <summary>
        /// Gets the specified setting value
        /// </summary>
        /// <typeparam name="T">the Expected Type for the given setting</typeparam>
        /// <param name="identifier">the identifier for which to get a setting</param>
        /// <returns>the expected value or the default value for the provided type</returns>
        public T GetValue<T>(string identifier)
        {
            T retVal = default(T);
            if (types.ContainsKey(identifier))
            {
                if (parent != null)
                {
                    retVal = parent.GetValue<T>(identifier);
                }

                if (configuration.ContainsKey(identifier))
                {

                    object value = configuration[identifier];
                    if (value is T variable)
                    {
                        retVal = variable;
                    }
                }
            }

            //LogEnvironment.LogEvent($"{configTag} Returning value of {identifier} --> {retVal}", LogSeverity.Report);
            return retVal;
        }

        /// <summary>
        /// Sets the value that is expected by the used renderer
        /// </summary>
        /// <typeparam name="T">the Type of the provided value</typeparam>
        /// <param name="identifier">the identifier of the expected value</param>
        /// <param name="value">the value for the given setting</param>
        public void SetValue<T>(string identifier, T value)
        {
            //LogEnvironment.LogEvent($"{configTag} Setting {identifier} to {value}", LogSeverity.Report);
            Type t = typeof(T);
            if (types.ContainsKey(identifier))
            {
                if (types[identifier].IsAssignableFrom(t))
                {
                    configuration[identifier] = value;
                }
            }
        }
    }
}
