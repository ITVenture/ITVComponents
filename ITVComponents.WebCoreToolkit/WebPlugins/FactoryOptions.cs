using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.WebPlugins
{
    /// <summary>
    /// Enables external objects ot configure a created PluginFactory with further fixed dependencies
    /// </summary>
    public class FactoryOptions
    {
        private Dictionary<string, Func<IServiceProvider,object>> dependencies = new Dictionary<string, Func<IServiceProvider, object>>();

        private readonly HashSet<string> scopeOwnedDependencies = new HashSet<string>();

        /// <summary>
        /// Adds a dependency that must be accessible from the pluginfactory as a parameter
        /// </summary>
        /// <param name="name">the name of the dependency</param>
        /// <param name="dependency">the injected value of the dependency</param>
        /// <param name="disposeWithScope">
        /// when true, the resolved value is treated as a per-operation resource: it is created freshly for a
        /// plugin operation-scope and disposed (if <see cref="IDisposable"/>) when that scope closes. Use this for
        /// e.g. a per-operation DbContext (delegate returns <c>factory.CreateDbContext()</c>). Default (false) =
        /// the value is resolved as-is and out-lives the scope (DI-/host-owned), preserving the historic behaviour.
        /// </param>
        public void AddDependency(string name, Func<IServiceProvider, object> dependency, bool disposeWithScope = false)
        {
            dependencies.Add(name, dependency);
            if (disposeWithScope)
            {
                scopeOwnedDependencies.Add(name);
            }
        }

        /// <summary>
        /// Names of dependencies that are per-operation resources (created fresh + disposed with a plugin
        /// operation-scope). See <see cref="AddDependency(string, Func{IServiceProvider, object}, bool)"/>.
        /// </summary>
        public IReadOnlyCollection<string> ScopeOwnedDependencies => scopeOwnedDependencies;

        /// <summary>True when <paramref name="name"/> is a per-operation, scope-owned dependency.</summary>
        public bool IsScopeOwned(string name) => scopeOwnedDependencies.Contains(name);

        /// <summary>
        /// Configures the target factory with the provided parameters
        /// </summary>
        /// <param name="name">the configured name of the required dependency</param>
        /// <param name="services">the services collection that enables the pluginFactory to get a specific dependency</param>
        internal object GetDependency(string name, IServiceProvider services)
        {
            if (dependencies.ContainsKey(name))
            {
                return dependencies[name](services);
            }

            return null;
        }
    }
}
