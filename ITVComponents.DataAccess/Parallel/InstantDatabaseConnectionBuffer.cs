using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Threading;

namespace ITVComponents.DataAccess.Parallel
{
    public class InstantDatabaseConnectionBuffer:IConnectionBuffer
    {
        /// <summary>
        /// the constructor string used to generate new connections
        /// </summary>
        private string databaseConstructor;

        /// <summary>
        /// the Plugin factory used to generate the connection plugins
        /// </summary>
        private PluginFactory factory;

        /// <summary>
        /// indicates whether this instance has been disposed
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Initializes a new instance of the InstantDatabaseConnectionBuffer class
        /// </summary>
        /// <param name="factory">the factory that is used to create new connections</param>
        /// <param name="connectionString">the connectionstring that is used to construct a new connection</param>
        public InstantDatabaseConnectionBuffer(PluginFactory factory, string connectionString)
        {
            this.factory = factory;
            this.databaseConstructor = connectionString;
        }

        /// <summary>
        /// Gets an available Database Connection from the pool of available connections
        /// </summary>
        /// <param name="useTransaction">indicates whether to begin a transaction for the returned database connection</param>
        /// <param name="database">the database that is available for usage</param>
        /// <returns>a connection that is unused at the moment</returns>
        public IResourceLock AcquireConnection(bool useTransaction, out IDbWrapper database)
        {
            while (!disposed)
            {
                try
                {
                    database = factory.LoadPlugin<IDbWrapper>("dummy", databaseConstructor, false);
                    ITransaction trans = null;
                    if (useTransaction)
                    {
                        trans = database.AcquireTransaction();
                    }

                    OnConnectionAcquiring(database);
                    return new ResourceDisposer(database, trans);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(ex.Message,LogSeverity.Error, "DataAccess");
                }
            }

            database = null;
            return null;
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            disposed = true;
            OnDisposed();
        }

        /// <summary>
        /// Raises the OnConnectionAcquiring Event
        /// </summary>
        /// <param name="obj">the new created database wrapper</param>
        protected virtual void OnConnectionAcquiring(IDbWrapper obj)
        {
            ConnectionAcquiring?.Invoke(obj);
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Enables a client object to configure the acquired connection before it is returned
        /// </summary>
        public event Action<IDbWrapper> ConnectionAcquiring;

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
