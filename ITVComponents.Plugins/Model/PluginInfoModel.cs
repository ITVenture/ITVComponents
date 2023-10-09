using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Plugins.Model
{
    public class PluginInfoModel
    {
        public string UniqueName { get; set; }

        public string ConstructorString { get; set; }

        public bool Buffer { get; set; } = true;
    }
}
