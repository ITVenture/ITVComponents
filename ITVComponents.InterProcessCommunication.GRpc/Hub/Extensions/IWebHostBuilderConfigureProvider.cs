using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using Microsoft.AspNetCore.Hosting;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions
{
    public interface IWebHostBuilderConfigureProvider:IPlugin
    {
        /// <summary>
        /// Configures the HostBuilder before it is being created
        /// </summary>
        /// <param name="webBuilder">the web-builder instance that is used to create a server end-point</param>
        void Configure(IWebHostBuilder webBuilder);
    }
}
