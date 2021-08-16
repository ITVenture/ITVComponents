using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Protos;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub
{
    public class ServiceHubProvider : IPlugin, IDeferredInit, IServiceHubProvider
    {
        private static ServiceHubProvider instance;

        private readonly string hubAddresses;
        private readonly string basePath;
        private readonly PluginFactory factory;
        private List<IAppConfigureProvider> appConfigProviders;
        private List<IServicesConfigureProvider> serviceConfigProviders;
        private List<IWebHostBuilderConfigureProvider> webHostBuilderConfigProviders;
        private List<IEndPointInitializer> endPointInitializers;
        private List<IEndPointDefaultsConfigurator> listenOptionsConfigurators;
        private IHost webHost;
        private bool ownsBroker = true;

        /// <summary>
        /// Gets the initialized singleton-instance of the ServiceHubProvider
        /// </summary>
        internal static ServiceHubProvider Instance => instance;

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization { get; } = false;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        public EndPointBroker Broker { get; }

        /// <summary>
        /// Initializes a new instance of the ServiceHubProvider class
        /// </summary>
        /// <param name="hubAddresses">the hub-addresses for this serviceHub</param>
        /// <param name="basePath">Cnfigures a base-path that extends the host-url of this hub</param>
        /// <param name="factory">a factory that provides access to other plugins</param>
        public ServiceHubProvider(string hubAddresses, PluginFactory factory)
        {
            if (instance != null)
            {
                throw new NotSupportedException("Can have only one active Instance of ServiceHubProvider at a time");
            }

            instance = this;
            this.hubAddresses = hubAddresses;
            this.factory = factory;
            appConfigProviders = new List<IAppConfigureProvider>();
            serviceConfigProviders = new List<IServicesConfigureProvider>();
            webHostBuilderConfigProviders = new List<IWebHostBuilderConfigureProvider>();
            endPointInitializers = new List<IEndPointInitializer>();
            listenOptionsConfigurators = new List<IEndPointDefaultsConfigurator>();
            Broker = new EndPointBroker();
        }

        public ServiceHubProvider(ITVComponents.InterProcessCommunication.MessagingShared.Hub.IServiceHubProvider parent, string hubAddresses, PluginFactory factory)
        {
            if (instance != null)
            {
                throw new NotSupportedException("Can have only one active Instance of ServiceHubProvider at a time");
            }

            instance = this;
            this.hubAddresses = hubAddresses;
            this.factory = factory;
            appConfigProviders = new List<IAppConfigureProvider>();
            serviceConfigProviders = new List<IServicesConfigureProvider>();
            webHostBuilderConfigProviders = new List<IWebHostBuilderConfigureProvider>();
            endPointInitializers = new List<IEndPointInitializer>();
            listenOptionsConfigurators = new List<IEndPointDefaultsConfigurator>();
            Broker = parent.Broker;
            ownsBroker = false;
        }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            if (!Initialized)
            {
                webHost = Host.CreateDefaultBuilder()
                    //.UseWindowsService()
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton<IServiceHubProvider>(this);
                    })
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseUrls((from t in hubAddresses.Split(';') select t.Trim()).ToArray());
                        webBuilder.UseStartup<ServiceStartup>();
                        webHostBuilderConfigProviders.ForEach(n => n.Configure(webBuilder));
                        webBuilder.UseKestrel(k =>
                        {
                            k.Limits.MaxRequestBodySize = null;
                        });
                        webBuilder.ConfigureKestrel((h, o) =>
                        {
                            o.ConfigureEndpointDefaults(li =>
                            {
                                li.Protocols = HttpProtocols.Http2;
                                listenOptionsConfigurators.ForEach(n => n.ConfigureEndPointDefaults(li));
                            });
                        });
                    }).Build();
                webHost.RunAsync();
                Initialized = true;
            }
        }

        /// <summary>
        /// Registers a ConfigureProvider that configures the AppBuilder or the Hosting-Environment
        /// </summary>
        /// <param name="provider">the ConfigureProvider that initializes specific services</param>
        public void RegisterAppConfigureProvider(IAppConfigureProvider provider)
        {
            appConfigProviders.Add(provider);
        }

        /// <summary>
        /// Registers a ConfigureProvider that registers services for the DependencyInjection
        /// </summary>
        /// <param name="provider">the ConfigureProvider that initializes specific services</param>
        public void RegisterServicesConfigureProvider(IServicesConfigureProvider provider)
        {
            serviceConfigProviders.Add(provider);
        }

        /// <summary>
        /// Registers a ConfigureProvider that configures the WebHostBuilder
        /// </summary>
        /// <param name="provider">the ConfigureProvider that initializes specific services</param>
        public void RegisterWebHostBuilderConfigureProvider(IWebHostBuilderConfigureProvider provider)
        {
            webHostBuilderConfigProviders.Add(provider);
        }

        /// <summary>
        /// Registers an endpoint-initializer instance that can be used to initialize a specific grpc endpoint
        /// </summary>
        /// <param name="initializer"></param>
        public void RegisterEndPointInitializer(IEndPointInitializer initializer)
        {
            endPointInitializers.Add(initializer);
        }

        /// <summary>
        /// Registers an EndPointDefaultsConfigurator - Instance that can configure Kestrel-Listenoptions
        /// </summary>
        /// <param name="configurator">the configurator instance that is capable for configuring Listener-Options</param>
        public void RegisterEndPointDefaultsConfigurator(IEndPointDefaultsConfigurator configurator)
        {
            listenOptionsConfigurators.Add(configurator);
        }

        /// <summary>
        /// Configures the app-builder
        /// </summary>
        /// <param name="app">the application builder instance that is being configured</param>
        /// <param name="env">the host-environment for the current webapp</param>
        public void ConfigureApp(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (!string.IsNullOrEmpty(basePath))
            {
                app.UsePathBase(new PathString(basePath));
            }

            appConfigProviders.ForEach(n => n.Configure(app, env));
        }

        /// <summary>
        /// Initializes dependency-Injection services
        /// </summary>
        /// <param name="services">the dependency-injection environment</param>
        public void ConfigureServices(IServiceCollection services)
        {
            serviceConfigProviders.ForEach(n => n.ConfigureServices(services));
        }

        /// <summary>
        /// Initializes the EndPoints using the registered EndPointInitializers
        /// </summary>
        /// <param name="endpointRouteBuilder">the EndpointBuilder that is used to configure the endpoints</param>
        public void MapEndPoints(IEndpointRouteBuilder endpointRouteBuilder)
        {
            endPointInitializers.ForEach(n => n.MapEndPoints(endpointRouteBuilder));
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            webHost?.StopAsync().GetAwaiter().GetResult();
            if (ownsBroker)
            {
                Broker.Dispose();
            }
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
