using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.WebPlugins.Initialization
{
    public interface IPluginsInitOptions
    {
        /// <summary>
        /// Indicates whether automatic plugins have been initialized yet
        /// </summary>
        bool PluginsInitialized { get; }

        /// <summary>
        /// Runs the Plugin-Initialization
        /// </summary>
        /// <param name="initAction">the init action to run for plugins</param>
        void Init(Action initAction);

        /// <summary>
        /// Sets the parent of a scoped option-object
        /// </summary>
        /// <param name="parent">the global parent of this instance</param>
        void SetParent(IPluginsInitOptions parent);
    }
}
