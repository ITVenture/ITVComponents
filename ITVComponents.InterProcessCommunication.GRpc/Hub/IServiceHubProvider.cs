using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub
{
    public interface IServiceHubProvider
    {
        /// <summary>
        /// Gets the EndPoint broker instance that manages all traffic between the communication endpoints
        /// </summary>
        EndPointBroker Broker { get; }

        /// <summary>
        /// Registers a ConfigureProvider that configures the AppBuilder or the Hosting-Environment
        /// </summary>
        /// <param name="provider">the ConfigureProvider that initializes specific services</param>
        void RegisterAppConfigureProvider(IAppConfigureProvider provider);

        /// <summary>
        /// Registers a ConfigureProvider that registers services for the DependencyInjection
        /// </summary>
        /// <param name="provider">the ConfigureProvider that initializes specific services</param>
        void RegisterServicesConfigureProvider(IServicesConfigureProvider provider);

        /// <summary>
        /// Registers a ConfigureProvider that configures the WebHostBuilder
        /// </summary>
        /// <param name="provider">the ConfigureProvider that initializes specific services</param>
        void RegisterWebHostBuilderConfigureProvider(IWebHostBuilderConfigureProvider provider);

        /// <summary>
        /// Registers an endpoint-initializer instance that can be used to initialize a specific grpc endpoint
        /// </summary>
        /// <param name="initializer"></param>
        void RegisterEndPointInitializer(IEndPointInitializer initializer);

        /// <summary>
        /// Registers an EndPointDefaultsConfigurator - Instance that can configure Kestrel-Listenoptions
        /// </summary>
        /// <param name="configurator">the configurator instance that is capable for configuring Listener-Options</param>
        void RegisterEndPointDefaultsConfigurator(IEndPointDefaultsConfigurator configurator);
        
        /// <summary>
        /// Configures the app-builder
        /// </summary>
        /// <param name="app">the application builder instance that is being configured</param>
        /// <param name="env">the host-environment for the current webapp</param>
        void ConfigureApp(IApplicationBuilder app, IWebHostEnvironment env);

        /// <summary>
        /// Initializes dependency-Injection services
        /// </summary>
        /// <param name="services">the dependency-injection environment</param>
        void ConfigureServices(IServiceCollection services);

        /// <summary>
        /// Initializes the EndPoints using the registered EndPointInitializers
        /// </summary>
        /// <param name="endpointRouteBuilder">the EndpointBuilder that is used to configure the endpoints</param>
        void MapEndPoints(IEndpointRouteBuilder endpointRouteBuilder);
    }
}
