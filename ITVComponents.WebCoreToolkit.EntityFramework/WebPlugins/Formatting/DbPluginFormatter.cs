﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Formatting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.WebPlugins.Formatting
{
    public class DbPluginFormatter:IStringFormatProvider
    {
        private Dictionary<string, object> formatPrototype = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Initializes a new instance of the DbPluginFormatter class
        /// </summary>
        /// <param name="context">the database containing formatting-hints</param>
        public DbPluginFormatter(SecurityContext context)
        {
            context.WebPluginConstants.ForEach(n => formatPrototype.Add(n.Name, n.Value));
        }

        /// <summary>
        /// Processes a raw-string and uses it as format-string of the configured const-collection
        /// </summary>
        /// <param name="rawString">the raw-string that was read from a plugin-configuration string</param>
        /// <returns>the format-result of the raw-string</returns>
        public string ProcessLiteral(string rawString)
        {
            return formatPrototype.FormatText(rawString);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
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
