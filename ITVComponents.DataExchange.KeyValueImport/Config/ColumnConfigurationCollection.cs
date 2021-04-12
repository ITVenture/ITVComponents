using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.DataExchange.KeyValueImport.Config
{
    [Serializable]
    public class ColumnConfigurationCollection : List<ColumnConfiguration>
    {
        /// <summary>
        /// Gets the value of a query with the specified name
        /// </summary>
        /// <param name="name">the name of the requested item</param>
        /// <returns>the  requeted item or null if it does not exist</returns>
        public ColumnConfiguration this[string name] => this.FirstOrDefault(n => name.Equals(n.RawDataKey, StringComparison.OrdinalIgnoreCase));
    }
}
