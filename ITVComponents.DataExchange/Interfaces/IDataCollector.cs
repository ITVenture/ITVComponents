using System;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.Plugins;

namespace ITVComponents.DataExchange.Interfaces
{
    public interface IDataCollector:IPlugin
    {
        /// <summary>
        /// Collects data for a specific configuration name
        /// </summary>
        /// <param name="builderName">the name of the structure-Builder that is associated with this collector-job</param>
        /// <param name="configuration">the query configurations that must be used for collecting data</param>
        /// <param name="parameterCallback">A Callback that can be used to request values of parameters in queries</param>
        /// <returns>a structured DynamicResult-Array</returns>
        DynamicResult[] CollectData(string builderName, QueryConfigurationCollection configuration, Func<string,object> parameterCallback);

        /// <summary>
        /// Registers a Source for database access
        /// </summary>
        /// <param name="uniqueName">the unique name of the source</param>
        /// <param name="connectionMapper">the connectionmapper that is registered on the provided name</param>
        void RegisterSource(string uniqueName, IConnectionMapper connectionMapper);

        /// <summary>
        /// Removes a source from the list of available data sources
        /// </summary>
        /// <param name="uniqueName">the unique name of the data source</param>
        /// <param name="connectionMapper">the mapper that must be removed</param>
        void UnregisterSource(string uniqueName, IConnectionMapper connectionMapper);

        /// <summary>
        /// Registers a DataContainer that will store or buffer data for a data-collection job
        /// </summary>
        /// <param name="uniqueName">the unique name of the container</param>
        /// <param name="container">the container instance</param>
        void RegisterTarget(string uniqueName, IDataContainer container);

        /// <summary>
        /// Removes a DataContainer that was used to store or buffer data for a data-collection job
        /// </summary>
        /// <param name="uniqueName">the unique name of the container</param>
        /// <param name="container">the container instance</param>
        void UnregisterTarget(string uniqueName, IDataContainer container);

        /// <summary>
        /// Registers a Structure builder object
        /// </summary>
        /// <param name="uniqueName">the unique name of the structure builder</param>
        /// <param name="builder">the builder instance</param>
        void RegisterStructureBuilder(string uniqueName, IStructureBuilder builder);

        /// <summary>
        /// Removes a Structure bulider object
        /// </summary>
        /// <param name="uniqueName">the unique name of the structure buidler</param>
        /// <param name="builder">the builder instance</param>
        void UnregisterStructureBuilder(string uniqueName, IStructureBuilder builder);
    }
}
