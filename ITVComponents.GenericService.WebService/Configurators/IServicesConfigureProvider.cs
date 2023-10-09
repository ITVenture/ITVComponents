using ITVComponents.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.GenericService.WebService.Configurators
{
    public interface IServicesConfigureProvider 
    {
        /// <summary>
        /// Configures the services for a specific use-case
        /// </summary>
        /// <param name="services">the service-collection that is used to inject dependencies</param>
        void ConfigureServices(IServiceCollection services);
    }
}
