using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Threading;
using System.Xml;
using Antlr4.Runtime.Misc;
using ITVComponents.AssemblyResolving;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DataAccess.Parallel;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.DatabaseDrivenConfiguration.Helpers;
using ITVComponents.Plugins.DatabaseDrivenConfiguration.Models;
using ITVComponents.Plugins.Helpers;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Plugins.Model;
using ITVComponents.Plugins.PluginServices;
using ITVComponents.Scripting.CScript.Core;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ITVComponents.Plugins.DatabaseDrivenConfiguration
{
    public class DatabaseDrivenLoader : AssemblyPluginAnalyzer, IDynamicLoader
    {
        /// <summary>
        /// the database link that contains the db-link for the dynamic objects
        /// </summary>
        private IDbWrapper database;

        /// <summary>
        /// the name of the Plugin-Table that is used by this loader
        /// </summary>
        private string tableName;

        private readonly string genericParamTableName;
        private readonly string tenantName;

        private List<PluginConfigItem> pluginData;

        /// <summary>
        /// Initializes a new instance of the DatabaseDrivenLoader class
        /// </summary>
        /// <param name="factory">the factory that is used to initialize plugins</param>
        /// <param name="database">the database that is used to access the configured plugins</param>
        /// <param name="configurationName">the name of the used Loader-Configuration</param>
        public DatabaseDrivenLoader(IPluginFactory factory, IDbWrapper database, string configurationName)
            : this(factory, database,
                DatabaseLoaderConfig.Helper.LoaderConfigurations[configurationName].PluginTableName,
                DatabaseLoaderConfig.Helper.LoaderConfigurations[configurationName].ParamTableName,
                DatabaseLoaderConfig.Helper.LoaderConfigurations[configurationName].TenantName)
        {
        }


        /// <summary>
        /// Initializes a new instance of the DatabaseDrivenLoader class
        /// </summary>
        /// <param name="factory">the factory that is used to initialize plugins</param>
        /// <param name="database">the database that is used to access the configured plugins</param>
        /// <param name="refreshCycle">a timeout within the objects must be refreshed</param>
        public DatabaseDrivenLoader(IPluginFactory factory, IDbWrapper database)
            : this(factory, database, "Plugins", null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DatabaseDrivenLoader class
        /// </summary>
        /// <param name="factory">the factory that is used to initialize plugins</param>
        /// <param name="database">the database that is used to access the configured plugins</param>
        /// <param name="tenantName">the tenant-name that is used to load plugins in a multi-tenant environment</param>
        /// <param name="tableName">the name of the used plugin-Table</param>
        public DatabaseDrivenLoader(IPluginFactory factory, IDbWrapper database, string tableName,
            string genericParamTableName, string tenantName)
            : base(factory)
        {
            pluginData = new();
            this.database = database;
            this.tableName = tableName;
            this.genericParamTableName = genericParamTableName;
            this.tenantName = tenantName;
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
                database.GetResults<DatabasePlugin>($@"Select * from {tableName} where isnull(disabled,0)=0 and 
(tenantId=@tenantId or (tenantId is null and @tenantId is null))
order by LoadOrder",
                    database.GetParameter("tenantId", tenantName)).ToArray();
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
                        InitializeAfter = plugin.PostInitializationList.ProcessSequence(),
                        InitializeBefore = plugin.PreInitializationList.ProcessSequence()
                    },
                    GenericArguments = new List<GenericTypeDefinition>()
                };

                pluginData.Add(cfgPlug);
                cfgPlug.GenericArguments.AddRange(ReadGenericArguments(cfgPlug.PluginDefinition.Name));
            }
        }

        private IEnumerable<GenericTypeDefinition> ReadGenericArguments(string name)
        {
            if (!string.IsNullOrEmpty(genericParamTableName))
            {

                var ct =
                    database.ExecuteCommandScalar<int>(
                        $@"Select count(a.*) from {tableName} p inner join {genericParamTableName} a on a.PlugInId = p.PlugInId where p.UniqueName = @uniqueName and isnull(disabled,0)=0 and
(p.tenantId=@tenantId or (p.tenantId is null and @tenantId is null)) and
(a.tenantId=@tenantId or (a.tenantId is null and @tenantId is null))",
                        database.GetParameter("uniqueName", name),
                        database.GetParameter("tenantId", tenantName));
                if (ct != 0)
                {
                    var data =
                        database.GetResults<DatabasePluginTypeParam>(
                            $@"Select a.* from {tableName} p inner join {genericParamTableName} a on a.PlugInId = p.PlugInId where p.UniqueName = @uniqueName and isnull(disabled,0)=0 and
(p.tenantId=@tenantId or (p.tenantId is null and @tenantId is null)) and
(a.tenantId=@tenantId or (a.tenantId is null and @tenantId is null))"
                            , database.GetParameter("uniqueName", name),
                            database.GetParameter("tenantId", tenantName)).ToArray();
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
    }
}
