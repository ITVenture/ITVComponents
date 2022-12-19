using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Client
{
    public class SelfSignedServerCertificateInit: CollectableClientInit
    {
        public SelfSignedServerCertificateInit() : base()
        {
        }

        public SelfSignedServerCertificateInit(CollectedClientInit parent):base(parent)
        {
        }

        /// <summary>
        /// Configures a channel before a grpc-client is created
        /// </summary>
        /// <param name="options">the channel-options to configure</param>
        public override void ConfigureChannel(GrpcChannelOptions options)
        {
            options.HttpClient = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        }
    }
}
