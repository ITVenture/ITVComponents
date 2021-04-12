using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataAccess.Linq
{
    /// <summary>
    /// Not-Threadsafe Context. Use this Context only if you need tables in multiple threads and you know what you're doing!
    /// </summary>
    public class UnsafeLinqContext:IDataContext
    {
        public UnsafeLinqContext()
        {
            Tables = new Dictionary<string, IEnumerable<DynamicResult>>();
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets a List of available Tables for this DataContext
        /// </summary>
        public IDictionary<string, IEnumerable<DynamicResult>> Tables { get; private set; }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
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
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
