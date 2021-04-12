using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using ITVComponents.DataAccess.FileOutput;

namespace ITVComponents.DataAccess
{
    public class RecordBuffer : IDisposable
    {
        /// <summary>
        /// The saver thread that dumps data coming from the database or generated while being offline
        /// </summary>
        private Thread saver;

        /// <summary>
        /// Object used to synchronize the usage of offline and online data
        /// </summary>
        private object updateLocker;

        /// <summary>
        /// Object used to synchronize the usage of the DataResources
        /// </summary>
        private object resourceLocker;

        /// <summary>
        /// A Dictionary that is used to buffer offline resultsets 
        /// </summary>
        private Dictionary<string, List<DynamicResult>> offlineResultsets;

        /// <summary>
        /// a Dictionary that is used to merge table names to their containing files
        /// </summary>
        private Dictionary<string, string> fileNames;

        /// <summary>
        /// a dictionary that stores the online results that are retreived periodically from
        /// the database and dumps them to their containing files
        /// </summary>
        private Dictionary<string, DynamicResult[]> onlineResults;

        /// <summary>
        /// a list of online queries used to dump online data in periodic intervals to offline files
        /// </summary>
        private Dictionary<string, string> queries;

        /// <summary>
        /// a value indicating whether to keep the thread running
        /// </summary>
        private bool keepRunning = true;

        /// <summary>
        /// indicates whether this buffer instance is currently in the initial status
        /// </summary>
        private bool firstRun = true;

        /// <summary>
        /// the minimum timeout between single updates of the filedumps
        /// </summary>
        private int updateTimeout = 5000;

        /// <summary>
        /// a list of queryParameters used to dump the online data
        /// </summary>
        private Dictionary<string, Dictionary<string, object>> queryParameters;

        /// <summary>
        /// The database link used to read and write data
        /// </summary>
        private IDbWrapper database;

        /// <summary>
        /// a Strategy used to dump data to a file
        /// </summary>
        private IDataDumper dumpStrategy;

        /// <summary>
        /// Initializes a new instance of the RecordBuffer class
        /// </summary>
        /// <param name="database">the database in which the buffered data is originally located</param>
        /// <param name="dumperStrategy">the Strategy used to dump and load data from the filesystem</param>
        public RecordBuffer(IDbWrapper database, IDataDumper dumperStrategy)
            : this()
        {
            this.database = database;
            this.dumpStrategy = dumperStrategy;
        }

        /// <summary>
        /// Gets the offline set for a specific table name
        /// </summary>
        /// <param name="name">the table name for which to get the offline-values</param>
        /// <returns>the result set that is bound to the specified name</returns>
        public DynamicResult[] this[string name]
        {
            get { return onlineResults[name]; }
        }

        /// <summary>
        /// Avoids strange thread effects when using the database and provides a possibility to dump offlinedata in offline mode
        /// </summary>
        /// <returns></returns>
        public IDisposable AcquireDataLock()
        {
            return new DataLocker(resourceLocker, updateLocker, database == null);
        }

        /// <summary>
        /// Signals the worker thread the the initialization of the Buffer is done
        /// </summary>
        public void InitializationDone()
        {
            lock (updateLocker)
            {
                Monitor.Pulse(updateLocker);
                Monitor.Wait(updateLocker);
            }
        }

        /// <summary>
        /// Registers an offlinefile that stores 
        /// </summary>
        /// <param name="name">the tablename of the registered offlinefile</param>
        /// <param name="fileName">the filename in which the content of the offlinetable is stored</param>
        /// <returns>the list in which to fill the data</returns>
        public List<DynamicResult> RegisterOfflineFile(string name, string fileName)
        {
            List<DynamicResult> itemTemplate = new List<DynamicResult>();
            offlineResultsets.Add(name, itemTemplate);
            fileNames.Add(name, fileName);
            return itemTemplate;
        }

        /// <summary>
        /// Registers an online-actualized offline datasource 
        /// </summary>
        /// <param name="name">the name of the offline-DataSource</param>
        /// <param name="fileName">the fileName in which the data is stored</param>
        /// <param name="query">the query that will provide the required data</param>
        /// <param name="arguments">a list of arguments used to execute the queries</param>
        public void RegisterOnlineQuery(string name, string fileName, string query, Dictionary<string, object> arguments)
        {
            this.fileNames.Add(name, fileName);
            this.queries.Add(name, query);
            this.queryParameters.Add(name, arguments ?? new Dictionary<string, object>());
        }

        /// <summary>
        /// Queries items from a given list and returns the result
        /// </summary>
        /// <param name="source">the list containing all items</param>
        /// <param name="query">the query that should validate the </param>
        /// <returns>the queried result</returns>
        public dynamic[] QueryItems(string source, Where query)
        {
            using (AcquireDataLock())
            {
                if (onlineResults.ContainsKey(source))
                {
                    return (from t in onlineResults[source] where query.Validate(t) select t).ToArray();
                }
                else
                {
                    return (from t in offlineResultsets[source] where query.Validate(t) select t).ToArray();
                }
            }
        }

        /// <summary>
        /// Raises the ImportOfflineData event
        /// </summary>
        /// <param name="e">the event arguments</param>
        protected virtual void OnImportOfflineData(CancelEventArgs e)
        {
            if (ImportOfflineData != null)
            {
                ImportOfflineData(this, e);
            }
        }

