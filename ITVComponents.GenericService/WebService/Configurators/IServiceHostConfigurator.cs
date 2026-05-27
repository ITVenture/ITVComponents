using Microsoft.AspNetCore.Builder;

namespace ITVComponents.GenericService.WebService.Configurators
{
    public interface IServiceHostConfigurator
    {
        /// <summary>
        /// Configures the WebApplication builder (inject services, set defaults, etc.)
        /// </summary>
        /// <param name="builder">the web-application builder that is used to setup a grpc service</param>
        void ConfigureBuilder(WebApplicationBuilder builder);

        /// <summary>
        /// Configures the app after it is built. (e.g. build the service middleware pipeline
        /// </summary>
        /// <param name="app">the built app</param>
        void ConfigureApp(WebApplication app);
    }
}
