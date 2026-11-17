#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using ITVComponents.Logging;

namespace ITVComponents.Plugins.Modules
{
    /// <summary>
    /// The load context of one module: it loads the module's own dependencies in the versions the module brings - even if
    /// another module brings the same assembly in another version - and shares the contracts with the host.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>A contract (<see cref="PluginContract"/>) the host can load comes from the default context, never from the
    /// module: otherwise a type of the module would not be the type of the host (<c>InvalidCastException</c>).</item>
    /// <item>Everything else is resolved from the module's <c>.deps.json</c> (<see cref="AssemblyDependencyResolver"/>);
    /// what the module does not bring (the framework) comes from the default context.</item>
    /// <item>Native libraries are loaded by full path from the module folder. Their own dependencies Windows resolves by
    /// name - two modules with equally named native libraries in different versions can still disturb each other.</item>
    /// <item>The context is not collectible: modules take effect with the next start of the host.</item>
    /// </list>
    /// </remarks>
    public sealed class ModuleLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver resolver;

        /// <summary>
        /// Initializes the context of a module.
        /// </summary>
        /// <param name="moduleId">the id of the module, for the log</param>
        /// <param name="mainAssemblyPath">the module's main assembly; its <c>.deps.json</c> describes the dependencies</param>
        public ModuleLoadContext(string moduleId, string mainAssemblyPath)
            : base($"module:{moduleId}", isCollectible: false)
        {
            ModuleId = moduleId;
            MainAssemblyPath = Path.GetFullPath(mainAssemblyPath);
            resolver = new AssemblyDependencyResolver(MainAssemblyPath);
        }

        /// <summary>
        /// Gets the id of the module.
        /// </summary>
        public string ModuleId { get; }

        /// <summary>
        /// Gets the main assembly of the module.
        /// </summary>
        public string MainAssemblyPath { get; }

        /// <inheritdoc />
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (IsSharedContract(assemblyName))
            {
                return null;
            }

            var path = resolver.ResolveAssemblyToPath(assemblyName);
            if (path == null)
            {
                // Without .deps.json the resolver knows nothing; a file next to the main assembly still belongs to the module.
                var beside = Path.Combine(Path.GetDirectoryName(MainAssemblyPath)!, assemblyName.Name + ".dll");
                if (!File.Exists(beside))
                {
                    // Not part of the module (framework, or an assembly only the host has): the default context decides.
                    return null;
                }

                path = beside;
            }

            LogEnvironment.LogDebugEvent($"Module {ModuleId} loads {assemblyName.Name} {assemblyName.Version} from {path}.", LogSeverity.Report);
            return LoadFromAssemblyPath(path);
        }

        /// <inheritdoc />
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path == null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
        }

        /// <summary>
        /// A contract the host has (loaded already, or next to the host) is always the host's.
        /// </summary>
        private static bool IsSharedContract(AssemblyName assemblyName)
        {
            var loaded = Default.Assemblies.FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
            if (loaded != null)
            {
                return string.IsNullOrEmpty(loaded.Location) || PluginContract.IsContract(loaded.Location);
            }

            var besideHost = Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll");
            return PluginContract.IsContract(besideHost);
        }
    }
}
