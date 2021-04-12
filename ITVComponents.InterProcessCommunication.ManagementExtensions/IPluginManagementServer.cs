using System;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions
{
    public interface IPluginManagementServer
    {
        /// <summary>
        /// Gets a list of statistics providing plugins
        /// </summary>
        /// <returns>a list of plugins that are able to provide runtime statistics</returns>
        PluginInformation[] GetStatisticsProviders();

        /// <summary>
        /// Adds a new job to the list of scheduled statistics jobs
        /// </summary>
        /// <param name="pluginName"></param>
        /// <param name="resetStats"></param>
        /// <param name="beginTime"></param>
        /// <param name="endTime"></param>
        /// <param name="refreshTimeout"></param>
        /// <returns></returns>
        void AddStatisticJob(string pluginName, bool resetStats, DateTime beginTime, DateTime endTime, int refreshTimeout);

        /// <summary>
        /// Gets a list of scheduled jobs and their current status
        /// </summary>
        /// <returns>a list of jobs that are currently collecting statistics</returns>
        StatisticJob[] GetStatisticsJobs();
    }
}