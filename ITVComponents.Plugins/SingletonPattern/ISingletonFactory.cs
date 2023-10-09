using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Plugins.SingletonPattern
{
    /// <summary>
    /// Marker Interface that is required for DI
    /// </summary>
    public interface ISingletonFactory:IPluginFactory
    {
    }
}
