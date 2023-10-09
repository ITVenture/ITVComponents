using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ITVComponents.Plugins;

namespace ITVComponents.DataAccess.Linq
{
    public class DefaultContext:IDataContext, IDisposable
    {
        /// <summary>
        /// holds local tables for the current Thread
        /// </summary>
        private ThreadLocal<IDictionary<string, IEnumerable<DynamicResult>>> tables;

        /// <summary>
        /// Initializes a new instance of the DefaultContext class
        /// </summary>
        public DefaultContext()
        {
            tables = new ThreadLocal<IDictionary<string, IEnumerable<DynamicResult>>>(()=>new Dictionary<string, IEnumerable<DynamicResult>>());
        }

        /// <summary>
        /// Gets a List of available Tables for this DataContext
        /// </summary>
        public IDictionary<string, IEnumerable<DynamicResult>> Tables
        {
            get { return tables.Value; }
        }
        
        /// <summary>
        /// Flushes the local data of this context
        /// </summary>
        public void FlushLocalContext()
        {
            tables.Value.Clear();
        }

        /// <summary>
        /// Registers a table in the local context
        /// </summary>
        /// <param name="tableName">the name of the table</param>
        /// <param name="data">the data of the declared table</param>
        public void RegisterTable(string tableName, IEnumerable<DynamicResult> data)
        {
            tables.Value[tableName] = data;
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            tables.Dispose();
        }
    }
}
