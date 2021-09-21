using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.WebPlugins.Initialization
{
    /// <summary>
    /// Initializes Auto-Plugins
    /// </summary>
    internal class UsePluginsInit:IConfigureOptions<PluginsInitOptions>
    {
        /// <summary>
        /// The singleton-PluginsInitOptions object
        /// </summary>
        private readonly IPluginsInitOptions globalInit;

        /// <summary>
        /// the DI infrastructure
        /// </summary>
        private readonly IServiceScopeFactory serviceProvider;

        private readonly ILogger<UsePluginsInit> plugLogger;

        /// <summary>
        /// Initializes a new instance of hte UsePluginsInit class
        /// </summary>
        /// <param name="globalInit">the global PluginsInitOptions object</param>
        /// <param name="serviceProvider">the DI infrastructure</param>
        /// <param name="plugLogger">a loger instance that is used to log information from PluginsInit</param>
        public UsePluginsInit(IPluginsInitOptions globalInit, IServiceScopeFactory serviceProvider, ILogger<UsePluginsInit> plugLogger)
        {
            this.globalInit = globalInit;
            this.serviceProvider = serviceProvider;
            this.plugLogger = plugLogger;
        }

        /// <summary>
        /// Invoked to configure a <typeparamref name="TOptions" /> instance.
        /// </summary>
        /// <param name="options">The options instance to configure.</param>
        public void Configure(PluginsInitOptions options)
        {
            options.SetParent(globalInit);
            options.Init(() =>
            {
                try
                {
                    plugLogger.LogDebug("Creating service scope...");
                    using (var scope = serviceProvider.CreateScope())
                    {
                        var pluginProvider = scope.ServiceProvider.GetService<IWebPluginsSelector>();
                        PluginFactory noBufFactory;
                        using (noBufFactory = new PluginFactory(false))
                        {
                            plugLogger.LogDebug("Enumerating Startup-Plugins");
                            foreach (var plug in pluginProvider.GetStartupPlugins())
                            {
                                try
                                {
                                    plugLogger.LogDebug($"Initializing {plug.UniqueName}");
                                    using (var plugInst = noBufFactory.LoadPlugin<IPlugin>(plug.UniqueName,
                                        plug.StartupRegistrationConstructor))
                                    {
                                        if (plugInst is IDependencyInit init)
                                        {
                                            init.Initialize(scope.ServiceProvider);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    plugLogger.LogError(ex, "Error on performing auto-load of plugins");
                                }
                            }

                            plugLogger.LogDebug("Done");
                        }
                    }
                }
                catch (Exception ex)
                {
                    plugLogger.LogError(ex, "Error on preparing Plugins-Autoload");
                }
            });
        }
    }
}
