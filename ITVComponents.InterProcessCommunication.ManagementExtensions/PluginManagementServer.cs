using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Management;
using ITVComponents.Serialization;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions
{
    public class PluginManagementServer:IPlugin, IPluginManagementServer, IDeferredInit
    {
        /// <summary>
        /// provides a list of all loaded plugins in the current runtime-environment
        /// </summary>
        private PluginFactory factory;

        /*/// <summary>
        /// a list of runtime configurable objects
        /// </summary>
        private List<IRuntimeConfigurable> runtimeConfigurables;

        /// <summary>
        /// a list of objects that can provide statistics
        /// </summary>
        private List<IStatisticsProvider> statisticsProviders;*/

        private IEnumerable<IStatisticsProvider> statisticsProviders;

        /// <summary>
        /// a list of statisticjobs that are used to monitor jobs for a specific duration
        /// </summary>
        private List<StatisticJob> statisticJobs;

        /// <summary>
        /// A Refreshing timer that is used to collect statistics for jobs that are currently running
        /// </summary>
        private Timer refreshTimer;

        /// <summary>
        /// the timeout in which the jobs are being refreshed
        /// </summary>
        private int jobRefreshTimeout;

        /// <summary>
        /// a value instructing the housekeep mechanism how long statistics remain in memory before they are removed from the list of jobs
        /// </summary>
        private int keepDaysBeforeHousekeep;

        /// <summary>
        /// Initializes a new instance of the PluginManagementServer class
        /// </summary>
        /// <param name="factory">the plugin factory that is used to get loaded plugins that may be managable</param>
        /// <param name="jobRefreshTimeout">the timeout after which a timer checks whether jobs need updates</param>
        /// <param name="keepDaysBeforeHousekeep">number of days before statistic jobs are being removed from the list of processed jobs</param>
        public PluginManagementServer(PluginFactory factory, int jobRefreshTimeout, int keepDaysBeforeHousekeep)
            : this()
        {
            this.jobRefreshTimeout = jobRefreshTimeout;
            this.keepDaysBeforeHousekeep = keepDaysBeforeHousekeep;
            this.factory = factory;
            statisticsProviders = from t in factory where t is IStatisticsProvider select (IStatisticsProvider)t;
            //factory.PluginInitialized += PluginInitialized;
        }

        /// <summary>
        /// Prevents a default instance of the PluginManagementServer class from being created
        /// </summary>
        private PluginManagementServer()
        {
            //runtimeConfigurables = new List<IRuntimeConfigurable>();
            //statisticsProviders = new List<IStatisticsProvider>();
            statisticJobs = new List<StatisticJob>();
            refreshTimer = new Timer(CheckJobs, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets a list of statistics providing plugins
        /// </summary>
        /// <returns>a list of plugins that are able to provide runtime statistics</returns>
        public PluginInformation[] GetStatisticsProviders()
        {
            return (from t in statisticsProviders
                    select new PluginInformation {PluginName = t.UniqueName, PluginType = t.GetType().FullName}).ToArray
                ();
        }

        /*/// <summary>
        /// Gets a list of runtimeconfigurable plugins that are currently loaded in this application
        /// </summary>
        /// <returns>a list of runtime-configurable objects</returns>
        public PluginInformation[] GetRuntimeConfigurables()
        {
            return (from t in runtimeConfigurables
                    select new PluginInformation {PluginName = t.UniqueName, PluginType = t.GetType().FullName}).ToArray
                ();
        }*/

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
           // factory.PluginInitialized -= PluginInitialized;
            OnDisposed();
        }


        /// <summary>
        /// Adds a new job to the list of scheduled statistics jobs
        /// </summary>
        /// <param name="pluginName"></param>
        /// <param name="resetStats"></param>
        /// <param name="beginTime"></param>
        /// <param name="endTime"></param>
        /// <param name="refreshTimeout"></param>
        /// <returns></returns>
        public void AddStatisticJob(string pluginName, bool resetStats, DateTime beginTime, DateTime endTime, int refreshTimeout)
        {
            lock (statisticJobs)
            {
                StatisticJob equJob = (from t in statisticJobs
                                       where
                                           t.PluginName == pluginName && ((beginTime >= t.StartTime && beginTime <= t.EndTime) ||
                                           (endTime >= t.StartTime && endTime <= t.EndTime))
                                       select t).FirstOrDefault();
                if (equJob != null)
                {
                    throw new Exception("There's already a job scheduled for the defined period.");
                }

                StatisticJob newJob = new StatisticJob
                                          {
                                              PluginName = pluginName,
                                              StartTime = beginTime,
                                              EndTime = endTime,
                                              ResetStatistics = resetStats,
                                              RefreshTimeout =
                                                  refreshTimeout != 0 ? refreshTimeout : this.jobRefreshTimeout
                                          };
                statisticJobs.Add(newJob);
            }
        }

        /// <summary>
        /// Gets a list of scheduled jobs and their current status
        /// </summary>
        /// <returns>a list of jobs that are currently collecting statistics</returns>
        public StatisticJob[] GetStatisticsJobs()
        {
            lock (statisticJobs)
            {
                return statisticJobs.ToArray();
            }
        }

        /*/// <summary>
        /// Gets the configuration for the provided pluginName
        /// </summary>
        /// <param name="pluginName">the plugin from which to get the configurable parameters</param>
        /// <returns>a list of configurable parameters</returns>
        public Dictionary<string, ConfigurationDefinition> GetPluginConfiguration(string pluginName)
        {
            var plugin = (from t in runtimeConfigurables where t.UniqueName == pluginName select t).FirstOrDefault();
            return plugin != null ? plugin.GetParameters() : null;
        }*/

        /*/// <summary>
        /// Sets a specific configuration parameter on a runtime configurable object
        /// </summary>
        /// <param name="pluginName">the plugin name on which to set a runtime parameter</param>
        /// <param name="parameterName">the name of the runtime parameter</param>
        /// <param name="configurationValue">the new value of the runtime parameter</param>
        /// <returns>a value indicating whether the configuration was successfully applied on the target object</returns>
        public bool SetPluginConfiguration(string pluginName, string parameterName, object configurationValue)
        {
            var plugin = (from t in runtimeConfigurables where t.UniqueName == pluginName select t).FirstOrDefault();
            bool ok = plugin != null;
            if (ok)
            {
                ok = plugin.SetParameter(parameterName, configurationValue);
            }

            return ok;
        }*/

        /// <summary>
        /// Raises the disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /*/// <summary>
        /// is triggered when a plugin is initialized and adds classes that are managable (IRuntimeConfigurable or IStatisticsProvider) to according lists
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">the event arguments</param>
        private void PluginInitialized(object sender, PluginInitializedEventArgs e)
        {
            Detect(e.Plugin);
        }

        /// <summary>
        /// Detects whether a plugin can provide statistic informations or is configurable at runtime
        /// </summary>
        /// <param name="plugin">a plugin that has been loaded by a plugin-factory connected to this management server</param>
        private void Detect(IPlugin plugin)
        {
            if (plugin is IRuntimeConfigurable)
            {
                runtimeConfigurables.Add((IRuntimeConfigurable) plugin);
            }

            if (plugin is IStatisticsProvider)
            {
                statisticsProviders.Add((IStatisticsProvider) plugin);
            }
        }*/

        /// <summary>
        /// Checks all configured jobs if they need to be refreshed or to be started/stopped
        /// </summary>
        /// <param name="state">**ignored parameter**</param>
        private void CheckJobs(object state)
        {
            refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                lock (statisticJobs)
                {
                    DateTime now = DateTime.Now;
                    var currentJobs = (from t in statisticJobs
                     join j in statisticsProviders on t.PluginName equals j.UniqueName
                     where (now >= t.StartTime && now <= t.EndTime && now.Subtract(t.LastUpdate).TotalMinutes > t.RefreshTimeout) || (now > t.EndTime && t.JobRunning)
                     select new {Target = j, Job = t});
                    foreach (var job in currentJobs)
                    {
                        if (!job.Job.JobRunning)
                        {
                            try
                            {
                                if (job.Job.ResetStatistics)
                                {
                                    job.Target.ResetStats();
                                }

                                job.Job.JobRunning = job.Target.BeginCollectStatistics();
                            }
                            catch (Exception ex)
                            {
                                LogEnvironment.LogEvent(string.Format(@"Collection data failed because
{0}", ex.Message), LogSeverity.Warning);
                            }
                        }
                        else
                        {
                            try
                            {
                                job.Job.LastResult = job.Target.GetStatistics();
                                job.Job.LastUpdate = DateTime.Now;
                                if (job.Job.EndTime < DateTime.Now)
                                {
                                    if (job.Target.EndCollectStatistics())
                                    {
                                        job.Job.JobRunning = false;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogEnvironment.LogEvent(string.Format(@"Stopping data collection failed because
{0}", ex.Message),LogSeverity.Warning);
                            }
                        }
                    }

                    StatisticJob[] removeJobs =
                        (from t in statisticJobs
                         where now.Subtract(t.EndTime).TotalDays > keepDaysBeforeHousekeep
                         select t).ToArray();
                    foreach (StatisticJob j in removeJobs)
                    {
                        statisticJobs.Remove(j);
                    }
                }
            }
            finally
            {
                refreshTimer.Change(jobRefreshTimeout, jobRefreshTimeout);
            }
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;

        public bool Initialized { get; private set; }
        public bool ForceImmediateInitialization => false;
        public void Initialize()
        {
            if (!Initialized)
            {
                try
                {
                    refreshTimer.Change(jobRefreshTimeout, jobRefreshTimeout);
                }
                finally
                {
                    Initialized = true;
                }
            }
        }
    }
}