        /// <summary>
        /// Prevents a default instance of the RecordBuffer class from being created
        /// </summary>
        private RecordBuffer()
        {
            onlineResults = new Dictionary<string, DynamicResult[]>();
            offlineResultsets = new Dictionary<string, List<DynamicResult>>();
            queries = new Dictionary<string, string>();
            fileNames = new Dictionary<string, string>();
            queryParameters = new Dictionary<string, Dictionary<string, object>>();
            saver = new Thread(RunDumpLoop);
            updateLocker = new object();
            resourceLocker = new object();
            lock (updateLocker)
            {
                saver.Start();
                Monitor.Wait(updateLocker);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            lock (updateLocker)
            {
                keepRunning = false;
                Monitor.Pulse(updateLocker);
            }

            saver.Join();
        }

        /// <summary>
        /// Runs the dumper thread and stores the buffered data periodically
        /// </summary>
        private void RunDumpLoop()
        {
            bool hasOfflineData = false;
            lock (updateLocker)
            {
                while (keepRunning)
                {
                    if (firstRun)
                    {
                        Monitor.Pulse(updateLocker);
                        Monitor.Wait(updateLocker);
                    }
                    else
                    {
                        Monitor.Wait(updateLocker, updateTimeout);
                    }
                    if (keepRunning)
                    {
                        if (fileNames.Count != 0)
                        {
                            lock (resourceLocker)
                            {
                                foreach (string fileKey in offlineResultsets.Keys)
                                {
                                    if (
                                        File.Exists(string.Format(@"{0}\{1}", dumpStrategy.DumperPath,
                                                                  fileNames[fileKey])))
                                    {
                                        offlineResultsets[fileKey].AddRange(dumpStrategy.TryLoadData(fileNames[fileKey]));
                                        hasOfflineData = true;
                                    }
                                }
                            }

                            foreach (string key in queries.Keys)
                            {
                                if (database != null)
                                {
                                    DynamicResult[] data = database.GetNativeResults(queries[key], default(IController),
                                                                                     GetParameters(queryParameters[key]));
                                    lock (resourceLocker)
                                    {
                                        onlineResults[key] = data;
                                    }

                                    dumpStrategy.DumpData(data, fileNames[key]);
                                }
                                else
                                {
                                    if (!onlineResults.ContainsKey(key))
                                    {
                                        onlineResults[key] = dumpStrategy.LoadData(fileNames[key]);
                                    }
                                }
                            }

                            if (database != null)
                            {
                                if (hasOfflineData)
                                {
                                    CancelEventArgs e = new CancelEventArgs();
                                    OnImportOfflineData(e);
                                    if (!e.Cancel)
                                    {
                                        (from t in fileNames
                                         select string.Format(@"{0}\{1}", dumpStrategy.DumperPath, t.Value)).ToList()
                                            .ForEach(File.Delete);
                                        hasOfflineData = false;
                                    }
                                }
                            }
                            else
                            {
                                foreach (
                                    string fileKey in (from t in fileNames.Keys where !queries.ContainsKey(t) select t))
                                {
                                    lock (resourceLocker)
                                    {
                                        dumpStrategy.DumpData(offlineResultsets[fileKey].ToArray(), fileNames[fileKey]);
                                    }
                                }
                            }
                        }

                        if (firstRun)
                        {
                            Monitor.Pulse(updateLocker);
                            firstRun = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets a list of IdbDataParameters used to retreive a specific query from the database
        /// </summary>
        /// <param name="queryParameter">the dictionary containing the raw representation of the requested parameters</param>
        /// <returns>an IdbDataParameter array containing the requested dbparameters</returns>
        private IDbDataParameter[] GetParameters(Dictionary<string, object> queryParameter)
        {
            return (from t in queryParameter select database.GetParameter(t.Key, t.Value)).ToArray();
        }

        /// <summary>
        /// When raised, offlineData is available for import by a client object
        /// </summary>
        public event CancelEventHandler ImportOfflineData;

        /// <summary>
        /// private class used to lock the Database connection while doing something with the data
        /// </summary>
        private class DataLocker : IDisposable
        {
            /// <summary>
            /// the object that is used for thread interlocking
            /// </summary>
            private object lockObject;

            /// <summary>
            /// the object used to trigger the write process
            /// </summary>
            private object triggerObject;

            /// <summary>
            /// indicates whether this Locker was generated in offline mode
            /// </summary>
            private bool offline;

            /// <summary>
            /// Initializes a new instance of the DataLocker class
            /// </summary>
            /// <param name="lockObject">the object that is used to interlock offlinedata</param>
            /// <param name="triggerObject">the object that is used to trigger data-dumps</param>
            /// <param name="offline">indicates whether this object was generated in offlinemode</param>
            public DataLocker(object lockObject, object triggerObject, bool offline)
            {
                this.lockObject = lockObject;
                this.triggerObject = triggerObject;
                this.offline = offline;
                Monitor.Enter(lockObject);
            }

            /// <summary>
            /// Prevents a default instance of the DataLocker class from being created
            /// </summary>
            private DataLocker()
            {
            }

            /// <summary>
            /// Releases the locked data and forces a new dump of data in the database
            /// </summary>
            public void Dispose()
            {
                try
                {
                    if (offline)
                    {
                        lock (triggerObject)
                        {
                            Monitor.Pulse(triggerObject);
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(lockObject);
                }
            }
        }
    }
}