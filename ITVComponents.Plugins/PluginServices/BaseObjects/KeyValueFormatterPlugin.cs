using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.Plugins.PluginServices.BaseObjects
{
    public class KeyValueFormatterPlugin : KeyValueFormatter
    {
        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        #region Overrides of ObjectFormatter

        /// <summary>Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.</summary>
        /// <filterpriority>2</filterpriority>
        public override void Dispose()
        {
            base.Dispose();
            OnDisposed();
        }

        #endregion

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
