using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Net.Client;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub
{
    /// <summary>
    /// Enables an implemeting class to configure a client-channel before gprc-clients are created
    /// </summary>
    public interface IHubClientConfigurator: IPlugin
    {
        /// <summary>
        /// Configures a channel before a grpc-client is created
        /// </summary>
        /// <param name="options">the channel-options to configure</param>
        void ConfigureChannel(GrpcChannelOptions options);
    }
}
