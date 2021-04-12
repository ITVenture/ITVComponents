using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Net.Client;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Client
{
    public class MessageSizeConfigInit:CollectableClientInit
    {
        private int? maxReceiveSize;

        private int? maxSendSize;
        public MessageSizeConfigInit()
        {
        }

        public MessageSizeConfigInit(int maxReceiveSize)
        {
            this.maxReceiveSize = maxReceiveSize;
        }
        
        public MessageSizeConfigInit(int maxReceiveSize, int maxSendSize)
        {
            this.maxReceiveSize = maxReceiveSize;
            this.maxSendSize = maxSendSize;
        }
        
        public MessageSizeConfigInit(CollectedClientInit parent):base(parent)
        {
        }

        public MessageSizeConfigInit(int maxReceiveSize, CollectedClientInit parent):base(parent)
        {
            this.maxReceiveSize = maxReceiveSize;
        }
        
        public MessageSizeConfigInit(int maxReceiveSize, int maxSendSize, CollectedClientInit parent):base(parent)
        {
            this.maxReceiveSize = maxReceiveSize;
            this.maxSendSize = maxSendSize;
        }
        public override void ConfigureChannel(GrpcChannelOptions options)
        {
            options.MaxReceiveMessageSize = maxReceiveSize;
            options.MaxSendMessageSize = maxSendSize;
        }
    }
}
