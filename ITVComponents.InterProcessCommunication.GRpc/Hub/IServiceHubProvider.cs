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
    public interface IServiceHubProvider : ITVComponents.InterProcessCommunication.MessagingShared.Hub.IServiceHubProvider
    {
        /// <summary>
        /// Gets the WebApp-Builder that enables dependent modules to register extension for Security or custom dependencies
        /// </summary>
        WebApplicationBuilder WebAppBuilder { get; }


        void RegisterConfigurator(IServiceHubConfigurator configurator);
    }
}
