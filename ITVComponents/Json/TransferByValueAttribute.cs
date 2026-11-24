using System;

namespace ITVComponents.Json
{
    /// <summary>
    /// Marks a type whose objects always travel by value: when a remote method returns one through an interface-typed
    /// return value or out-parameter, the inter-process communication serializes it instead of handing out a proxy to it.
    /// </summary>
    /// <remarks>
    /// Meant for data carriers that are declared through an interface (e.g. an <c>IDbDataParameter</c>) but have no
    /// identity on the server worth keeping. Inherited by derived types.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class TransferByValueAttribute : Attribute
    {
    }
}
