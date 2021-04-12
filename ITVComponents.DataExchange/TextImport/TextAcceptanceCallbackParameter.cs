using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Import;

namespace ITVComponents.DataExchange.TextImport
{
    public class TextAcceptanceCallbackParameter:AcceptanceCallbackParameter<string>
    {
        /// <summary>
        /// Initializes a new instance of the TextAcceptanceCallbackParameter class
        /// </summary>
        /// <param name="success">indicates whether the provided text was imported successfully</param>
        /// <param name="firstAcceptedCharacter">the first character of the provided data</param>
        /// <param name="acceptedLength">the length that was processed by the consumer</param>
        /// <param name="dataSetName">the name of the dataset the imported data is associated with</param>
        /// <param name="data">the recognized data</param>
        /// <param name="sourceData">the sourceData that was provided by a string-source</param>
        public TextAcceptanceCallbackParameter(bool success, int firstAcceptedCharacter, int acceptedLength, string dataSetName, DynamicResult data, string sourceData) : base(success, dataSetName, data, sourceData)
        {
            FirstAcceptedCharacter = firstAcceptedCharacter;
            AcceptedLength = acceptedLength;
        }

        /// <summary>
        /// Gets the index of the first recognized character
        /// </summary>
        public int FirstAcceptedCharacter { get; }

        /// <summary>
        /// Gets the length of the processed data
        /// </summary>
        public int AcceptedLength { get; }
    }
}
