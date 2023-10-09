using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Parallel;
using ITVComponents.DataExchange.Interfaces;
using ITVComponents.Plugins;
using ITVComponents.Threading;

namespace ITVComponents.DataExchange
{
    public class ConnectionMapper:IConnectionMapper, IDeferredInit
    {
        /// <summary>
        /// the connector that will provide dataconnections
        /// </summary>
        private IPluginFactory factory;

        private readonly string sourceName;

        /// <summary>
        /// the collector that is allowed to use this connection mapper
        /// </summary>
        private IDataCollector collector;

        private readonly string name;

        /// <summary>
        /// Initializes a new instance of the ConnectionMapper class
        /// </summary>
        /// <param name="source">the database Source that provides access to the database</param>
        /// <param name="target">the target collection that will consume the connected source</param>
        public ConnectionMapper(IPluginFactory factory, string sourceName, IDataCollector target, string uniqueName)
        {
            this.factory= factory;
            this.sourceName = sourceName;
            this.collector = target;
            this.name = uniqueName;
        }

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
        public IDbWrapper AcquireDatabase()
        {
            return (IDbWrapper)factory[sourceName];
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
                    collector.RegisterSource(name, this);
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
            collector.UnregisterSource(name, this);
        }

        /// <summary>
        /// Runs Initializations on derived objects
        /// </summary>
        protected virtual void Init()
        {
        }
    }
}
