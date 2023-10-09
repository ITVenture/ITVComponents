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
using ITVComponents.GenericService.Impl;
using ITVComponents.GenericService.PluginLoader;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Helpers;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Plugins.SingletonPattern;
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
        private ISingletonFactory pluginLoader;

        /// <summary>
        /// Gets or sets a value indicating whether this Service is in the configuration mode
        /// </summary>
        protected bool Configure { get; set; }

        /// <summary>
        /// Gets the Startup-Object for the global ServiceStartup
        /// </summary>
        public static IHostStarter Startup { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ServiceStartup class
        /// </summary>
        public ServiceStartup()
        {
        }

        internal static void RunService(StartupArguments param, Action<IHostBuilder> configureBuilder)
        {
            ServiceStartup srv = new ServiceStartup();
            srv.Configure = param.Action == RunAction.Configure;
            Startup = srv.Init();
            Startup.ServiceCollection.AddSingleton<ISingletonFactory>(s =>
            {
                var r = new PluginFactory(PluginInitializationPhase.SingletonStatic, false, s,
                    new ConfigFilePluginLoader());
                r.Start();
                return r;
            });
            Startup.ServiceCollection.AddScoped<IPluginFactory>(s =>
            {
                var sf = s.GetService<ISingletonFactory>();
                var r = new PluginFactory(PluginInitializationPhase.ScopeStatic, false, s,sf,
                    new ConfigFilePluginLoader());
                return r; 

            });
            Startup.ServiceCollection.AddHostedService(s =>
            {
                srv.ServiceProvider = s;
                return srv;
            });

            var cf = NativeSettings.Configuration.GetSection("HostOptions");
            Startup.ServiceCollection.Configure<HostOptions>(cf.GetSection("HostOptions"));
            if (configureBuilder != null)
            {
                Startup.WithHost(configureBuilder);
            }
            //configureBuilder(Startup.HostBuilder);


            if (param.Action == RunAction.Debug || (srv.Configure && param.Run))
            {
                Startup.RunAsync(CancellationToken.None);
                Console.ReadLine();
                //host.StopAsync();
                Startup.Shutdown();
            }
            else
            {
                Startup.Run();
            }
        }

        public IServiceProvider ServiceProvider { get; private set; }

        /// <summary>
        /// Performs further initialization on the service
        /// </summary>
        protected virtual IHostStarter Init()
        {
            IHostStarter retVal = null;
            pluginLoader = new PluginFactory(PluginInitializationPhase.Startup,Configure, new ConfigFilePluginLoader());
            pluginLoader.AllowFactoryParameter = true;
            if (string.IsNullOrEmpty(ServiceConfigHelper.ServiceStartup))
            {
                retVal = new DefaultHostStarter();
                pluginLoader.RegisterObject("startup", retVal);
            }
            else
            {
                retVal = pluginLoader.LoadPlugin<IHostStarter>("startup", ServiceConfigHelper.ServiceStartup, null);
            }

            pluginLoader.ImplementGenericType += ImplementGenericPlugIn;
            return retVal;
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
            /*foreach (PluginConfigurationItem pi in ServiceConfigHelper.PlugIns.Where(n => n.InitializationPhase == PluginInitializationPhase.Singleton && !n.Disabled))
            {
                pluginLoader.LoadPlugin<IPlugin>(pi.Name, pi.ConstructionString);
            }*/

            pluginLoader.Start();
            pluginLoader.InitializeDeferrables();
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
                        e.KnownArgumentsUsed = true;
                        return n.Value;
                    }
                }));
                var tmp = ServiceConfigHelper.GenericTypeInformation[e.PluginUniqueName];
                var impl = (from t in e.GenericTypes
                    join j in tmp on t.GenericTypeName equals j.TypeParameterName
                    select new { Param = t, Result = ExpressionParser.Parse(j.TypeExpression.ApplyFormat(e), dic) });
                foreach (var item in impl)
                {
                    item.Param.TypeResult = (Type)item.Result;
                }

                e.Handled = true;
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