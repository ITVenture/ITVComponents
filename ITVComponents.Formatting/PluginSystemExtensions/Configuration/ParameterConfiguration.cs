using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings;
using Newtonsoft.Json;

namespace ITVComponents.Formatting.PluginSystemExtensions.Configuration
{
    [Serializable]
    public class ParameterConfiguration
    {
        public string ConstIdentifier { get; set; }

        [JsonConverter(typeof(JsonStringEncryptConverter))]
        public string ConstValue { get; set; }
    }
}
