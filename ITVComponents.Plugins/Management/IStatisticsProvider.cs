using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.Management
{
    /// <summary>
    /// Enables a Plugin to provide statistics for other plugins
    /// </summary>
    public interface IStatisticsProvider: IPlugin
    {
        /// <summary>
        /// Instructs a IMetricsProvider Implementing object to start gathering System metrics
        /// </summary>
        /// <returns>a value indicating whether the starting of metrincs-gathering was successful</returns>
        bool BeginCollectStatistics();

        /// <summary>
        /// Instructs a IMetricsProvider implementing object to end the System metrics gathering
        /// </summary>
        /// <returns>indicates whether the stopping of the gathering process was successful</returns>
        bool EndCollectStatistics();

        /// <summary>
        /// Resets the previously collected statistic data
        /// </summary>
        void ResetStats();

        /// <summary>
        /// Gets statistics that were collected since the call of BeginCollectStatistics
        /// </summary>
        /// <returns>a key-value set containing informations about the runtime-status of this object</returns>
        Dictionary<string, object> GetStatistics();
    }
}
