using ITVComponents.Plugins;
using Microsoft.AspNetCore.Hosting;

namespace ITVComponents.GenericService.WebService.Configurators
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
