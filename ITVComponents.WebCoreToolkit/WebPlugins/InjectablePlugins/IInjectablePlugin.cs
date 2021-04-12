using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins
{
    /// <summary>
    /// Enables any object to consume a Configurable Plugin via dependencyInjection
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IInjectablePlugin<T> where T:class,IPlugin
    {
        /// <summary>
        /// Gets the value of the injected PlugIn
        /// </summary>
        T Instance { get; }
    }
}
