using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using Antlr4.Runtime.Misc;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Helpers;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Plugins.Model;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.WebCoreToolkit.Configuration;
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
        private IPluginFactory factory;
        private ILogger<WebPluginHelper> logger;

        private bool useExplicitTenants;

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
        public IPluginFactory GetFactory()
        {
            if (factory == null)
            {
                useExplicitTenants = false;
                factory = CreateFactory();
                //SetupFactory(factory, true);
            }

            return factory;
        }

        /// <summary>
        /// Initializes the PluginFactory
        /// </summary>
        /// <param name="explicitPluginScope">the scope that must be explicitly used for loading plugins and constants</param>
        /// <returns>the initialized factory</returns>
        public IPluginFactory GetFactory(string explicitPluginScope)
        {
            if (factory != null && pluginProvider.ExplicitPluginPermissionScope != explicitPluginScope)
            {
                throw new InvalidOperationException("The factory must be reset before re-initialization");
            }

            if (!pluginProvider.ExplicitScopeSupported)
            {
                throw new InvalidOperationException("The Plugin-Source does not support explicit user-scope-selections");
            }

            pluginProvider.ExplicitPluginPermissionScope = explicitPluginScope;
            useExplicitTenants = true;
            factory = CreateFactory();
            //SetupFactory(factory, false);
            return factory;
        }

        public void ResetFactory()
        {
            factory?.Dispose();
            factory = null;
        }

        /// <summary>
        /// Initializes a new i PluginFactory instance
        /// </summary>
        /// <returns>a Factory that can be used to load further plugins</returns>
        private IPluginFactory CreateFactory()
        {
            IDynamicLoader basicLoader = serviceProvider.GetService<IDynamicLoader>();
            IList<IDynamicLoader>  l = new List<IDynamicLoader>();
            if (basicLoader != null)
            {
                l.Add(basicLoader);
            }

            l.Add(new WebPluginLoadHelper(pluginProvider, useExplicitTenants, serviceProvider, PluginInitializationPhase.ScopeStatic));
            var retVal = new PluginFactory(PluginInitializationPhase.ScopeStatic, false, serviceProvider, l.ToArray());
            LogEnvironment.OpenRegistrationTicket(retVal);
            retVal.AllowFactoryParameter = true;
            //retVal.RegisterObject(Global.ServiceProviderName, serviceProvider);
            retVal.RegisterObject(Global.PlugInSelectorName, pluginProvider);
            if (factoryOptions != null)
            {
                factoryOptions.ApplyOptions(factory);
            }
            
            void Initializer(object sender, PluginInitializedEventArgs args)
            {
                PluginLoadInterceptHelper.RunInterceptors(retVal, args.Plugin);
            }

            void Finalizer(object sender, EventArgs e)
            {
                LogEnvironment.DisposeRegistrationTicket(sender);
                var pi = (IPluginFactory) sender ;
                var dp = (IServiceProvider) pi[Global.ServiceProviderName];
                if (dp != null)
                {
                    factory = null;
                }
                pi.Disposed -= Finalizer;
                pi.PluginInitialized -= Initializer;
            }

            retVal.PluginInitialized += Initializer;
            retVal.Disposed += Finalizer;
            retVal.Start();
            retVal.InitializeDeferrables();
            return retVal;
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
