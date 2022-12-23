using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.GenericService.WebService;
using ITVComponents.GenericService.WebService.Configurators;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Hubs;
using ITVComponents.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.EndPointInitializers
{
    public class OpenEndPointInitializer:IServiceHostConfigurator, IPlugin
    {
        /// <summary>
        /// Initializes a new instance of the OpenEndpointInitializer class
        /// </summary>
        /// <param name="parent"></param>
        public OpenEndPointInitializer(IWebHostStartup parent)
        {
            parent.RegisterConfigurator(this);
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Configures the WebApplication builder (inject services, set defaults, etc.)
        /// </summary>
        /// <param name="builder">the web-application builder that is used to setup a grpc service</param>
        public void ConfigureBuilder(WebApplicationBuilder builder)
        {   
        }

        /// <summary>
        /// Configures the app after it is built. (e.g. build the service middleware pipeline
        /// </summary>
        /// <param name="app">the built app</param>
        public void ConfigureApp(WebApplication app)
        {
            app.MapGrpcService<OpenServiceHubRpc>();
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }
        
        /// <summary>
        /// raises the disposed event
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
