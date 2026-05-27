using ITVComponents.Plugins;
using Microsoft.AspNetCore.Routing;

namespace ITVComponents.GenericService.WebService.Configurators
{
    public interface IEndPointInitializer:IPlugin
    {
        /// <summary>
        /// Maps the gdrp-endpoints to the endpoint route builder that configures the current http host
        /// </summary>
        /// <param name="endpointRouteBuilder">the current http-endpoint builder</param>
        void MapEndPoints(IEndpointRouteBuilder endpointRouteBuilder);
    }
}
