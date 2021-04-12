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
    public class ContextBuffer:IContextBuffer
    {
        /// <summary>
        /// The constructor string that will be used to initialize the context
        /// </summary>
        private readonly string contextConstructor;

        /// <summary>
        /// the PluginFactory that will load the data-context
        /// </summary>
        private readonly PluginFactory factory;

        /// <summary>
        /// Initializes a new instance of the ContextBuffer class
        /// </summary>
        /// <param name="contextConstructor">the constructor-string that will load the EF-Context as a plugin</param>
        /// <param name="factory">the plugin-factory that will load the context</param>
        public ContextBuffer(string contextConstructor, PluginFactory factory)
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
            IPlugin tmp = factory.LoadPlugin<IPlugin>("dummy", contextConstructor, false);
            context = (TContext)tmp ;
            return new ResourceDisposer(context);
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
