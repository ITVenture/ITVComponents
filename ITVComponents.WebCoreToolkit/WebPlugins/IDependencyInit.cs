using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.WebPlugins
{
    /// <summary>
    /// Enables a Plugin to perform Initializeations of the DependencyInjection
    /// </summary>
    public interface IDependencyInit
    {
        /// <summary>
        /// Initializes a service on the target service-collection
        /// </summary>
        /// <param name="services">the service-provider configuring Dependencyinjection</param>
        void Initialize(IServiceProvider services);
    }
}
