using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.DataExchange.Interfaces;
using ITVComponents.Logging;

namespace ITVComponents.DataExchange
{
    /// <summary>
    /// Collects data using specified structure builders
    /// </summary>
    public class DataCollector : IDataCollector
    {
        /// <summary>
        /// Holds a list of registered mappers
        /// </summary>
        private Dictionary<string, IConnectionMapper> mappers = new Dictionary<string, IConnectionMapper>();

        /// <summary>
        /// Holds a list of registered containers
        /// </summary>
        private Dictionary<string, IDataContainer> containers = new Dictionary<string, IDataContainer>();

        /// <summary>
        /// Holds the list of registered structure builders
        /// </summary>
        private Dictionary<string, IStructureBuilder> builders = new Dictionary<string, IStructureBuilder>();

        /// <summary>
        /// Holds local callbacks for variables-values that need to be set from the caller
        /// </summary>
        private ThreadLocal<Func<string, object>> variableCallback = new ThreadLocal<Func<string, object>>();

        /// <summary>
        /// Collects data for a specific configuration name
        /// </summary>
        /// <param name="builderName">the name of the structure-Builder that is associated with this collector-job</param>
        /// <param name="configuration">the query configurations that must be used for collecting data</param>
        /// <param name="parameterCallback">A Callback that can be used to request values of parameters in queries</param>
        /// <returns>a structured DynamicResult-Array</returns>
        public DynamicResult[] CollectData(string builderName, QueryConfigurationCollection configuration, Func<string,object> parameterCallback)
        {
            IStructureBuilder builder = builders[builderName];
            variableCallback.Value = parameterCallback;
            try
            {
                builder.BuildStructure(RunQuery, configuration);
                return builder.RootCollection;
            }
            finally
            {
                variableCallback.Value = null;
                builder.Clear();
            }
        }

        /// <summary>
        /// Registers a Source for database access
        /// </summary>
        /// <param name="uniqueName">the unique name of the source</param>
        /// <param name="connectionMapper">the connectionmapper that is registered on the provided name</param>
        public void RegisterSource(string uniqueName, IConnectionMapper connectionMapper)
        {
            lock (mappers)
            {
                mappers.Add(uniqueName, connectionMapper);
            }
        }

        /// <summary>
        /// Removes a source from the list of available data sources
        /// </summary>
        /// <param name="uniqueName">the unique name of the data source</param>
        /// <param name="connectionMapper">the mapper that must be removed</param>
        public void UnregisterSource(string uniqueName, IConnectionMapper connectionMapper)
        {
            lock (mappers)
            {
                if (!mappers.ContainsKey(uniqueName) || mappers[uniqueName] != connectionMapper)
                {
                    throw new InvalidOperationException(
                        "The provided connectionMapper is not registered or does not belong to the provided uniqueName");
                }

                mappers.Remove(uniqueName);
            }
        }

        /// <summary>
        /// Registers a DataContainer that will store or buffer data for a data-collection job
        /// </summary>
        /// <param name="uniqueName">the unique name of the container</param>
        /// <param name="container">the container instance</param>
        public void RegisterTarget(string uniqueName, IDataContainer container)
        {
            lock (containers)
            {
                containers.Add(uniqueName, container);
            }
        }

        /// <summary>
        /// Removes a DataContainer that was used to store or buffer data for a data-collection job
        /// </summary>
        /// <param name="uniqueName">the unique name of the container</param>
        /// <param name="container">the container instance</param>
        public void UnregisterTarget(string uniqueName, IDataContainer container)
        {
            lock (containers)
            {
                if (!containers.ContainsKey(uniqueName) || containers[uniqueName] != container)
                {
                    throw new InvalidOperationException(
                        "The provided container is not registered or the registered name is not equal to the provided name");
                }
            }
        }

        /// <summary>
        /// Registers a Structure builder object
        /// </summary>
        /// <param name="uniqueName">the unique name of the structure builder</param>
        /// <param name="builder">the builder instance</param>
        public void RegisterStructureBuilder(string uniqueName, IStructureBuilder builder)
        {
            lock (builders)
            {
                builders.Add(uniqueName, builder);
            }
        }

        /// <summary>
        /// Removes a Structure bulider object
        /// </summary>
        /// <param name="uniqueName">the unique name of the structure buidler</param>
        /// <param name="builder">the builder instance</param>
        public void UnregisterStructureBuilder(string uniqueName, IStructureBuilder builder)
        {
            lock (builders)
            {
                if (!builders.ContainsKey(uniqueName) || builders[uniqueName] != builder)
                {
                    throw new InvalidOperationException(
                        "The provided builder is not registered, or the registered name is not equal to the provided name");
                }

                builders.Remove(uniqueName);
            }
        }

        /// <summary>
        /// Runs the specified query and registers the result in the defined targets
        /// </summary>
        /// <param name="query">the query that must added to some targets</param>
        private void RunQuery(QueryDefinition query)
        {
            DynamicResult[] data;
            using (var db = mappers[query.Source].AcquireDatabase())
            {
                List<IDbDataParameter> parameters = new List<IDbDataParameter>();
                foreach (var param in query.Parameters)
                {
                    string callbackName;
                    if (!(param.ParameterValue is string && (callbackName=((string) param.ParameterValue)).StartsWith("!!")))
                    {
                        parameters.Add(db.GetParameter(param.ParameterName, param.ParameterValue));
                    }
                    else
                    {
                        parameters.Add(db.GetParameter(param.ParameterName,
                                                       variableCallback.Value(callbackName.Substring(2))));
                    }
                }

                LogEnvironment.LogDebugEvent(null, string.Format("Running Query: {0}", query.Query), (int) LogSeverity.Report, null);
                if (query.QueryType == QueryType.Query)
                {
                    data = db.GetNativeResults(query.Query, null, parameters.ToArray());
                }
                else if (query.QueryType == QueryType.Procedure)
                {
                    var tmpData = db.CallProcedure(query.Query, null, parameters.ToArray());
                    data = tmpData[query.DesiredResultSet];
                }
                else
                {
                    throw new InvalidOperationException("Unsupported Query Type provided!");
                }
            }

            foreach (var target in query.Targets)
            {
                containers[target.TargetName].RegisterData(target.RegisterAs, data);
            }
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
        }
    }
}