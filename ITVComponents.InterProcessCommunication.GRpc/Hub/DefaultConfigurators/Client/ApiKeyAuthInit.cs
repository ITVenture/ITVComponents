using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Grpc.Net.Client;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Client
{
    public class ApiKeyAuthInit:CollectableClientInit
    {
        private readonly string apiKey;

        public ApiKeyAuthInit(string apiKey, CollectedClientInit parent) : base(parent)
        {
            this.apiKey = apiKey;
        }

        public ApiKeyAuthInit(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public override void ConfigureChannel(GrpcChannelOptions options)
        {
            options.HttpClient = new HttpClient();
            options.HttpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            options.DisposeHttpClient = true;
        }
    }
}
