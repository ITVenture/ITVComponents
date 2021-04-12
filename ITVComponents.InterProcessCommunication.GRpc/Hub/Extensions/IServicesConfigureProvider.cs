using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions
{
    public interface IServicesConfigureProvider : IPlugin
    {
        /// <summary>
        /// Configures the services for a specific use-case
        /// </summary>
        /// <param name="services">the service-collection that is used to inject dependencies</param>
        void ConfigureServices(IServiceCollection services);
    }
}
