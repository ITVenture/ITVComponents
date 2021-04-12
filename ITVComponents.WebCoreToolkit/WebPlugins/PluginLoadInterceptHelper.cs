using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.WebPlugins
{
    public static class PluginLoadInterceptHelper
    {
        /// <summary>
        /// Holds a list of interceptors that need to be called when a plugin is being initialized
        /// </summary>
        private static List<Action<PluginFactory, IPlugin>> pluginInterceptors = new List<Action<PluginFactory, IPlugin>>();

        /// <summary>
        /// Registers a handler that will be fired for each plugin that is being initialized in a specific context
        /// </summary>
        /// <param name="action">the action to execute for each plugin initialization</param>
        public static void RegisterInterceptor(Action<PluginFactory, IPlugin> action)
        {
            lock (pluginInterceptors)
            {
                pluginInterceptors.Add(action);
            }
        }

        /// <summary>
        /// Runs the configured interceptors when a specific plugin was loaded
        /// </summary>
        /// <param name="factory">the factory that has loaded a plugin</param>
        /// <param name="plugin">the loaded plugin</param>
        public static void RunInterceptors(PluginFactory factory, IPlugin plugin)
        {
            Action<PluginFactory, IPlugin>[] pi =null;
            lock (pluginInterceptors)
            {
                pi = pluginInterceptors.ToArray();
            }

            foreach(var n in pi)
            {
                try
                {
                    n(factory, plugin);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(ex.Message, LogSeverity.Error);
                }
            }
        }
    }
}
