using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataExchange.Import;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.KeyValueImport.Decider
{
    public class ColumnReEvaluationData
    {
        /// <summary>
        /// Initializes a new instance of the ColumnReEvaluationData class
        /// </summary>
        /// <param name="data">the provided dataset that required to re-evaluate</param>
        public ColumnReEvaluationData(IBasicKeyValueProvider data)
        {
            Data = data;
        }

        /// <summary>
        /// Gets the Current ImportContext of this re-evaluation data
        /// </summary>
        public ImportContext ImportContext { get { return ImportContext.Current; } }

        /// <summary>
        /// Gets the Base-Data that is used to perform a decision
        /// </summary>
        public IBasicKeyValueProvider Data { get; }
    }
}
