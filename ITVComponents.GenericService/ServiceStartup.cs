//-----------------------------------------------------------------------
// <copyright file="srvStartup.cs" company="IT-Venture GmbH">
//     2009 by IT-Venture GmbH
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting; //using ITVComponents.Logging;

namespace ITVComponents.GenericService
{
    /// <summary>
    /// Mainclass of the Generic Service
    /// </summary>
    public class ServiceStartup : BackgroundService
    {
        /// <summary>
        /// the loader object used to load the worker plugins
        /// </summary>
        private PluginFactory pluginLoader;

        /// <summary>
        /// The ServiceHost that is running the local service
        /// </summary>
        private static IHost host;

        /// <summary>
        /// Gets or sets a value indicating whether this Service is in the configuration mode
        /// </summary>
        protected bool Configure { get; set; }

        /// <summary>
        /// Initializes a new instance of the ServiceStartup class
        /// </summary>
        public ServiceStartup()
        {
        }

        internal static void RunService(StartupArguments param, Action<IHostBuilder> configureBuilder)
        {
            ServiceStartup srv = new ServiceStartup();
            srv.Configure = param.Configure;
            srv.Init();
            var builder = Host.CreateDefaultBuilder().ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService(s => srv);
            });
            configureBuilder(builder);
            host = builder.Build();
            if (param.Debug)
            {
                host.RunAsync();
                Console.ReadLine();
                //host.StopAsync();
                host.Dispose();
            }
            else
            {
                host.Run();
            }
        }

        /// <summary>
        /// Performs further initialization on the service
        /// </summary>
        protected virtual void Init()
        {
            pluginLoader = new PluginFactory(true, false, true, Configure);
            pluginLoader.AllowFactoryParameter = true;
        }

        /// <summary>
        /// This should never be called!
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            //return base.StartAsync(cancellationToken);
            //pluginLoader.CriticalError += EmergencyExit;
            //this.SetupLog();
            this.SetPath(ServiceConfigHelper.Path);
            try
            {
                SetupWorkers();
            }
            catch (Exception ex)
            {
                Exception e = ex;
                while (e != null)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                    e = e.InnerException;
                }
            }

            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            pluginLoader.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Initializes the workers driven by this service
        /// </summary>
        private void SetupWorkers()
        {
            foreach (PluginConfigurationItem pi in ServiceConfigHelper.PlugIns)
            {
                if (!pi.Disabled)
                {
                    pluginLoader.LoadPlugin<IPlugin>(pi.Name, pi.ConstructionString);
                }
            }

            pluginLoader.LoadRuntimeStatus();
            pluginLoader.InitializeDeferrables(ServiceConfigHelper.PlugIns.Where(n => !n.Disabled).Select(n => n.Name).ToArray());
        }

        /// <summary>
        /// Extends the Path environment variable by a particular value
        /// </summary>
        /// <param name="pathAddition">the Additional value to add to the System-path value</param>
        private void SetPath(string pathAddition)
        {
            if (!string.IsNullOrEmpty(pathAddition))
            {
                string oldPath = Environment.GetEnvironmentVariable("Path");
                if (oldPath != string.Empty && !oldPath.EndsWith(";"))
                {
                    oldPath += ";";
                }

                Environment.SetEnvironmentVariable("Path", oldPath + pathAddition, EnvironmentVariableTarget.Process);
            }
        }
    }
}