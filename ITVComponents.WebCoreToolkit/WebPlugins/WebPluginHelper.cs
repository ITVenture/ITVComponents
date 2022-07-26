using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Helpers;
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
                factory = CreateFactory(true, false);
                SetupFactory(factory, true);
            }

            return factory;
        }

        /// <summary>
        /// Initializes the PluginFactory
        /// </summary>
        /// <param name="explicitPluginScope">the scope that must be explicitly used for loading plugins and constants</param>
        /// <returns>the initialized factory</returns>
        public PluginFactory GetFactory(string explicitPluginScope)
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
            factory = CreateFactory(false, true);
            SetupFactory(factory, false);
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
        private PluginFactory CreateFactory(bool checkSecurity, bool useExplicitTenants)
        {
            var retVal = new PluginFactory();
            LogEnvironment.OpenRegistrationTicket(retVal);
            retVal.AllowFactoryParameter = true;
            retVal.RegisterObject(Global.ServiceProviderName, serviceProvider);
            retVal.RegisterObject(Global.PlugInSelectorName, pluginProvider);
            string explicitUserScope = null;
            if (useExplicitTenants)
            {
                explicitUserScope = pluginProvider.ExplicitPluginPermissionScope;
            }

            UnknownConstructorParameterEventHandler handler = (sender, args) =>
            {
                PluginFactory pi = (PluginFactory) sender;
                IWebPluginsSelector availablePlugins = pluginProvider;
                var globalProvider = serviceProvider.GetService<IGlobalSettingsProvider>();
                var tenantProvider = serviceProvider.GetService<IScopedSettingsProvider>();
                var preInitializationSequence = tenantProvider?.GetJsonSetting($"PreInitSequenceFor{args.RequestedName}", explicitUserScope)
                                                ?? globalProvider?.GetJsonSetting($"PreInitSequenceFor{args.RequestedName}");
                var postInitializationSequence = tenantProvider?.GetJsonSetting($"PostInitSequenceFor{args.RequestedName}", explicitUserScope)
                                                ?? globalProvider?.GetJsonSetting($"PostInitSequenceFor{args.RequestedName}");
                var preInitSequence = DeserializeInitArray(preInitializationSequence);
                var postInitSequence = DeserializeInitArray(postInitializationSequence);
                WebPlugin plugin =
                    availablePlugins.GetPlugin(args.RequestedName);
                if (plugin != null)
                {
                    if (!checkSecurity || serviceProvider.VerifyUserPermissions(new []{args.RequestedName},true))
                    {
                        if (preInitSequence.Length != 0)
                        {
                            foreach (var s in preInitSequence)
                            {
                                var tmp = pi[s, true];
                            }
                        }

                        if (!string.IsNullOrEmpty(plugin.Constructor))
                        {
                            args.Value = pi.LoadPlugin<IPlugin>(plugin.UniqueName, plugin.Constructor);
                            args.Handled = true;
                        }

                        if (postInitSequence.Length != 0)
                        {
                            foreach (var s in postInitSequence)
                            {
                                var tmp = pi[s, true];
                            }
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
                pi.ImplementGenericType -= Implementer;
            }

            void Implementer(object sender, ImplementGenericTypeEventArgs args)
            {
                PluginFactory pi = (PluginFactory)sender;
                IWebPluginsSelector availablePlugins = pluginProvider;
                var impl = availablePlugins.GetGenericParameters(args.PluginUniqueName);
                if (impl != null)
                {
                    var dic = new Dictionary<string, object>();
                    var assignments = (from t in args.GenericTypes
                        join a in impl on t.GenericTypeName equals a.GenericTypeName
                        select new { Arg = t, Type = a.TypeExpression });
                    foreach (var item in assignments)
                    {
                        item.Arg.TypeResult = (Type)ExpressionParser.Parse(item.Type.ApplyFormat(args), dic);
                    }

                    args.Handled = true;
                }
            }

            retVal.UnknownConstructorParameter += handler;
            retVal.PluginInitialized += Initializer;
            retVal.Disposed += Finalizer;
            retVal.ImplementGenericType += Implementer;
            return retVal;
        }

        private string[] DeserializeInitArray(string jsonSerializedArray)
        {
            string[] retVal = Array.Empty<string>();
            if (!string.IsNullOrEmpty(jsonSerializedArray))
            {
                try
                {
                    retVal = JsonHelper.FromJsonString<string[]>(jsonSerializedArray);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Failed to deserialize Init-Sequence as string[] for {jsonSerializedArray}",
                        LogSeverity.Error);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Sets up the factory and loads autoload-configured plugins
        /// </summary>
        private void SetupFactory(PluginFactory factory, bool testPermissions)
        {
            foreach (WebPlugin pi in pluginProvider.GetAutoLoadPlugins())
            {
                try
                {
                    if (!testPermissions || serviceProvider.VerifyUserPermissions(new []{pi.UniqueName}, true))
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
