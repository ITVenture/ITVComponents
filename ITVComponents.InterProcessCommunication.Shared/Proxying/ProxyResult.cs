using System;
using System.Runtime.Serialization;
using ITVComponents.InterProcessCommunication.Shared.Helpers;

namespace ITVComponents.InterProcessCommunication.Shared.Proxying
{
    public class ProxyResult
    {
        public TypeDescriptor Type { get; set; }

        public string UniqueName { get; set; }
    }
}
