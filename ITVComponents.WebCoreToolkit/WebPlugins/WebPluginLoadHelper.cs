using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.WebPlugins
{
    internal class WebPluginLoadHelper:IDynamicLoader
    {
        private IWebPluginsSelector pluginProvider;
        private bool useExplicitTenants;
        private IServiceProvider serviceProvider;
        private readonly PluginInitializationPhase targetPhase;

        public WebPluginLoadHelper(IWebPluginsSelector pluginProvider, bool useExplicitTenants, IServiceProvider serviceProvider, PluginInitializationPhase targetPhase)
        {
            this.pluginProvider = pluginProvider;
            this.useExplicitTenants = useExplicitTenants;
            this.serviceProvider = serviceProvider;
            this.targetPhase = targetPhase;
        }

        public IEnumerable<PluginInfoModel> GetStartupPlugins(PluginInitializationPhase startupPhase)
        {
            if (targetPhase == PluginInitializationPhase.ScopeStatic)
            {
                foreach (WebPlugin pi in pluginProvider.GetAutoLoadPlugins())
                {
                    if (useExplicitTenants || serviceProvider.VerifyUserPermissions(new[] { pi.UniqueName }, true))
                    {
                        GetPluginInfo(startupPhase, pi.UniqueName, out var yret);
                        yield return yret;
                    }
                }
            }
            else if (targetPhase == PluginInitializationPhase.Startup)
            {
                foreach (WebPlugin pi in pluginProvider.GetStartupPlugins())
                {
                    GetPluginInfo(startupPhase, pi.UniqueName, out var yret);
                    yield return new PluginInfoModel
                    {
                        Buffer = true,
                        ConstructorString = pi.StartupRegistrationConstructor,
                        UniqueName = pi.UniqueName
                    };
                }
            }
            else
            {
                throw new InvalidOperationException("Invalid TargetPhase selected!");
            }
        }

        public IEnumerable<PluginInfoModel> GetPreInitSequence(string uniqueName, PluginInitializationPhase eligibleInitializationPhase)
        {
            if (targetPhase == PluginInitializationPhase.ScopeStatic)
            {
                GetSettingsProviders(out var globalProvider, out var tenantProvider, out var explicitUserScope);
                var preInitializationSequence =
                    tenantProvider?.GetJsonSetting($"PreInitSequenceFor{uniqueName}", explicitUserScope)
                    ?? globalProvider?.GetJsonSetting($"PreInitSequenceFor{uniqueName}");
                var preInitSequence = DeserializeInitArray(preInitializationSequence);
                foreach (var preInit in preInitSequence)
                {
                    GetPluginInfo(PluginInitializationPhase.Scope, preInit, out var def);
                    yield return def;
                }
            }
        }

        public IEnumerable<PluginInfoModel> GetPostInitSequence(string uniqueName, PluginInitializationPhase eligibleInitializationPhase)
        {
            if (targetPhase == PluginInitializationPhase.ScopeStatic)
            {
                GetSettingsProviders(out var globalProvider, out var tenantProvider, out var explicitUserScope);
                var postInitializationSequence =
                    tenantProvider?.GetJsonSetting($"PostInitSequenceFor{uniqueName}", explicitUserScope)
                    ?? globalProvider?.GetJsonSetting($"PostInitSequenceFor{uniqueName}");

                var postInitSequence = DeserializeInitArray(postInitializationSequence);
                foreach (var postInit in postInitSequence)
                {
                    GetPluginInfo(PluginInitializationPhase.Scope, postInit, out var def);
                    yield return def;
                }
            }
        }

        public bool HasParamsFor(string uniqueName)
        {
            return pluginProvider.GetGenericParameters(uniqueName).Any();
        }

        public void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments, Dictionary<string, object> customVariables,
            IStringFormatProvider formatter, out bool knownTypeUsed)
        {
            //IPluginFactory pi = (IPluginFactory)sender;
            IWebPluginsSelector availablePlugins = pluginProvider;
            var impl = availablePlugins.GetGenericParameters(uniqueName);
            bool kno = false;
            knownTypeUsed = false;
            if (impl != null)
            {
                var dic = new Dictionary<string, object>();
                var knownTypes = customVariables ?? new Dictionary<string, object>();
                knownTypes.ForEach(n => dic.Add(n.Key, new SmartProperty
                {
                    GetterMethod = t =>
                    {
                        kno = true;
                        return n.Value;
                    }
                }));
                knownTypeUsed = kno;
                var assignments = (from t in genericTypeArguments
                                   join a in impl on t.GenericTypeName equals a.GenericTypeName
                                   select new { Arg = t, Type = a.TypeExpression });
                foreach (var item in assignments)
                {
                    var args = new ImplementGenericTypeEventArgs
                    {
                        GenericTypes = genericTypeArguments,
                        Handled = true,
                        KnownArguments = knownTypes,
                        KnownArgumentsUsed = knownTypeUsed,
                        PluginUniqueName = uniqueName
                    };
                    item.Arg.TypeResult = (Type)ExpressionParser.Parse(item.Type.ApplyFormat(args), dic);
                }

                //args.Handled = true;
            }
        }

        public bool GetPluginInfo(PluginInitializationPhase currentPhase, string uniqueName, out PluginInfoModel definition)
        {
            WebPlugin plugin =
                pluginProvider.GetPlugin(uniqueName);
            definition = null;
            var retVal = (plugin != null) && (useExplicitTenants || serviceProvider.VerifyUserPermissions(new[] { uniqueName }, true));
            if (retVal)
            {
                definition = new PluginInfoModel()
                {
                    UniqueName = plugin.UniqueName,
                    Buffer = true,
                    ConstructorString = plugin.Constructor
                };
            }

            return retVal;
        }

        private void GetSettingsProviders(out IGlobalSettingsProvider globalProvider,
            out IScopedSettingsProvider scopedProvider, out string explicitUserScope)
        {
            explicitUserScope = null;
            if (useExplicitTenants)
            {
                explicitUserScope = pluginProvider.ExplicitPluginPermissionScope;
            }

            globalProvider = serviceProvider.GetService<IGlobalSettingsProvider>();
            scopedProvider = serviceProvider.GetService<IScopedSettingsProvider>();
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
    }
}
