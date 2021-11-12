using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins.Initialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.WebPlugins
{
    /// <summary>
    /// Default implementation of a WebPlugin helper object
    /// </summary>
    public class WebPluginHelper:IWebPluginHelper
    {
        private readonly FactoryOptions factoryOptions;
        private IWebPluginsSelector pluginProvider;
       private IServiceProvider serviceProvider;
        //private PluginFactory pluginFactory;
        //private ConcurrentDictionary<GuidEnumeration, PluginFactory> factories;
        private PluginFactory factory;
        private ILogger<WebPluginHelper> logger;

        /// <summary>
        /// Initializes a new instance of the WebPluginHelper class
        /// </summary>
        /// <param name="pluginProvider">the plugin-repository that holds all web-plugins that need to be initialized</param>
        /// <param name="serviceProvider">the dependencyInjection infrasturcture that can be used to log errors</param>
        /// <param name="autoPluginsInit"></param>
        /// <param name="logger">a logger instance that is used to log events of this PluginHelper instance</param>
        public WebPluginHelper(IWebPluginsSelector pluginProvider, IServiceProvider serviceProvider, IOptions<PluginsInitOptions> autoPluginsInit, ILogger<WebPluginHelper> logger)
        {
            this.pluginProvider = pluginProvider;
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            var init = autoPluginsInit.Value;
        }

        /// <summary>
        /// Initializes a new instance of the WebPluginHelper class
        /// </summary>
        /// <param name="pluginProvider">the plugin-repository that holds all web-plugins that need to be initialized</param>
        /// <param name="serviceProvider">the dependencyInjection infrasturcture that can be used to log errors</param>
        /// <param name="autoPluginsInit"></param>
        /// <param name="factoryOptions">the factory-options used for DI injection into plugins</param>
        /// <param name="logger">a logger instance that is used to log events of this PluginHelper instance</param>
        public WebPluginHelper(IWebPluginsSelector pluginProvider, IServiceProvider serviceProvider, IOptions<PluginsInitOptions> autoPluginsInit, IOptions<FactoryOptions> factoryOptions, ILogger<WebPluginHelper> logger)
        :this(pluginProvider, serviceProvider, autoPluginsInit , logger)
        {
            this.factoryOptions = factoryOptions.Value;
        }

        /// <summary>
        /// Gets the a PluginFactory inside the current OWinContext
        /// </summary>
        /// <returns>a pluginfactory</returns>
        public PluginFactory GetFactory()
        {
            if (factory == null)
            {
                factory = CreateFactory();
                SetupFactory(factory);
            }

            return factory;
        }

        /// <summary>
        /// Initializes a new i PluginFactory instance
        /// </summary>
        /// <returns>a Factory that can be used to load further plugins</returns>
        private PluginFactory CreateFactory()
        {
            var retVal = new PluginFactory();
            LogEnvironment.OpenRegistrationTicket(retVal);
            retVal.AllowFactoryParameter = true;
            retVal.RegisterObject(Global.ServiceProviderName, serviceProvider);
            UnknownConstructorParameterEventHandler handler = (sender, args) =>
            {
                PluginFactory pi = (PluginFactory) sender;
                IWebPluginsSelector availablePlugins = pluginProvider;

                WebPlugin plugin =
                    availablePlugins.GetPlugin(args.RequestedName);
                if (plugin != null)
                {
                    if (serviceProvider.VerifyUserPermissions(new []{args.RequestedName},true))
                    {
                        if (!string.IsNullOrEmpty(plugin.Constructor))
                        {
                            args.Value = pi.LoadPlugin<IPlugin>(plugin.UniqueName, plugin.Constructor);
                            args.Handled = true;
                        }
                    }
                }
                else
                {
                    var tmp = factoryOptions?.GetDependency(args.RequestedName, serviceProvider);
                    args.Handled = tmp != null;
                    args.Value = tmp;
                }
            };
            
            void Initializer(object sender, PluginInitializedEventArgs args)
            {
                PluginLoadInterceptHelper.RunInterceptors(retVal, args.Plugin);
            }

            void Finalizer(object sender, EventArgs e)
            {
                LogEnvironment.DisposeRegistrationTicket(sender);
                var pi = (PluginFactory) sender ;
                var dp = (IServiceProvider) pi.GetRegisteredObject(Global.ServiceProviderName);
                if (dp != null)
                {
                    factory = null;
                }
                pi.Disposed -= Finalizer;
                pi.UnknownConstructorParameter -= handler;
                pi.PluginInitialized -= Initializer;
            }

            retVal.UnknownConstructorParameter += handler;
            retVal.PluginInitialized += Initializer;
            retVal.Disposed += Finalizer;

            return retVal;
        }

        /// <summary>
        /// Sets up the factory and loads autoload-configured plugins
        /// </summary>
        private void SetupFactory(PluginFactory factory)
        {
            foreach (WebPlugin pi in pluginProvider.GetAutoLoadPlugins())
            {
                try
                {
                    if (serviceProvider.VerifyUserPermissions(new []{pi.UniqueName}, true))
                    {
                        factory.LoadPlugin<IPlugin>(pi.UniqueName, pi.Constructor);
                    }
                }
                catch (Exception ex)
                {
                    //pi.AutoLoad = false;
                    //pluginProvider.ConfigurePlugin(pi);
                    logger.LogError($@"Plugin failed to load.
Error:
{ex.OutlineException()}
Section: Plugins", ex, "Plugins");
                }
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            logger.LogInformation("Disposing Factory...");
            factory?.Dispose();
            logger.LogInformation("Factory disposed.");
        }
    }
}
