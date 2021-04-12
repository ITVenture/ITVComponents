using System;
using ITVComponents.DataExchange.Configuration;

namespace ITVComponents.DataExchange.Interfaces
{
    /// <summary>
    /// Is used to build a structured dataset
    /// </summary>
    public interface IStructureBuilder: IDataContainer
    {
        /// <summary>
        /// Builds a structure and uses the specified query-callback
        /// </summary>
        /// <param name="queryCallback">the query-callback that is used to the get structure-queries</param>
        /// <param name="configuration">the Configuration that must be used for this structurebuild-run</param>
        void BuildStructure(ExecuteQuery queryCallback, QueryConfigurationCollection configuration);
    }
}
