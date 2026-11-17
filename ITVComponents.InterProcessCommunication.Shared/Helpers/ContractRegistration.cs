using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Json.Contracts;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    internal static class ContractRegistration
    {
        public static void RegisterContracts()
        {
            DynamicContractResolver.ConfigureType(typeof(IManualSerializer), typeof(TypedParam), "IPC_TypedParam");
            DynamicContractResolver.ConfigureType(typeof(IManualSerializer), typeof(TypeDescriptor), "IPC_TypeDescriptor");

            // The transport types of the protocol itself: a server that only reads registered argument types must still
            // read the wrappers a proxy puts around every argument. They keep their full names on the wire.
            ManualTypeRegistry.Allow(typeof(TypedParam));
            ManualTypeRegistry.Allow(typeof(TypeDescriptor));
            ManualTypeRegistry.Allow(typeof(Proxying.ProxyResult));
        }
    }
}
