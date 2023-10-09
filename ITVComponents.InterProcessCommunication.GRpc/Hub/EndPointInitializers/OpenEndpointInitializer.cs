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
    public class OpenEndPointInitializer: IServiceHostConfigurator
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
    }
}
