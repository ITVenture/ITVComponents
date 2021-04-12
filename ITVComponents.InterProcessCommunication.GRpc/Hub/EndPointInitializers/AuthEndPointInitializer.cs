using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.EndPointInitializers
{
    public class AuthEndPointInitializer:IEndPointInitializer
    {
        /// <summary>
        /// Initializes a new instance of the OpenEndpointInitializer class
        /// </summary>
        /// <param name="parent"></param>
        public AuthEndPointInitializer(IServiceHubProvider parent)
        {
            parent.RegisterEndPointInitializer(this);
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Maps the gdrp-endpoints to the endpoint route builder that configures the current http host
        /// </summary>
        /// <param name="endpointRouteBuilder">the current http-endpoint builder</param>
        public void MapEndPoints(IEndpointRouteBuilder endpointRouteBuilder)
        {
            endpointRouteBuilder.MapGrpcService<AuthServiceHubRpc>();
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }
        
        /// <summary>
        /// raises the disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
