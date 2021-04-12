using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.ExcelSource.Data
{
    public class ExcelDataDictionaryWrapper:IBasicKeyValueProvider
    {
        /// <summary>
        /// the wrapped dictionary
        /// </summary>
        private IDictionary<string, ExcelDataRecord> wrapped;

        private ImportMode mode;

        /// <summary>
        /// Initialiezs a new instance of the DictionaryWrapper class
        /// </summary>
        /// <param name="wrapped">the wrapped dictionary</param>
        public ExcelDataDictionaryWrapper(IDictionary<string, ExcelDataRecord> wrapped, ImportMode mode)
        {
            if (mode == ImportMode.TextPlusActualTypes)
            {
                throw new ArgumentException("Unsupported for DictionaryWrapper", nameof(mode));
            }

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
                    if (mode == ImportMode.ActualTypes)
                    {
                        retVal = wrapped[name].RawData;
                    }
                    else
                    {
                        retVal = wrapped[name].FormattedText;
                    }
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
