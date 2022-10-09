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
    public interface IInjectablePlugin<T>: IDisposable where T:class,IPlugin
    {
        /// <summary>
        /// Gets the value of the injected PlugIn
        /// </summary>
        T Instance { get; }

        /// <summary>
        /// Gets a specific Instance of the initialized PluginType
        /// </summary>
        /// <param name="name">the name of the desired PlugIn</param>
        /// <returns>the Plugin-Instance that was requested by the caller</returns>
        T GetInstance(string name);
    }
}
