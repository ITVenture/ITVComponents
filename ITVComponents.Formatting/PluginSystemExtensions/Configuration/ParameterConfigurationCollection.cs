using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Formatting.PluginSystemExtensions.Configuration
{
    [Serializable]
    public class ParameterConfigurationCollection:List<ParameterConfiguration>
    {
        public ParameterConfiguration this[string identifier]
        {
            get { return this.FirstOrDefault(n => n.ConstIdentifier == identifier); }
        }
    }
}
