using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions
{
    public interface IAppConfigureProvider : IPlugin
    {
        /// <summary>
        /// Configures the app-builder / host-environment before the services are configured
        /// </summary>
        /// <param name="app">the app-builder</param>
        /// <param name="env">the hosting environment</param>
        void Configure(IApplicationBuilder app, IWebHostEnvironment env);
    }
}
