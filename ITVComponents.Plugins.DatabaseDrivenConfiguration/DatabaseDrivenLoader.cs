﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ITVComponents.AssemblyResolving;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Parallel;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Plugins.PluginServices;

namespace ITVComponents.Plugins.DatabaseDrivenConfiguration
{
    public class DatabaseDrivenLoader:AssemblyPluginAnalyzer, IDynamicLoader
    {
        /// <summary>
        /// the database link that contains the db-link for the dynamic objects
        /// </summary>
        private IConnectionBuffer database;

        /// <summary>
        /// the factory that is used to load further plugins
        /// </summary>
        private PluginFactory factory;

        /// <summary>
        /// timer object that is used to refresh the loaded plugins
        /// </summary>
        private Timer refresher;

        /// <summary>
        /// the refresh cycle for checking for new configured plugins
        /// </summary>
        private int refreshCycle;

        /// <summary>
        /// the name of the Plugin-Table that is used by this loader
        /// </summary>
        private string tableName;

        /// <summary>
        /// Initializes a new instance of the DatabaseDrivenLoader class
        /// </summary>
        /// <param name="factory">the factory that is used to initialize plugins</param>
        /// <param name="database">the database that is used to access the configured plugins</param>
        /// <param name="configurationName">the name of the used Loader-Configuration</param>
        public DatabaseDrivenLoader(PluginFactory factory, IConnectionBuffer database, string configurationName)
            : this(factory, database, DatabaseLoaderConfig.Helper.LoaderConfigurations[configurationName].PluginTableName, DatabaseLoaderConfig.Helper.LoaderConfigurations[configurationName].RefreshCycle)
        {
        }


        /// <summary>
        /// Initializes a new instance of the DatabaseDrivenLoader class
        /// </summary>
        /// <param name="factory">the factory that is used to initialize plugins</param>
        /// <param name="database">the database that is used to access the configured plugins</param>
        /// <param name="refreshCycle">a timeout whithin the objects must be refreshed</param>
        public DatabaseDrivenLoader(PluginFactory factory, IConnectionBuffer database, int refreshCycle)
            : this(factory, database, "Plugins", refreshCycle)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DatabaseDrivenLoader class
        /// </summary>
        /// <param name="factory">the factory that is used to initialize plugins</param>
        /// <param name="database">the database that is used to access the configured plugins</param>
        /// <param name="refreshCycle">a timeout whithin the objects must be refreshed</param>
        /// <param name="tableName">the name of the used plugin-Table</param>
        public DatabaseDrivenLoader(PluginFactory factory, IConnectionBuffer database, string tableName, int refreshCycle)
            : base(factory)
        {
            refresher = new Timer(CheckPlugins, null, Timeout.Infinite, Timeout.Infinite);
            this.factory = factory;
            this.database = database;
            this.refreshCycle = refreshCycle;
            this.tableName = tableName;
        }

        /// <summary>
        /// Loads dynamic assemblies that are required for a specific application
        /// </summary>
        public IEnumerable<string> LoadDynamicAssemblies()
        {
            try
            {
                return LoadPlugins();
            }
            finally
            {
                if (refreshCycle != 0)
                {
                    refresher.Change(refreshCycle, refreshCycle);
                }
            }
        }

        /// <summary>
        /// Checks for plugins that are currently not loaded
        /// </summary>
        /// <param name="state">ignored</param>
        private void CheckPlugins(object state)
        {
            refresher.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                var tmp = LoadPlugins().ToArray();
                LogEnvironment.LogDebugEvent($"{tmp.Length} new PlugIns loaded..", LogSeverity.Report);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
            }
            finally
            {
                if (refreshCycle != 0)
                {
                    refresher.Change(refreshCycle, refreshCycle);
                }
            }
        }

        private IEnumerable<string> LoadPlugins()
        {
            using (database.AcquireConnection(false, out var db))
            {
                DynamicResult[] plugins =
                    db.GetNativeResults($"Select * from {tableName} where isnull(disabled,0)=0 order by LoadOrder",
                        null);
                foreach (DynamicResult plugin in plugins)
                {
                    if (factory[plugin["UniqueName"]] == null)
                    {
                        bool ok = false;
                        try
                        {
                            factory.LoadPlugin<IPlugin>(plugin["UniqueName"], plugin["Constructor"]);
                            ok = true;
                        }
                        catch (Exception ex)
                        {
                            LogEnvironment.LogDebugEvent(ex.OutlineException(), LogSeverity.Error);
                            db.ExecuteCommand(
                                $"Update {tableName} set disabled = 1, disabledreason = @reason where pluginid = @pluginId",
                                db.GetParameter("pluginid", plugin["pluginId"]),
                                db.GetParameter("reason", ex.Message));
                        }

                        if (ok)
                        {
                            yield return plugin["UniqueName"];
                        }
                    }
                }
            }
        }
    }
}
