using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.Formatting.ScriptExtensions
{
    /// <summary>
    /// Dummy-Plugin that will register Scripting-features of the TextFormat lib
    /// </summary>
    public class ScriptFormatInitializer:IPlugin
    {
        /// <summary>
        /// Initializes a new instance of the ScriptFormatInitializer Plugin
        /// </summary>
        public ScriptFormatInitializer()
        {
            ScriptExtensionHelper.Register();
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
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
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
