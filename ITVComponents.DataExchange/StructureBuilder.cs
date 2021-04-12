using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.DataExchange.Interfaces;
using ITVComponents.Plugins;
using ITVComponents.Plugins.SelfRegistration;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;

namespace ITVComponents.DataExchange
{
    public class StructureBuilder:IStructureBuilder, IPlugin, IDeferredInit
    {
        /// <summary>
        /// Holds the current root collection that will contain the entire datastructure that is built by this builder
        /// </summary>
        private ThreadLocal<DynamicResult[]> rootCollection;

        /// <summary>
        /// Holds the current data-Callback that was provided by a collector
        /// </summary>
        private ThreadLocal<ExecuteQuery> currentCallback;

        /// <summary>
        /// Holds the current DataItem for which to extend the structure
        /// </summary>
        private ThreadLocal<DynamicResult> currentItem;

        /// <summary>
        /// Holds the configuration for the current structure layer of this builder
        /// </summary>
        private ThreadLocal<QueryConfigurationCollection> currentConfiguration;

        /// <summary>
        /// Holds the parent-tree of this builder in reverse order
        /// </summary>
        private ThreadLocal<List<DynamicResult>> parents;

        /// <summary>
        /// The datacollector object that is used to provide all collected data
        /// </summary>
        private IDataCollector parent;

        /// <summary>
        /// Initializes a new instance of the StructureBuilder class
        /// </summary>
        public StructureBuilder(IDataCollector parent)
        {
            this.parent = parent;
            rootCollection = new ThreadLocal<DynamicResult[]>();
            currentCallback = new ThreadLocal<ExecuteQuery>();
            currentItem = new ThreadLocal<DynamicResult>();
            currentConfiguration = new ThreadLocal<QueryConfigurationCollection>();
            parents = new ThreadLocal<List<DynamicResult>>();
        }

        /// <summary>
        /// Gets the root-collection of the collected data
        /// </summary>
        public DynamicResult[] RootCollection { get { return rootCollection.Value; } }

        /// <summary>
        /// Gets the current Parents for processing
        /// </summary>
        private List<DynamicResult> Parents
        {
            get
            {
                if (parents.Value == null)
                {
                    parents.Value = new List<DynamicResult>();
                }
                return parents.Value;
            }
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
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            if (!Initialized)
            {
                try
                {
                    parent.RegisterStructureBuilder(UniqueName, this);
                    parent.RegisterTarget(UniqueName, this);
                    Init();
                }
                finally
                {
                    Initialized = true;
                }
            }
        }

        /// <summary>
        /// Registers a Dataset in this datacontainer
        /// </summary>
        /// <param name="name">the name of the dataset</param>
        /// <param name="data">the data to register for the provided name</param>
        public void RegisterData(string name, DynamicResult[] data)
        {
            if (name == "root")
            {
                rootCollection.Value = data;
            }
            else if (currentItem.Value != null)
            {
                currentItem.Value[name] = data;
            }
            else
            {
                throw new InvalidOperationException("Unable to register this dataset");
            }

            QueryConfiguration config;
            if ((config = currentConfiguration.Value[name]) != null)
            {
                if (config.Children.Count != 0)
                {
                    ProcessConfig(config.Children, data);
                }
            }
        }

        /// <summary>
        /// Clears this container
        /// </summary>
        public void Clear()
        {
            rootCollection.Value = null;
        }

        /// <summary>
        /// Builds a structure and uses the specified query-callback
        /// </summary>
        /// <param name="queryCallback">the query-callback that is used to the get structure-queries</param>
        /// <param name="configuration">the Configuration that must be used for this structurebuild-run</param>
        public void BuildStructure(ExecuteQuery queryCallback, QueryConfigurationCollection configuration)
        {
            currentCallback.Value = queryCallback;
            try
            {
                ProcessConfig(configuration);
            }
            finally
            {
                currentCallback.Value = null;
            }
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
        /// Processes all configurations in the provided configuration collection
        /// </summary>
        /// <param name="configuration">the current configuration collection</param>
        /// <param name="data">the data that came out of the last query</param>
        private void ProcessConfig(QueryConfigurationCollection configuration, DynamicResult[] data = null)
        {
            QueryConfigurationCollection tmp = currentConfiguration.Value;
            DynamicResult currentTmpItem = currentItem.Value;
            try
            {
                if (currentTmpItem != null)
                {
                    Parents.Insert(0, currentTmpItem);
                }
                currentConfiguration.Value = configuration;
                if (data != null)
                {
                    foreach (DynamicResult result in data)
                    {
                        currentItem.Value = result;
                        ProcessQueries(configuration);
                    }
                }
                else
                {
                    ProcessQueries(configuration);
                }
            }
            finally
            {
                currentConfiguration.Value = tmp;
                currentItem.Value = currentTmpItem;
                if (currentTmpItem != null)
                {
                    Parents.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Runs all configured queries in the sequence they were registered in the configuration
        /// </summary>
        /// <param name="queries">the queries to run on the target</param>
        private void ProcessQueries(QueryConfigurationCollection queries)
        {
            Dictionary<string, object> variables = new Dictionary<string, object>{{"current",currentItem.Value},{"parents",Parents.ToArray()}};
            QueryDefinition callConfig = new QueryDefinition();
            foreach (QueryConfiguration config in queries)
            {
                callConfig.Query = config.Query;
                callConfig.Source = config.Source;
                callConfig.Targets =
                    (from t in config.Targets
                     select new TargetDefinition {TargetName = t.TargetName, RegisterAs = t.RegisterName}).ToArray();
                callConfig.Parameters = (from t in config.Parameters
                                         select
                                             new ParameterDefinition
                                                 {
                                                     ParameterName = t.ParameterName,
                                                     ParameterValue =
                                                         ExpressionParser.Parse(t.ParameterExpression,
                                                                                variables, a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); })
                                                 }).ToArray();
                currentCallback.Value(callConfig);
            }
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            parent.UnregisterStructureBuilder(UniqueName, this);
            parent.UnregisterTarget(UniqueName, this);
            OnDisposed();
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
