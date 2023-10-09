using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ITVComponents.DataAccess.Resources;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Threading;

namespace ITVComponents.DataAccess.Parallel
{
    /*/// <summary>
    /// Buffers Connections and avoids multiple usage of connections
    /// </summary>
    public class DatabaseConnectionBuffer: IConnectionBuffer
    {
        /// <summary>
        /// A List of connections that is handled by this buffer
        /// </summary>
        private List<DatabaseContainer> connections;

        /// <summary>
        /// the constructor string used to generate new connections
        /// </summary>
        private string databaseConstructor;

        /// <summary>
        /// the Plugin factory used to generate the connection plugins
        /// </summary>
        private IPluginFactory factory;

        /// <summary>
        /// indicates whether this buffer is currently being disposed
        /// </summary>
        private bool disposing = false;

        /// <summary>
        /// Counts the number of current active connections
        /// </summary>
        private int activeConnectionCount;

        /// <summary>
        /// Avoids Threading problems when increasing or decreasing the number of active connections
        /// </summary>
        private object activeConnectionLock;

        /// <summary>
        /// timer object used to monitor the usage of the initialized database connections
        /// </summary>
        private Timer timer;

        /// <summary>
        /// provides a value indicating after how much time a connection is considered abandoned
        /// </summary>
        private const int AbandonTimeoutMinutes = 10;

        /// <summary>
        /// Initializes a new instance of the DatabaseConnectionBuffer class
        /// </summary>
        /// <param name="factory">the plugin factory used to generate database connections</param>
        /// <param name="connectionString">the connection string used to initialize database connections</param>
        public DatabaseConnectionBuffer(IPluginFactory factory, string connectionString)
            : this()
        {
            this.factory = factory;
            this.databaseConstructor = connectionString;
        }

        /// <summary>
        /// Prevents a default instance of the DatabaseConnectionBuffer class from being created
        /// </summary>
        private DatabaseConnectionBuffer()
        {
            connections = new List<DatabaseContainer>();
            activeConnectionLock = new object();
            timer = new Timer(MonitorConnections, string.Format("::{0}::", GetHashCode()), 0, 600000);
        }

        /// <summary>
        /// Gets an available Database Connection from the pool of available connections
        /// </summary>
        /// <param name="useTransaction">indicates whether to begin a transaction for the returned database connection</param>
        /// <param name="database">the database that is available for usage</param>
        /// <returns>a connection that is unused at the moment</returns>
        public IResourceLock AcquireConnection(bool useTransaction, out IDbWrapper database)
        {
            string threadId = Thread.CurrentThread.LocalOwner();

            DatabaseContainer db;
            IResourceLock inner = null;
            while (!disposing)
            {
                try
                {
                    lock (connections)
                    {
                        var tmp =
                            (from t in connections
                                    where !t.InUse && !t.Disposed && t.ThreadId == threadId
                                    select t).FirstOrDefault
                                ();
                        if (tmp != null)
                        {
                            tmp.InUse = true;
                            tmp.LastUsage = DateTime.Now;
                        }

                        db = tmp;
                    }

                    if (db == null)
                    {
                        db = new DatabaseContainer
                        {
                            Database = factory.LoadPlugin<IDbWrapper>(DataAccessResources.ParallelDummyInstanceName,
                                databaseConstructor,
                                false),
                            LastUsage = DateTime.Now,
                            InUse = true,
                            ThreadId = threadId
                        };
                        lock (connections)
                        {
                            connections.Add(db);
                        }
                    }

                    if (useTransaction)
                    {
                        inner = db.Database.AcquireTransaction();
                    }



                    database = db.Database;
                    lock (activeConnectionLock)
                    {
                        activeConnectionCount++;
                    }

                    OnConnectionAcquiring(database);
                    return new DataBufferResourceLock(this, db, inner);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(ex.Message, LogSeverity.Error, "DataAccess");
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
            if (!disposing)
            {
                disposing = true;
                timer.Dispose();
                while (activeConnectionCount > 0)
                {
                    lock (activeConnectionLock)
                    {
                        Monitor.Wait(activeConnectionLock, 2000);
                    }
                }

                lock (connections)
                {
                    foreach (DatabaseContainer val in connections)
                    {
                        val.Database.Dispose();
                        val.Disposed = true;
                    }

                    connections.Clear();
                }
            }
        }

        /// <summary>
        /// Decreases the number of active used connections
        /// </summary>
        internal void DecreaseActiveConnectionCount(DatabaseContainer database)
        {
            lock (activeConnectionLock)
            {
                activeConnectionCount--;
                if (activeConnectionCount < 0)
                {
                    throw new Exception(DataAccessResources.ParallelMultithreadingMalfunctionMessage);
                }

                lock (connections)
                {
                    database.InUse = false;
                }

                Monitor.Pulse(activeConnectionLock);
            }
        }

        /// <summary>
        /// Raises the ConnectionAcquiring event
        /// </summary>
        /// <param name="obj">the obejct that is being acquired</param>
        protected virtual void OnConnectionAcquiring(IDbWrapper obj)
        {
            ConnectionAcquiring?.Invoke(obj);
        }

        /// <summary>
        /// Monitors the active connections and disposeds abandoned ones
        /// </summary>
        /// <param name="state">unused argument</param>
        private void MonitorConnections(object state)
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
            Thread.CurrentThread.Name = state.ToString();
            try
            {
                List<DatabaseContainer> abandonedConnections = new List<DatabaseContainer>();
                DateTime now = DateTime.Now;
                lock (connections)
                {
                    LogEnvironment.LogDebugEvent(null, string.Format("Current Connectioncount: {0}", connections.Count), (int) LogSeverity.Report, "DataAccess");
                    foreach (DatabaseContainer item in connections)
                    {
                        if (!item.InUse &&
                            now.Subtract(item.LastUsage).TotalMinutes > AbandonTimeoutMinutes)
                        {
                            abandonedConnections.Add(item);
                        }
                    }

                    foreach (DatabaseContainer item in abandonedConnections)
                    {
                        item.Database.Dispose();
                        item.Disposed = true;
                        connections.Remove(item);
                        LogEnvironment.LogDebugEvent(null, DataAccessResources.ParallelAbandonMessage, (int) LogSeverity.Report, "DataAccess");
                    }

                    LogEnvironment.LogDebugEvent(null, string.Format("New Connectioncount: {0}", connections.Count), (int) LogSeverity.Report, "DataAccess");
                }
            }
            finally
            {
                timer.Change(600000, 600000);
                
            }
        }

        /// <summary>
        /// Enables a client object to configure the acquired connection before it is returned
        /// </summary>
        public event Action<IDbWrapper> ConnectionAcquiring;
    }*/
}
