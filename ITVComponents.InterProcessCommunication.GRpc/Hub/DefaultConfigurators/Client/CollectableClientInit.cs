using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Net.Client;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Client
{
    /// <summary>
    /// A Client-Initializer that can be used in a multi-initializers-scenario
    /// </summary>
    public abstract class CollectableClientInit:IHubClientConfigurator
    {
        /// <summary>
        /// Initializes a new instance of the CollectableClientInit class without using a parent
        /// </summary>
        protected CollectableClientInit()
        {
        }

        /// <summary>
        /// Initializes a new instance of the CollectableClientInit class with using a parent
        /// </summary>
        /// <param name="parent">the parent instance that invokes this initializer together with others</param>
        protected CollectableClientInit(CollectedClientInit parent)
        {
            parent.RegisterConfigurator(this);
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Configures a channel before a grpc-client is created
        /// </summary>
        /// <param name="options">the channel-options to configure</param>
        public abstract void ConfigureChannel(GrpcChannelOptions options);

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            Dispose(true);
            OnDisposed();
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Raises the Disposed event
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
