using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.Threading;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo
{
    /*public class ContextBuffer:IContextBuffer
    {
        /// <summary>
        /// The constructor string that will be used to initialize the context
        /// </summary>
        private readonly string contextConstructor;

        /// <summary>
        /// the PluginFactory that will load the data-context
        /// </summary>
        private readonly IPluginFactory factory;

        /// <summary>
        /// Initializes a new instance of the ContextBuffer class
        /// </summary>
        /// <param name="contextConstructor">the constructor-string that will load the EF-Context as a plugin</param>
        /// <param name="factory">the plugin-factory that will load the context</param>
        public ContextBuffer(string contextConstructor, IPluginFactory factory)
        {
            this.contextConstructor = contextConstructor;
            this.factory = factory;
        }

        /// <summary>
        /// Acquires a connection using the desired Context-Plugin
        /// </summary>
        /// <typeparam name="TContext">the context - type that is used to access the database</typeparam>
        /// <param name="context">holds the context that was loaded using the pluginFactory</param>
        /// <returns>a resource-lock that will dispose the context after using</returns>
        public IResourceLock AcquireContext<TContext>(out TContext context) where TContext : DbContext
        {
            var tmp = factory.LoadPlugin<TContext>("dummy", contextConstructor, false);
            context = tmp ;
            return new ResourceDisposer(context);
        }
    }*/
}
