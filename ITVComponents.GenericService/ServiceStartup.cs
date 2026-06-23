//-----------------------------------------------------------------------
// <copyright file="srvStartup.cs" company="IT-Venture GmbH">
//     2009 by IT-Venture GmbH
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Helpers;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
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
            if (param.Action == RunAction.Interactive && param.Run)
            {
                RunInteractive();
                return;
            }

            ServiceStartup srv = new ServiceStartup();
            srv.Configure = param.Action == RunAction.Configure;
            srv.Init();
            var builder = Host.CreateDefaultBuilder().ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService(s => srv);
                var cf = NativeSettings.Configuration.GetSection("HostOptions");
                services.Configure<HostOptions>(cf.GetSection("HostOptions"));
            });
            configureBuilder(builder);
            host = builder.Build();
            if (param.Action == RunAction.Debug || (srv.Configure && param.Run))
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
        /// Loads the configured plugins into a fresh PluginFactory <b>without</b> initializing the deferrables (i.e.
        /// without actually starting the service-workers) and runs an interactive CScript-REPL against that factory.
        /// </summary>
        internal static void RunInteractive()
        {
            ServiceStartup srv = new ServiceStartup();
            srv.Init();
            srv.SetPath(ServiceConfigHelper.Path);
            try
            {
                // Initialize the factory (load plugins + dynamics) but skip InitializeDeferrables so that the
                // service-workers do not actually start. The fully populated factory is handed to the REPL.
                srv.SetupWorkers(false, true);
                InteractiveConsole.Run(srv.pluginLoader);
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
            finally
            {
                srv.pluginLoader.Dispose();
            }
        }

        /// <summary>
        /// Performs further initialization on the service
        /// </summary>
        protected virtual void Init()
        {
            pluginLoader = new PluginFactory(true, false, true, Configure);
            pluginLoader.AllowFactoryParameter = true;
            pluginLoader.ImplementGenericType += ImplementGenericPlugIn;
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
            SetupWorkers(true, false);
        }

        /// <summary>
        /// Initializes the workers driven by this service
        /// </summary>
        /// <param name="initializeDeferrables">indicates whether to initialize the deferrables. Pass <c>false</c> to
        /// load the plugins into the factory without actually starting the service-workers (e.g. for interactive mode)</param>
        private void SetupWorkers(bool initializeDeferrables, bool continueOnFailure)
        {
            foreach (PluginConfigurationItem pi in ServiceConfigHelper.PlugIns)
            {
                if (!pi.Disabled)
                {
                    try
                    {
                        pluginLoader.LoadPlugin<IPlugin>(pi.Name, pi.ConstructionString);
                    }
                    catch (Exception ex)
                    {
                        if (!continueOnFailure)
                        {
                            throw;
                        }
                        else
                        {
                            LogEnvironment.LogEvent($"Failed to load Plugin {pi.Name}, {ex.Message}",
                                LogSeverity.Warning);
                        }
                    }
                }
            }

            pluginLoader.LoadDynamics(!continueOnFailure);
            if (initializeDeferrables)
            {
                pluginLoader.InitializeDeferrables(ServiceConfigHelper.PlugIns.Where(n => !n.Disabled).Select(n => n.Name).ToArray());
            }
        }

        private void ImplementGenericPlugIn(object sender, ImplementGenericTypeEventArgs e)
        {
            var dic = new Dictionary<string, object>();
            if (ServiceConfigHelper.GenericTypeInformation != null &&
                ServiceConfigHelper.GenericTypeInformation.ContainsKey(e.PluginUniqueName))
            {
                var knownTypes = e.KnownArguments ?? new Dictionary<string, object>();
                knownTypes.ForEach(n => dic.Add(n.Key, new SmartProperty
                {
                    GetterMethod = t =>
                    {
                        //e.KnownArgumentsUsed = true;
                        return n.Value;
                    }
                }));
                var tmp = ServiceConfigHelper.GenericTypeInformation[e.PluginUniqueName];
                /*var impl = (from t in e.GenericTypes
                    join j in tmp on t.GenericTypeName equals j.TypeParameterName
                    select new { Param = t, Result = ExpressionParser.Parse(j.TypeExpression.ApplyFormat(e), dic) });*/
                List<(string name, Type type)> fixTypes = new List<(string name, Type type)>();
                Type argumentProvider = null;
                foreach (var item in tmp)
                {
                    var t = (Type)ExpressionParser.Parse(item.TypeExpression.ApplyFormat(e), dic);
                    if (item.TypeParameterName!= "$$genericArgumentProvider")
                    {
                        fixTypes.Add((name: item.TypeParameterName,
                            type: t));
                    }
                    else
                    {
                        argumentProvider = t;
                    }
                }

                if (argumentProvider == null)
                {
                    var rawTypes = typeof(object).GetInterfaceGenericArgumentsOf(fixTypeEntries: fixTypes.ToArray());
                    e.Handled = e.GenericTypes.FinalizeTypeArguments(rawTypes);
                }
                else
                {
                    var rawTypes = argumentProvider.GetInterfaceGenericArgumentsOf(fixTypeEntries: fixTypes.ToArray());
                    e.Handled = e.GenericTypes.FinalizeTypeArguments(rawTypes);
                }
                //e.Handled = true;
            }
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