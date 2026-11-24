using ITVComponents.Json.Contracts;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    internal static class ContractRegistration
    {
        public static void RegisterContracts()
        {
            // The transport types of the protocol itself: a server that only reads registered argument types must still
            // read the wrappers a proxy puts around every argument. They keep their type names on the wire.
            ManualTypeRegistry.Allow(typeof(TypedParam));
            ManualTypeRegistry.Allow(typeof(ResolvableArray<>));
            ManualTypeRegistry.Allow(typeof(TypeDescriptor));
            ManualTypeRegistry.Allow(typeof(Proxying.ProxyResult));
        }
    }
}
