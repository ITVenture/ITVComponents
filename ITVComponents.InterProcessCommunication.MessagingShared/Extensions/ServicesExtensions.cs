using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.MessagingShared.DependencyExtensions;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Extensions
{
    public static class ServicesExtensions
    {
        /// <summary>
        /// Registers the default Client factory to the given service-collection
        /// </summary>
        /// <param name="services">the service-collection that is used to register the ClientFactory</param>
        /// <returns>the service-collection that was provided as argument</returns>
        public static IServiceCollection SetupClientFactory(this IServiceCollection services)
        {
            return services.AddScoped<IClientFactory, DefaultClientFactory>();
        }
    }
}
