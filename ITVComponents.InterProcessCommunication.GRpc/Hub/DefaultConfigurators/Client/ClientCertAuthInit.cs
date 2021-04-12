using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Client
{
    public class ClientCertAuthInit:CollectableClientInit
    {
        /// <summary>
        /// Configures a channel before a grpc-client is created
        /// </summary>
        /// <param name="options">the channel-options to configure</param>
        public override void ConfigureChannel(GrpcChannelOptions options)
        {
            // das isch im fall absichtlech nonig implementiert ond liit ned amne kafi-interrupt. bruuchts momentan nid!
            options.Credentials = new SslCredentials();
        }
    }
}
