using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Parallel;
using ITVComponents.DataExchange.Interfaces;
using ITVComponents.Plugins;
using ITVComponents.Plugins.SelfRegistration;
using ITVComponents.Threading;

namespace ITVComponents.DataExchange
{
    public class ConnectionMapper:IConnectionMapper, IDeferredInit
    {
        /// <summary>
        /// the connector that will provide dataconnections
        /// </summary>
        private IConnectionBuffer connector;

        /// <summary>
        /// the collector that is allowed to use this connection mapper
        /// </summary>
        private IDataCollector collector;

        /// <summary>
        /// Initializes a new instance of the ConnectionMapper class
        /// </summary>
        /// <param name="source">the database Source that provides access to the database</param>
        /// <param name="target">the target collection that will consume the connected source</param>
        public ConnectionMapper(IConnectionBuffer source, IDataCollector target)
        {
            this.connector = source;
            this.collector = target;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization => false;

        /// <summary>
        /// Gets a Database connection and opens a transaction is requested
        /// </summary>
        /// <param name="useTransaction">indicates whether to open a new transaction on the returned connection</param>
        /// <param name="database">the database connection</param>
        /// <returns>a ResourceLock - Object that enables the caller to free the Database implicitly after using</returns>
        public IResourceLock AcquireDatabase(bool useTransaction, out IDbWrapper database)
        {
            return connector.AcquireConnection(useTransaction, out database);
        }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            if (!Initialized)
            {
                try
                {
                    collector.RegisterSource(UniqueName, this);
                    Init();
                }
                finally
                {
                    Initialized = true;
                }
            }
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            collector.UnregisterSource(UniqueName, this);
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Runs Initializations on derived objects
        /// </summary>
        protected virtual void Init()
        {
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
