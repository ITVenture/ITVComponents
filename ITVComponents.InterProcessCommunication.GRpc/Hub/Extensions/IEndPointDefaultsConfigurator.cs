using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions
{
    /// <summary>
    /// Configures Default Endpoint-Listener options
    /// </summary>
    public interface IEndPointDefaultsConfigurator
    {
        /// <summary>
        /// Configures the ListenOptions for the Kestrel-Server
        /// </summary>
        /// <param name="options">the default-endpoint-listener options</param>
        void ConfigureEndPointDefaults(ListenOptions options);
    }
}
