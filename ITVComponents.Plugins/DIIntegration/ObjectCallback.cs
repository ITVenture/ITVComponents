using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Plugins.DIIntegration
{
    public class ObjectCallback
    {
        public Func<IPluginFactory, object> GetPlugin { get; set; }
    }
}
