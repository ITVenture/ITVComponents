using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using ITVComponents.DataAccess.Extensions;

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

        public override CallOptions ConfigureCallOptions(CallOptions optionsRaw)
        {

            var retVal = base.ConfigureCallOptions(optionsRaw);
            var ent = new Metadata.Entry("X-Api-Key", apiKey);
            if (retVal.Headers == null)
            {
                retVal = retVal.WithHeaders(new Metadata { ent });
            }
            else
            {
                retVal.Headers.Add(ent);
            }

            return retVal;
        }
    }
}
