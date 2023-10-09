using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ITVComponents.GenericService.WebService;
using ITVComponents.GenericService.WebService.Configurators;
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

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.EndPointInitializers
{
    public class GrpcInit : IServiceHostConfigurator
    {
        private readonly IServiceHubProvider hubProvider;
        private readonly bool configureKestrel = true;
        private bool ownsBroker = true;

        /// <summary>
        /// Initializes a new instance of the ServiceHubProvider class
        /// </summary>
        /// <param name="webHost">the web-host initializer for this serviceHub</param>
        public GrpcInit(IWebHostStartup webHost, IServiceHubProvider hubProvider)
        {
            this.hubProvider = hubProvider;
            webHost.RegisterConfigurator(this);
        }

        public GrpcInit(IWebHostStartup webHost, IServiceHubProvider hubProvider, bool configureKestrel) : this(webHost, hubProvider)
        {
            this.configureKestrel = configureKestrel;
        }

        public void ConfigureBuilder(WebApplicationBuilder builder)
        {
            builder.Services.AddSingleton(hubProvider);
            if (configureKestrel)
            {
                builder.WebHost.ConfigureKestrel(k =>
                {
                    k.ConfigureEndpointDefaults(li => li.Protocols = HttpProtocols.Http2);
                });
            }

            builder.Services.AddGrpc(opt =>
            {
                opt.EnableDetailedErrors = true;
                opt.MaxReceiveMessageSize = null;
                opt.MaxSendMessageSize = null;
            });
        }

        public void ConfigureApp(WebApplication app)
        {
        }
    }
}
