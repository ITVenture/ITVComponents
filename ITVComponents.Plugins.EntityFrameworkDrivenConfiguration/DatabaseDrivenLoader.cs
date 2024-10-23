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
using ITVComponents.Plugins.Helpers;
using ITVComponents.Scripting.CScript.Core;

namespace ITVComponents.Plugins.EntityFrameworkDrivenConfiguration
{
    public class DatabaseDrivenLoader<TContext> : AssemblyPluginAnalyzer, IDynamicLoader where TContext:DbContext, IPluginRepo  
    {
        private readonly PluginFactory factory;

        /// <summary>
        /// timer object that is used to refresh the loaded plugins
        /// </summary>
        private Timer refresher;

        /// <summary>
        /// the refresh cycle for checking for new configured plugins
        /// </summary>
        private int refreshCycle;

        private readonly bool useGenericParams;

        private IContextBuffer database;

        public DatabaseDrivenLoader(PluginFactory factory, IContextBuffer database, int refreshCycle) : this(factory,
            database, refreshCycle, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DatabaseDrivenLoader class
        /// </summary>
        /// <param name="factory">the factory that is used to initialize plugins</param>
        /// <param name="database">the database that is used to access the configured plugins</param>
        /// <param name="refreshCycle">a timeout whithin the objects must be refreshed</param>
        public DatabaseDrivenLoader(PluginFactory factory, IContextBuffer database, int refreshCycle, bool useGenericParams):base(factory)
        {
            refresher = new Timer(CheckPlugins, null, Timeout.Infinite, Timeout.Infinite);
            this.factory = factory;
            this.database = database;
            this.refreshCycle = refreshCycle;
            this.useGenericParams = useGenericParams;
        }

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

        public bool HasParamsFor(string uniqueName)
        {
            if (!useGenericParams)
            {
                return false;
            }

            using (database.AcquireContext<TContext>(out var db))
            {
                return (from t in db.PluginGenericParameters
                    join p in db.Plugins on t.PluginId equals p.PluginId
                    where p.UniqueName == uniqueName
                    select t.TypeParamId).Any();
            }
        }

        public void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments, Dictionary<string, object> customVariables,
            IStringFormatProvider formatter)
        {
            //knownTypeUsed = false;
            if (useGenericParams)
            {
                using (database.AcquireContext<TContext>(out var db))
                {
                    var data = (from t in db.PluginGenericParameters
                        join p in db.Plugins on t.PluginId equals p.PluginId
                        where p.UniqueName == uniqueName
                        select t).ToArray();
                    /*var joined = from t in genericTypeArguments
                        join d in data on t.GenericTypeName equals d.GenericTypeName
                        select new { Target = t, Type = d.TypeExpression };*/
                    Dictionary<string, object> dic = new Dictionary<string, object>();
                    customVariables ??= new Dictionary<string, object>();
                    //bool kt = knownTypeUsed;
                    customVariables.ForEach(n => dic.Add(n.Key, new SmartProperty
                    {
                        GetterMethod = t =>
                        {
                      //      kt = true;
                            return n.Value;
                        }
                    }));
                    //knownTypeUsed = kt;
                    List<(string name, Type type)> fixTypes = new List<(string name, Type type)>();
                    Type argumentProvider = null;
                    foreach (var j in data)
                    {
                        var t = (Type)ExpressionParser.Parse(j.TypeExpression.ApplyFormat(formatter), dic);
                        if (j.GenericTypeName!= "$$genericArgumentProvider")
                        {
                            fixTypes.Add((name: j.GenericTypeName,
                                type: t));
                        }
                        else
                        {
                            argumentProvider = t;
                        }
                    }

                    if (argumentProvider == null)
                    {
                        var rawTypes = typeof(object).GetInterfaceGenericArgumentsOf(fixTypeEntries: fixTypes.ToArray());
                        if (!genericTypeArguments.FinalizeTypeArguments(rawTypes))
                        {
                            throw new InvalidOperationException(
                                $"Unable to finalize Type with given Information for Plugin {uniqueName}.");
                        }
                    }
                    else
                    {
                        var rawTypes = argumentProvider.GetInterfaceGenericArgumentsOf(fixTypeEntries: fixTypes.ToArray());
                        if (!genericTypeArguments.FinalizeTypeArguments(rawTypes))
                        {
                            throw new InvalidOperationException(
                                $"Unable to finalize Type with given Information for Plugin {uniqueName}.");
                        }
                    }
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
            using (database.AcquireContext<TContext>(out var db))
            {
                var plugins = db.Plugins.Where(n => !(n.Disabled ?? false)).ToArray();
                foreach (var plugin in plugins)
                {
                    if (factory[plugin.UniqueName] == null)
                    {
                        bool ok = false;
                        try
                        {
                            factory.LoadPlugin<IPlugin>(plugin.UniqueName, plugin.Constructor);
                            ok = true;
                        }
                        catch (Exception ex)
                        {
                            LogEnvironment.LogDebugEvent(ex.OutlineException(), LogSeverity.Error);
                            plugin.Disabled = true;
                            plugin.DisabledReason = ex.Message;
                            db.SaveChanges();
                        }

                        if (ok)
                        {
                            yield return plugin.UniqueName;
                        }
                    }
                }
            }
        }
    }
}
