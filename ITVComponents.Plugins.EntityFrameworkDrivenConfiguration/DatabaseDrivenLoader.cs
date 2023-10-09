using ITVComponents.DataAccess;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Plugins.PluginServices;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.EFRepo;
using Microsoft.EntityFrameworkCore;
using ITVComponents.DataAccess.Parallel;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.DatabaseDrivenConfiguration.Helpers;
using ITVComponents.Plugins.Helpers;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Plugins.Model;
using ITVComponents.Plugins.DatabaseDrivenConfiguration.Models;
using System.Xml.Linq;

namespace ITVComponents.Plugins.EntityFrameworkDrivenConfiguration
{
    public class DatabaseDrivenLoader<TContext> : AssemblyPluginAnalyzer, IDynamicLoader where TContext:DbContext, IPluginRepo  
    {
        private readonly IPluginFactory factory;

        /// <summary>
        /// timer object that is used to refresh the loaded plugins
        /// </summary>
        private Timer refresher;

        private readonly bool useGenericParams;

        private DbContext database;

        private List<PluginConfigItem> pluginData;

        private string tenantName;

        public DatabaseDrivenLoader(IPluginFactory factory, DbContext database) : this(factory,
            database, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DatabaseDrivenLoader class
        /// </summary>
        /// <param name="factory">the factory that is used to initialize plugins</param>
        /// <param name="database">the database that is used to access the configured plugins</param>
        /// <param name="refreshCycle">a timeout whithin the objects must be refreshed</param>
        public DatabaseDrivenLoader(IPluginFactory factory, DbContext database, bool useGenericParams):base(factory)
        {
            this.factory = factory;
            this.database = database;
            this.useGenericParams = useGenericParams;
            LoadPlugins();
        }

        public IEnumerable<PluginInfoModel> GetStartupPlugins(PluginInitializationPhase startupPhase)
        {
            return (from t in pluginData
                    where !t.PluginDefinition.Disabled && (t.PluginDefinition.InitializationPhase & startupPhase) != 0
                    select
                        new PluginInfoModel
                        {
                            Buffer = true,
                            ConstructorString = t.PluginDefinition.ConstructionString,
                            UniqueName = t.PluginDefinition.Name
                        });
        }

        public IEnumerable<PluginInfoModel> GetPreInitSequence(string uniqueName, PluginInitializationPhase eligibleInitializationPhase)
        {
            var ok = FetchBufferedPlugin(uniqueName, eligibleInitializationPhase, out var item);
            if (ok && item.PluginDefinition.InitializeBefore.Length != 0)
            {
                return from t in item.PluginDefinition.InitializeBefore
                       let pio = new
                       {
                           Ok = GetPluginInfo(eligibleInitializationPhase, t, out var pii),
                           item = pii
                       }
                       where pio.Ok
                       select pio.item;
            }

            return Array.Empty<PluginInfoModel>();
        }

        public IEnumerable<PluginInfoModel> GetPostInitSequence(string uniqueName, PluginInitializationPhase eligibleInitializationPhase)
        {
            var ok = FetchBufferedPlugin(uniqueName, eligibleInitializationPhase, out var item);
            if (ok && item.PluginDefinition.InitializeAfter.Length != 0)
            {
                return from t in item.PluginDefinition.InitializeAfter
                       let pio = new
                       {
                           Ok = GetPluginInfo(eligibleInitializationPhase, t, out var pii),
                           item = pii
                       }
                       where pio.Ok
                       select pio.item;
            }

            return Array.Empty<PluginInfoModel>();
        }

        /// <summary>
        /// Gets a value indicating whether this loader is able to provide further generic information for a specific plugin
        /// </summary>
        /// <param name="uniqueName">the plugin for which to get the generic information</param>
        /// <returns>a value indicating whether the requested plugin is known by this loader and contains generic arguments</returns>
        public bool HasParamsFor(string uniqueName)
        {
            var plug = pluginData.FirstOrDefault(n => !n.PluginDefinition.Disabled && n.PluginDefinition.Name == uniqueName);
            return plug != null && plug.GenericArguments.Count != 0;
        }

        /// <summary>
        /// Fills the generic Type-Arguments with the appropriate values
        /// </summary>
        /// <param name="uniqueName">the unique-name for which to get the generic arguments</param>
        /// <param name="genericTypeArguments">get generic arguments defined in the plugin-type</param>
        public void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments,
            Dictionary<string, object> customVariables, IStringFormatProvider formatter, out bool knownTypeUsed)
        {
            knownTypeUsed = false;
            var plug = pluginData.FirstOrDefault(n => !n.PluginDefinition.Disabled && n.PluginDefinition.Name == uniqueName);
            if (plug != null && plug.GenericArguments.Count != 0)
            {
                var data = plug.GenericArguments;
                var joined = from t in genericTypeArguments
                             join d in data on t.GenericTypeName equals d.TypeParameterName
                             select new { Target = t, Type = d.TypeExpression };
                Dictionary<string, object> dic = new Dictionary<string, object>();
                customVariables ??= new Dictionary<string, object>();
                bool kt = knownTypeUsed;
                customVariables.ForEach(n => dic.Add(n.Key, new SmartProperty
                {
                    GetterMethod = t =>
                    {
                        kt = true;
                        return n.Value;
                    }
                }));
                knownTypeUsed = kt;
                foreach (var j in joined)
                {
                    j.Target.TypeResult = (Type)ExpressionParser.Parse(j.Type.ApplyFormat(formatter), dic);
                }
            }
        }

