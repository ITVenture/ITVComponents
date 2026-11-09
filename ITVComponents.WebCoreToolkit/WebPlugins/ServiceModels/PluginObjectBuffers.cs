using System;
using System.Collections.Concurrent;
using ITVComponents.DIServices;

namespace ITVComponents.WebCoreToolkit.WebPlugins.ServiceModels
{
    /// <summary>
    /// Holds the plugin buffers (one per permission scope) when the host has no <c>IMemoryCache</c>. Registered as
    /// a singleton by <c>InitializePluginSystem</c>, so the buffers live exactly as long as the DI container, like a
    /// memory cache would. A static store would let two containers in one process (integration tests, several
    /// hosts) see each other's plugin definitions under the same scope name.
    /// </summary>
    internal sealed class PluginObjectBuffers
    {
        private readonly ConcurrentDictionary<string, IObjectProvider> buffers =
            new ConcurrentDictionary<string, IObjectProvider>(StringComparer.Ordinal);

        public IObjectProvider GetOrAdd(string objectName, Func<IObjectProvider> create)
            => buffers.GetOrAdd(objectName, _ => create());
    }
}
