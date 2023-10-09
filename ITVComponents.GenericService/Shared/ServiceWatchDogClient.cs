using System;
using System.Diagnostics;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.WatchDogs;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Settings;

namespace ITVComponents.GenericService.Shared
{
    public class ServiceWatchDogClient : IDeferredInit, IDisposable
    {
        /// <summary>
        /// The client object that is used to connect to the watchDog service
        /// </summary>
        private readonly IBaseClient client;

        /// <summary>
        /// The name of the watchdog-object on the service
        /// </summary>
        private readonly string watchDogName;

        /// <summary>
        /// Initializes a new instance of the ServiceWatchDogClient class
        /// </summary>
        /// <param name="client">the client object that provides a connection to the watchDog service</param>
        /// <param name="watchDogName">the name of the watchDog object</param>
        public ServiceWatchDogClient(IBaseClient client, string watchDogName)
        {
            this.client = client;
            this.watchDogName = watchDogName;
        }
        
        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization { get; } = false;

        /// <summary>Initializes this deferred initializable object</summary>
        public void Initialize()
        {
            var proxy = client.CreateProxy<IIpcWinSvcWatchDog>(watchDogName);
            ServiceConfig Section = JsonSettingsSection.GetSection<ServiceConfig>("GenericService_ServiceConfiguration");
            if (Section.UseExtConfig)
            {
                var cp = Process.GetCurrentProcess();
                proxy.RegisterService(cp.MachineName, Section.ServiceName, cp.ProcessName, cp.Id);
            }
            else
            {
                LogEnvironment.LogEvent("Unable to register a service that is using xml-configuration", LogSeverity.Error);
            }

            Initialized = true;
        }

        /// <summary>Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.</summary>
        public void Dispose()
        {
            var proxy = client.CreateProxy<IIpcWinSvcWatchDog>(watchDogName);
            ServiceConfig Section = JsonSettingsSection.GetSection<ServiceConfig>("GenericService_ServiceConfiguration");
            if (Section.UseExtConfig)
            {
                var cp = Process.GetCurrentProcess();
                proxy.RegisterRegularShutdown(cp.MachineName, Section.ServiceName, cp.ProcessName, cp.Id);
            }
        }
    }
}
