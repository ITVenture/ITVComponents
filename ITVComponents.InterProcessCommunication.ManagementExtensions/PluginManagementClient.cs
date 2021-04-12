using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Management;

namespace ITVComponents.InterProcessCommunication.ManagementExtensions
{
    public class PluginManagementClient:IPlugin
    {
        /// <summary>
        /// the method name on the server object for getting statistics providing plugins
        /// </summary>
        private const string GetStatisticsProvidersMethod = "GetStatisticsProviders";

        /// <summary>
        /// Adds a statistics job to the list of jobs on the servers management object
        /// </summary>
        private const string AddStatisticJobMethod = "AddStatisticJob";

        /// <summary>
        /// Gets jobs and their latest results from the service
        /// </summary>
        private const string GetStatisticsJobsMethod = "GetStatisticsJobs";

        /// <summary>
        /// the communication object that enables this object to communicate with the targetservice
        /// </summary>
        private IBaseClient communicator;

        /// <summary>
        /// the name of the PluginManagementServer object in the target service
        /// </summary>
        private string managementObject;

        /// <summary>
        /// the serverobject that enables the management of the target-service
        /// </summary>
        private IPluginManagementServer server;

        /// <summary>
        /// Initializes a new instance of the PluginManagementClient object
        /// </summary>
        /// <param name="communicator">the communicator object that is used to connect to the service process</param>
        /// <param name="managementObject">the name of the PluginManagementServer object</param>
        public PluginManagementClient(IBaseClient communicator, string managementObject)
        {
            this.communicator = communicator;
            this.managementObject = managementObject;
            server = communicator.CreateProxy<IPluginManagementServer>(managementObject);
        }
        
        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets a value indicating whether the service object is available on the remote service
        /// </summary>
        public bool Status { get; private set; }

        /// <summary>
        /// Gets a list of statistic providers that are available on the connected service
        /// </summary>
        /// <returns>a list of plugin information objects identifying statistic providing plugins</returns>
        public PluginInformation[] GetStatisticProviders()
        {
            PluginInformation[] retVal = {};
            if (CheckOnline())
            {
                retVal = server.GetStatisticsProviders();
            }

            return retVal;
        }

        /// <summary>
        /// Adds a new job to the list of scheduled statistics jobs
        /// </summary>
        /// <param name="pluginName">the name of the plugin on which to start a statistic collection job</param>
        /// <param name="resetStats">indicates whether to reset the statistics before collecting</param>
        /// <param name="beginTime">the begin time when to start collecting the data</param>
        /// <param name="endTime">the end time when to stop collecting data</param>
        /// <param name="refreshTimeout">the timeout after which to refresh the results periodically</param>
        public void AddStatisticJob(string pluginName, bool resetStats, DateTime beginTime, DateTime endTime,
                                    int refreshTimeout)
        {
            server.AddStatisticJob(pluginName, resetStats, beginTime, endTime, refreshTimeout);
        }

        /// <summary>
        /// Gets statistics jobs from the service object
        /// </summary>
        /// <returns>a list of statistic jobs containing latest results</returns>
        public StatisticJob[] GetStatisticsJobs()
        {
            StatisticJob[] retVal = { };
            if (CheckOnline())
            {
                retVal = server.GetStatisticsJobs();
            }

            return retVal;
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed Event
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Checks whether this object is currently online
        /// </summary>
        /// <returns>a value indicating whether the status of the remote object is ok</returns>
        private bool CheckOnline()
        {
            return communicator.ValidateConnection() &&
                          (communicator.CheckRemoteObjectAvailability(managementObject)?.Available??false);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