        public bool GetPluginInfo(PluginInitializationPhase currentPhase, string uniqueName, out PluginInfoModel definition)
        {
            var retVal = FetchBufferedPlugin(uniqueName, currentPhase, out var m);
            definition = m != null
                ? new PluginInfoModel
                {
                    Buffer = (m.PluginDefinition.InitializationPhase & PluginInitializationPhase.NoTracking) == 0,
                    ConstructorString = m.PluginDefinition.ConstructionString,
                    UniqueName = m.PluginDefinition.Name
                }
                : null;
            return retVal;
        }

        public override void Dispose()
        {
            base.Dispose();
            database.Dispose();
        }

        private bool FetchBufferedPlugin(string uniqueName, PluginInitializationPhase currentPhase, out PluginConfigItem item)
        {
            item = pluginData.FirstOrDefault(n =>
                !n.PluginDefinition.Disabled && n.PluginDefinition.Name == uniqueName &&
                ((n.PluginDefinition.InitializationPhase & currentPhase) != 0 || (n.PluginDefinition.InitializationPhase & PluginInitializationPhase.NoTracking) != 0));
            bool retVal = item != null;
            return retVal;
        }

        private void LoadPlugins()
        {
            DatabasePlugin[] plugins =
                database.Set<DatabasePlugin>().Where(n => !(n.Disabled??false)).OrderBy(n => n.LoadOrder).ToArray();
            foreach (var plugin in plugins)
            {
                var cfgPlug = new PluginConfigItem()
                {
                    PluginDefinition = new PluginConfigurationItem()
                    {
                        ConstructionString = plugin.Constructor,
                        Disabled = plugin.Disabled ?? false,
                        InitializationPhase = plugin.PluginInitializationPhase ?? PluginInitializationPhase.Startup,
                        Name = plugin.UniqueName,
                        InitializeAfter = ProcessSequence(plugin.PostInitializationList),
                        InitializeBefore = ProcessSequence(plugin.PreInitializationList)
                    },
                    GenericArguments = new List<GenericTypeDefinition>()
                };

                pluginData.Add(cfgPlug);
                cfgPlug.GenericArguments.AddRange(ReadGenericArguments(cfgPlug.PluginDefinition.Name));
            }
        }

        private IEnumerable<GenericTypeDefinition> ReadGenericArguments(string name)
        {
            if (useGenericParams)
            {

                var ct =
                    database.Set<DatabasePluginTypeParam>().Count(n => n.Plugin.UniqueName == name);
                if (ct != 0)
                {
                    var data =
                        database.Set<DatabasePluginTypeParam>().Where(n => n.Plugin.UniqueName == name).ToArray();
                    foreach (var item in data)
                    {
                        yield return new GenericTypeDefinition
                        {
                            TypeExpression = item.TypeExpression,
                            TypeParameterName = item.GenericTypeName
                        };
                    }
                }
            }
        }

        private string[] ProcessSequence(string list)
        {
            string[] retVal = Array.Empty<string>();
            if (!string.IsNullOrEmpty(list))
            {
                if (list.Contains("["))
                {
                    retVal = JsonHelper.FromJsonString<string[]>(list);
                }
                else
                {
                    retVal = list.Replace("\r", "\n").Replace("\n\n", "\n").Split("\n").Where(n => !string.IsNullOrEmpty(n)).ToArray();
                }
            }

            return retVal;
        }
    }
}
