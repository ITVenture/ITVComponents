using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Protos;
using ITVComponents.InterProcessCommunication.MessagingShared.DependencyExtensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.Plugins;
using Microsoft.AspNetCore;
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
        /*private List<IAppConfigureProvider> appConfigProviders;
        private List<IServicesConfigureProvider> serviceConfigProviders;
        private List<IWebHostBuilderConfigureProvider> webHostBuilderConfigProviders;
        private List<IEndPointInitializer> endPointInitializers;
        private List<IEndPointDefaultsConfigurator> listenOptionsConfigurators;
        private IHost webHost;*/
        private List<IServiceHubConfigurator> configurators = new();
        private bool ownsBroker = true;
        private readonly bool ownsApp = true;

        private WebApplication app;

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

        public WebApplicationBuilder WebAppBuilder { get; }
        public void RegisterConfigurator(IServiceHubConfigurator configurator)
        {
            configurators.Add(configurator);
        }

        /// <summary>
        /// Initializes a new instance of the ServiceHubProvider class
        /// </summary>
        /// <param name="hubAddresses">the hub-addresses for this serviceHub</param>
        /// <param name="basePath">Configures a base-path that extends the host-url of this hub</param>
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
            Broker = new EndPointBroker();
            WebAppBuilder = WebApplication.CreateBuilder();
        }

        public ServiceHubProvider(MessagingShared.Hub.IServiceHubProvider parent, string hubAddresses, PluginFactory factory)
        {
            if (instance != null)
            {
                throw new NotSupportedException("Can have only one active Instance of ServiceHubProvider at a time");
            }

            instance = this;
            this.hubAddresses = hubAddresses;
            this.factory = factory;
            Broker = parent.Broker;
            ownsBroker = false;
            WebAppBuilder = WebApplication.CreateBuilder();
        }

        public ServiceHubProvider(WebApplicationBuilder parentApp, string hubAddresses, PluginFactory factory)
        {
            if (instance != null)
            {
                throw new NotSupportedException("Can have only one active Instance of ServiceHubProvider at a time");
            }

            instance = this;
            this.hubAddresses = hubAddresses;
            this.factory = factory;
            Broker = new EndPointBroker();
            WebAppBuilder = parentApp;
            ownsApp = false;
        }

        public ServiceHubProvider(MessagingShared.Hub.IServiceHubProvider parent, WebApplicationBuilder parentApp, string hubAddresses, PluginFactory factory)
        {
            if (instance != null)
            {
                throw new NotSupportedException("Can have only one active Instance of ServiceHubProvider at a time");
            }

            instance = this;
            this.hubAddresses = hubAddresses;
            this.factory = factory;
            Broker = parent.Broker;
            ownsBroker = false;
            WebAppBuilder = parentApp;
            ownsApp = false;
        }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            if (!Initialized)
            {
                SetDefaults();
                configurators.ForEach(n => n.ConfigureBuilder(WebAppBuilder));
                if (ownsApp)
                {
                    app = WebAppBuilder.Build();
                    if (!string.IsNullOrEmpty(basePath))
                    {
                        app.UsePathBase(new PathString(basePath));
                    }

                    InitializeApp(app);
                    app.RunAsync();
                }

                Initialized = true;
            }
        }

        public void InitializeApp(WebApplication app)
        {
            configurators.ForEach(n => n.ConfigureApp(app));
        }

        /// <summary>
        /// When called, a Dependency is installed that allows any object in a web-application to request access to a specific service
        /// </summary>
        public void SetupClientFactory()
        {
            WebAppBuilder.Services.AddScoped<IClientFactory, DefaultClientFactory>();
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            if (ownsApp)
            {
                app?.StopAsync().GetAwaiter().GetResult();
            }

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

        private void SetDefaults()
        {
            WebAppBuilder.Services.AddSingleton<IServiceHubProvider>(this);
            if (ownsApp)
            {
                WebAppBuilder.WebHost.UseUrls((from t in hubAddresses.Split(';') select t.Trim()).ToArray());
                WebAppBuilder.WebHost.UseKestrel(k =>
                {
                    k.Limits.MaxRequestBodySize = null;
                    k.ConfigureEndpointDefaults(li => li.Protocols = HttpProtocols.Http2);
                });

                WebAppBuilder.Services.AddControllers();
            }

            WebAppBuilder.Services.AddGrpc(opt =>
            {
                opt.EnableDetailedErrors = true;
                opt.MaxReceiveMessageSize = null;
                opt.MaxSendMessageSize = null;
            });
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
