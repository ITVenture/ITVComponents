using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DataAccess.Linq;
using ITVComponents.DataExchange.Interfaces;
using ITVComponents.Plugins;

namespace ITVComponents.DataExchange.Linq
{
    public class LinqTarget:IDataContainer, IDeferredInit, IDisposable
    {
        /// <summary>
        /// The target context to serve with data
        /// </summary>
        private IDataContext targetContext;

        /// <summary>
        /// the parent on which to register this item
        /// </summary>
        private IDataCollector parent;

        private readonly string name;

        /// <summary>
        /// Initializes a new instance of the LinqTaret class
        /// </summary>
        /// <param name="targetContext">the linq datacontext that is used to store data for linq queries</param>
        /// <param name="parent">the datacollector that is using this target to register partial results</param>
        public LinqTarget(IDataContext targetContext, IDataCollector parent, string uniqueName)
        {
            this.targetContext = targetContext;
            this.parent = parent;
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
        /// Gets the root-collection of the collected data
        /// </summary>
        public DynamicResult[] RootCollection
        {
            get
            {
                Dictionary<string, object> tmp = new Dictionary<string, object>();
                (from t in targetContext.Tables select new KeyValuePair<string, object>(t.Key, t.Value)).ForEach(
                    n => tmp.Add(n.Key, n.Value));
                return new[] {new DynamicResult(tmp)};
            }
        }

        public void Initialize()
        {
            if (!Initialized)
            {
                try
                {
                    parent.RegisterTarget(name, this);
                    Init();
                }
                finally
                {
                    Initialized = true;
                }
            }
        }

        /// <summary>
        /// Clears this container
        /// </summary>
        public void Clear()
        {
            targetContext.Tables.Clear();
        }

        /// <summary>
        /// Registers a Dataset in this datacontainer
        /// </summary>
        /// <param name="name">the name of the dataset</param>
        /// <param name="data">the data to register for the provided name</param>
        public void RegisterData(string name, DynamicResult[] data)
        {
            targetContext.Tables[name] = data;
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            parent.UnregisterTarget(name, this);
        }

        /// <summary>
        /// Runs Initializations on derived objects
        /// </summary>
        protected virtual void Init()
        {
        }
    }
}
