using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataExchange.Configuration;

namespace ITVComponents.DataExchange.KeyValueImport.Config
{
    [Serializable]
    public class KeyValueConfiguration
    {
        public string TableName { get; set; }

        public ColumnConfigurationCollection Columns { get; } = new ColumnConfigurationCollection();

        public ConstConfigurationCollection VirtualColumns { get; } = new ConstConfigurationCollection();
    }
}
