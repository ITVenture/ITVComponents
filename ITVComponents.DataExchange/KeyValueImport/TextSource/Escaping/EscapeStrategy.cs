using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.DataExchange.KeyValueImport.TextSource.Escaping
{
    public abstract class EscapeStrategy:IPlugin
    {
        /// <summary>
        /// Holds all initializes strategies
        /// </summary>
        private static ConcurrentDictionary<string, EscapeStrategy> availableStrategies;

        /// <summary>
        /// the name of this strategy
        /// </summary>
        private string name;

        /// <summary>
        /// Initializes static members of the EscapeStrategy class
        /// </summary>
        static EscapeStrategy()
        {
            availableStrategies = new ConcurrentDictionary<string, EscapeStrategy>();
            var tmp = new DefaultEscapeStrategy("Default");
        }

        /// <summary>
        /// Initializes a new instance of the EscapeSrategy class
        /// </summary>
        /// <param name="name">the strategyName of this strategy</param>
        protected EscapeStrategy(string name)
        {
            availableStrategies.AddOrUpdate(name, n => this, (n, e) => e);
            this.name = name;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Removes escape-characters from a string and returns the effective value
        /// </summary>
        /// <param name="raw">the raw-value of the string</param>
        /// <returns>the un-escaped value of the string</returns>
        public abstract string Unescape(string raw);

        /// <summary>
        /// Escapes a specific string using the demanded strategy
        /// </summary>
        /// <param name="strategyName">the strategy that is used to unescape a string</param>
        /// <param name="raw">the raw-value</param>
        /// <returns>an unescaped string</returns>
        public static string UnescapeString(string strategyName, string raw)
        {
            if (availableStrategies.TryGetValue(strategyName, out var strat))
            {
                return strat.Unescape(raw);
            }

            return raw;
        }

        /// <summary>
        ///   Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        public void Dispose()
        {
            OnDisposed();
            if (availableStrategies.TryGetValue(name, out var me) && me == this)
            {
                availableStrategies.TryRemove(name, out _);
            }
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
