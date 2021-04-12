using System.Collections.Generic;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Import;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.DictionaryTableImport
{
    public class DictionaryAcceptanceCallbackParameter<TValue>:AcceptanceCallbackParameter<IDictionary<string,TValue>>
    {
        /// <summary>
        /// Initializes a new instance of the TextAcceptanceCallbackParameter class
        /// </summary>
        /// <param name="success">indicates whether the provided text was imported successfully</param>
        /// <param name="dataSetName">the name of the dataset the imported data is associated with</param>
        /// <param name="data">the recognized data</param>
        /// <param name="sourceData">the sourceData that was provided by a string-source</param>
        /// <param name="acceptedKeys">the data-keys that were accepted by the processing reader</param>
        public DictionaryAcceptanceCallbackParameter(bool success, string dataSetName, DynamicResult data, IDictionary<string,TValue> sourceData, string[] acceptedKeys) : base(success, dataSetName, data, sourceData)
        {
            AcceptedKeys = acceptedKeys;
        }

        /// <summary>
        /// Gets an array identifying the accepted keys of the source
        /// </summary>
        public string[] AcceptedKeys { get; }
    }
}
