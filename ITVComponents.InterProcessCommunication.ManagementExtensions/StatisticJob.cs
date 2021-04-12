using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions
{
    public class StatisticJob
    {
        /// <summary>
        /// Gets or sets the name of the plugin for which to collect statistics
        /// </summary>
        public string PluginName { get; set; }

        /// <summary>
        /// Gets or sets the time when the collecting of data must be started for a specific object
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the time when the collection of data must be stopped
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to reset the statistics before running this job
        /// </summary>
        public bool ResetStatistics { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this job is currently running
        /// </summary>
        public bool JobRunning { get; set; }

        /// <summary>
        /// The number of minutes after which to refresh the statistics of this job
        /// </summary>
        public int RefreshTimeout { get; set; }

        /// <summary>
        /// Gets or sets the latest result of the statistics collection
        /// </summary>
        public Dictionary<string, object> LastResult { get; set; }

        /// <summary>
        /// Gets or sets the latest update time when the statistic data was refreshed
        /// </summary>
        public DateTime LastUpdate { get; set; }
    }
}
