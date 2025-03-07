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
        }
    }
}
