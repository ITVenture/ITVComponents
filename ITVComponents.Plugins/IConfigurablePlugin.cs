using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings;

namespace ITVComponents.Plugins
{
    /// <summary>
    /// Plugin definition that enables a component to be fully configured by the ITVComponents JsonSettings
    /// </summary>
    public interface IConfigurablePlugin:IPlugin, IConfigurableComponent, IDeferredInit
    {
        /// <summary>
        /// Instructs the Plugin to read the JsonSettings or to create a default instance if none is available
        /// </summary>
        void ReadSettings();
    }
}
