﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Grpc.Messages
{
    public class AbandonExtendedProxyRequestMessage:AuthenticatedRequestMessage
    {
        public string ObjectName { get; set; }
    }
}
