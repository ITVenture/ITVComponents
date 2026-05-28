using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.KeyValueImport.Data
{
    public class CsvDictionaryWrapper:IBasicKeyValueProvider
    {
        /// <summary>
        /// the wrapped dictionary
        /// </summary>
        private IDictionary<string, CsvDataRecord> wrapped;

        /// <summary>
        /// Initialiezs a new instance of the CsvDictionaryWrapper class
        /// </summary>
        /// <param name="wrapped">the wrapped dictionary</param>
        public CsvDictionaryWrapper(IDictionary<string, CsvDataRecord> wrapped)
        {
            this.wrapped = wrapped;
        }

        #region Implementation of IBasicKeyValueProvider

        /// <summary>
        /// Gets the value that is associated with the given name
        /// </summary>
        /// <param name="name">the name requested by an object</param>
        /// <returns>the value associated with the given key</returns>
        public object this[string name]
        {
            get
            {
                object retVal = null;
                if (wrapped.ContainsKey(name))
                {
                    var tmp = wrapped[name];
                    retVal = tmp.Converted ?? tmp.RawText;
                }

                return retVal;
            }
        }

        /// <summary>
        /// Gets all MemberNames that are associated with this basicKeyValue provider instance
        /// </summary>
        public string[] Keys { get { return wrapped.Keys.ToArray(); } }

        /// <summary>
        /// Gets a value indicating whether a specific key is present in this basicKeyValueProvider instance
        /// </summary>
        /// <param name="key">the Key for which to check</param>
        /// <returns>a value indicating whether the specified key exists in this provider</returns>
        public bool ContainsKey(string key)
        {
            return wrapped.ContainsKey(key);
        }

        #endregion
    }
}
