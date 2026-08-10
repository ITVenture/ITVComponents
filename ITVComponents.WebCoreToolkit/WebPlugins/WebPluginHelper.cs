using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DIServices;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Helpers;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using ITVComponents.WebCoreToolkit.WebPlugins.Initialization;
using ITVComponents.WebCoreToolkit.WebPlugins.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly ISecurityAccessProvider securityAccessProvider;
        private readonly IPermissionScope scopeProvider;
        private readonly FactoryOptions factoryOptions;
        private IWebPluginsSelector pluginProvider;
       private IServiceProvider serviceProvider;
        //private PluginFactory pluginFactory;
        //private ConcurrentDictionary<GuidEnumeration, PluginFactory> factories;
        private PluginFactory factory;
        private ILogger<WebPluginHelper> logger;
        private List<IPlugin> transientPlugins = new();
        private ThreadLocal<bool> pluginIsLoading = new ThreadLocal<bool>(() => false);
        /// <summary>
        /// Initializes a new instance of the WebPluginHelper class
        /// </summary>
        /// <param name="pluginProvider">the plugin-repository that holds all web-plugins that need to be initialized</param>
        /// <param name="serviceProvider">the dependencyInjection infrasturcture that can be used to log errors</param>
        /// <param name="autoPluginsInit"></param>
        /// <param name="logger">a logger instance that is used to log events of this PluginHelper instance</param>
        public WebPluginHelper(IWebPluginsSelector pluginProvider, IServiceProvider serviceProvider, IOptions<PluginsInitOptions> autoPluginsInit, IPermissionScope scopeProvider, ILogger<WebPluginHelper> logger)
        {
            this.pluginProvider = pluginProvider;
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            var init = autoPluginsInit.Value;
            this.scopeProvider = scopeProvider;
        }

        /// <summary>
        /// Initializes a new instance of the WebPluginHelper class
        /// </summary>
        /// <param name="pluginProvider">the plugin-repository that holds all web-plugins that need to be initialized</param>
        /// <param name="serviceProvider">the dependencyInjection infrasturcture that can be used to log errors</param>
        /// <param name="autoPluginsInit"></param>
        /// <param name="factoryOptions">the factory-options used for DI injection into plugins</param>
        /// <param name="logger">a logger instance that is used to log events of this PluginHelper instance</param>
        public WebPluginHelper(IWebPluginsSelector pluginProvider, IServiceProvider serviceProvider, IOptions<PluginsInitOptions> autoPluginsInit, IOptions<FactoryOptions> factoryOptions, IPermissionScope scopeProvider, ISecurityAccessProvider securityAccessProvider, ILogger<WebPluginHelper> logger)
        :this(pluginProvider, serviceProvider, autoPluginsInit, scopeProvider, logger)
        {
            this.securityAccessProvider = securityAccessProvider;
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
                factory = CreateFactory(true, false, out var tenantObjects);
                SetupFactory(factory, true, tenantObjects);
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
            factory = CreateFactory(false, true, out var tenantObjects);
            SetupFactory(factory, false, tenantObjects);
            return factory;
        }

        public void ResetFactory()
        {
            factory?.Dispose();
            factory = null;
            IPlugin[] toDispose;
            lock (transientPlugins)
            {
                toDispose = transientPlugins.ToArray();
                transientPlugins.Clear();
            }

            toDispose.ForEach(n => n.Dispose());
        }

        /// <summary>
        /// Initializes a new i PluginFactory instance
        /// </summary>
        /// <returns>a Factory that can be used to load further plugins</returns>
        private PluginFactory CreateFactory(bool checkSecurity, bool useExplicitTenants,
            out IObjectProvider objectProvider)
        {
            var retVal = new PluginFactory();
            LogEnvironment.OpenRegistrationTicket(retVal);
            retVal.AllowFactoryParameter = true;
            retVal.RegisterObject(Global.ServiceProviderName, serviceProvider);
            retVal.RegisterObject(Global.PlugInSelectorName, pluginProvider);
            retVal.RegisterObject(Global.SecurityAccessProvider, securityAccessProvider);
            string explicitUserScope = null;
            if (useExplicitTenants)
            {
                explicitUserScope = pluginProvider.ExplicitPluginPermissionScope;
            }

            var activeUserScope = explicitUserScope ?? scopeProvider.PermissionPrefix;
            objectProvider = serviceProvider.GetObjectProvider(activeUserScope);

            retVal.RegisterObject(Global.TenantObjectCacheName, objectProvider);
            var tenantObjects = objectProvider;

            UnknownConstructorParameterEventHandler handler = (sender, args) =>
            {
                PluginFactory pi = (PluginFactory)sender;
                bool cleanup = false;
                // The transient loading-scope this handler opens for the current resolution chain. Kept so we can
                // close exactly THIS scope at the end - NOT pi.ScopeClose(), which closes the factory's CurrentScope
                // and would tear down an OUTER scope that happens to be active while a constructor parameter is
                // being resolved.
                IPluginFactory loadingScope = null;
                // If an EXPLICIT scope is already active (NewScope with transientLoadingScope = false, e.g. the IPC
                // FactoryWrapper.OpenScope), do NOT open a transient loading-scope of our own and do NOT collect the
                // transients in transientPlugins: the transiently created dependencies then belong to the active
                // scope and are disposed when it is released - otherwise they would survive until the next
                // ResetFactory, a leak beyond the lifetime of that scope. Only the outermost, scope-free resolution
                // on this thread opens the transient loading-scope.
                if (!pluginIsLoading.Value && !pi.IsInLoadScope)
                {
                    pluginIsLoading.Value = true;
                    cleanup = true;
                    loadingScope = pi.NewScope(null, null, true);
                }

                // The load runs INSIDE the transient loading-scope when this call opened it (outermost, scope-free
                // resolution) - only that way WithScope sets the CurrentScope and plugins marked as transient really
                // end up in the loading-scope. For nested calls (no own loading-scope) the load goes through the
                // main factory; the CurrentScope is already set by the outer call.
                IPluginFactory loadTarget = loadingScope ?? (IPluginFactory)pi;

                try
                {
                    IWebPluginsSelector availablePlugins = pluginProvider;
                    var globalProvider = serviceProvider.GetService<IGlobalSettingsProvider>();
                    var tenantProvider = serviceProvider.GetService<IScopedSettingsProvider>();

                    var preInitializationSequence = tenantObjects.GetBufferedValue(
                        $"PreInitSequenceFor{args.RequestedName}", k =>
                            tenantProvider?.GetJsonSetting(k, explicitUserScope)
                            ?? globalProvider?.GetJsonSetting(k), null);
                    var postInitializationSequence = tenantObjects.GetBufferedValue(
                        $"PostInitSequenceFor{args.RequestedName}", k =>
                            tenantProvider?.GetJsonSetting(k, explicitUserScope)
                            ?? globalProvider?.GetJsonSetting(k), null);
                    var preInitSequence = DeserializeInitArray(preInitializationSequence);
                    var postInitSequence = DeserializeInitArray(postInitializationSequence);
                    WebPlugin plugin =
                        tenantObjects.GetBufferedValue($"TenantPI#{args.RequestedName}",
                            _ => availablePlugins.GetPlugin(args.RequestedName), null);
                    if (plugin != null)
                    {
                        if (!checkSecurity || serviceProvider.VerifyUserPermissions(new[] { args.RequestedName }, true))
                        {
                            if (preInitSequence.Length != 0)
                            {
                                foreach (var s in preInitSequence)
                                {
                                    var tmp = pi[s, true, args.PluginType];
                                }
                            }

                            if (!string.IsNullOrEmpty(plugin.Constructor))
                            {
                                // TransientLoad instead of assigning UseTransientScope: the load below resolves the
                                // constructor-parameters of this plugin first, and every one of them passes through
                                // this same handler and announces ITS OWN transient-flag. A plain assignment would
                                // therefore be overwritten by the last-resolved dependency by the time this plugin
                                // is registered. The token restores our value when the nested call returns.
                                using (pi.TransientLoad(plugin.Transient))
                                {
                                    if (args.PluginType != null)
                                    {
                                        args.Value = loadTarget.LoadPlugin<IPlugin>(plugin.UniqueName, plugin.Constructor,
                                            new Dictionary<string, object> { { "CallingPlugin", args.PluginType } });
                                    }
                                    else
                                    {
                                        args.Value = loadTarget.LoadPlugin<IPlugin>(plugin.UniqueName, plugin.Constructor);
                                    }
                                }

                                args.Handled = true;
                            }

                            if (postInitSequence.Length != 0)
                            {
                                foreach (var s in postInitSequence)
                                {
                                    var tmp = pi[s, true, args.PluginType];
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
                }
                finally
                {
                    if (cleanup)
                    {
                        pluginIsLoading.Value = false;
                        lock (transientPlugins)
                        {
                            // Close exactly the scope this call opened. pi.ScopeClose() would close the factory's
                            // CurrentScope, which at this point is either nothing (no-op, and the transient scope
                            // would leak in scopedPlugins) or somebody else's scope.
                            transientPlugins.AddRange(loadingScope.ScopeClose());
                        }

                    }
                }
            };

            void Initializer(object sender, PluginInitializedEventArgs args)
            {
                PluginLoadInterceptHelper.RunInterceptors(retVal, args.Plugin);
            }

            void Finalizer(object sender, EventArgs e)
            {
                LogEnvironment.DisposeRegistrationTicket(sender);
                var pi = (PluginFactory)sender;
                var dp = (IServiceProvider)pi.GetRegisteredObject(Global.ServiceProviderName);
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
                var impl = tenantObjects.GetBufferedValue($"PIArgs4#{args.PluginUniqueName}",
                    _ => availablePlugins.GetGenericParameters(args.PluginUniqueName).ToArray(), null);
                if (impl != null)
                {
                    var dic = new Dictionary<string, object>();
                    var knownTypes = args.KnownArguments ?? new Dictionary<string, object>();
                    knownTypes.ForEach(n => dic.Add(n.Key, new SmartProperty
                    {
                        GetterMethod = t =>
                        {
                            //args.KnownArgumentsUsed = true;
                            return n.Value;
                        }
                    }));
                    /*var assignments = (from t in args.GenericTypes
                        join a in impl on t.GenericTypeName equals a.GenericTypeName
                        select new { Arg = t, Type = a.TypeExpression });*/
                    List<(string name, Type type)> fixTypes = new List<(string name, Type type)>();
                    Type argumentProvider = null;
                    foreach (var item in impl)
                    {
                        var t = (Type)ExpressionParser.Parse(item.TypeExpression.ApplyFormat(args), dic);
                        if (item.GenericTypeName != "$$genericArgumentProvider")
                        {
                            fixTypes.Add((name: item.GenericTypeName,
                                type: t));
                        }
                        else
                        {
                            argumentProvider = t;
                        }
                    }

                    if (argumentProvider == null)
                    {
                        var rawTypes =
                            typeof(object).GetInterfaceGenericArgumentsOf(fixTypeEntries: fixTypes.ToArray());
                        args.Handled = args.GenericTypes.FinalizeTypeArguments(rawTypes);
                    }
                    else
                    {
                        var rawTypes =
                            argumentProvider.GetInterfaceGenericArgumentsOf(fixTypeEntries: fixTypes.ToArray());
                        args.Handled = args.GenericTypes.FinalizeTypeArguments(rawTypes);
                    }
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
                    retVal = JsonHelper.FromJsonString<string[]>(jsonSerializedArray, SerializationTypingMode.StaticTyping);
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
        private void SetupFactory(PluginFactory factory, bool testPermissions, IObjectProvider tenantObjects)
        {
            foreach (WebPlugin pi in tenantObjects.GetBufferedValue("PISetup#AutoPlugs", _ => pluginProvider.GetAutoLoadPlugins().ToArray(), null))
            {
                try
                {
                    if (!testPermissions || serviceProvider.VerifyUserPermissions([pi.UniqueName], true))
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
Section: Plugins");
                }
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            logger.LogInformation("Disposing Factory...");
            factory?.Dispose();
            IPlugin[] toDispose;
            lock (transientPlugins)
            {
                toDispose = transientPlugins.ToArray();
                transientPlugins.Clear();
            }

            toDispose.ForEach(n => n.Dispose());
            logger.LogInformation("Factory disposed.");
        }
    }
}
