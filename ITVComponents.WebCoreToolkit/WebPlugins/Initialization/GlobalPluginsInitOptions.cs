using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.WebPlugins.Initialization
{
    /// <summary>
    /// Handles the automatic initialization of the plugin-system
    /// </summary>
    public class GlobalPluginsInitOptions:IPluginsInitOptions
    {
        /// <summary>
        /// a logger instance that is used to log performed actions
        /// </summary>
        private readonly ILogger<GlobalPluginsInitOptions> logger;

        /// <summary>
        /// Indicates whether this is the global singleton instance
        /// </summary>
        private bool isGlobal = true;

        /// <summary>
        /// the parent instance of this object
        /// </summary>
        private IPluginsInitOptions parent;

        /// <summary>
        /// Indicates on the global object whether the auto-plugins have been initialized
        /// </summary>
        private bool pluginsInitialized;

        /// <summary>
        /// an object that is used to synchronize the initialization process
        /// </summary>
        private object objLock = new object();

        public GlobalPluginsInitOptions(ILogger<GlobalPluginsInitOptions> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Indicates whether automatic plugins have been initialized yet
        /// </summary>
        public bool PluginsInitialized => isGlobal ? pluginsInitialized : parent.PluginsInitialized;

        /// <summary>
        /// Runs the Plugin-Initialization
        /// </summary>
        /// <param name="initAction">the init action to run for plugins</param>
        public void Init(Action initAction)
        {
            if (isGlobal)
            {
                lock (objLock)
                {
                    if (!pluginsInitialized)
                    {
                        try
                        {
                            logger.LogDebug("Calling InitAction...");
                            initAction();
                            logger.LogDebug("Done.");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "InitAction has failed!");
                        }
                        finally
                        {
                            pluginsInitialized = true;
                        }
                    }
                }
            }
            else
            {
                parent.Init(initAction);
            }
        }

        /// <summary>
        /// Sets the parent of a scoped option-object
        /// </summary>
        /// <param name="parent">the global parent of this instance</param>
        public void SetParent(IPluginsInitOptions parent)
        {
            if (parent != null)
            {
                isGlobal = false;
                this.parent = parent;
            }
        }
    }
}
