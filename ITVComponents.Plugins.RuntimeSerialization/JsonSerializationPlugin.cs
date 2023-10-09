
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Serialization;

namespace ITVComponents.Plugins.RuntimeSerialization
{
    /*/// <summary>
    /// Binary serializer class enabling a plugin driven application to save the runtime status of its plugins
    /// </summary>
    [Obsolete("Not supported anymore!", true)]
    public class JsonSerializationPlugin : IRuntimeSerializer
    {
        /// <summary>
        /// the target file path for saving and loading runtime states
        /// </summary>
        private string targetFileName;

        /// <summary>
        /// Initializes a new instance of the BinarySerializationPlugin class
        /// </summary>
        /// <param name="targetFileName">The File in which the runtimedata is stored and loaded</param>
        public JsonSerializationPlugin(string targetFileName)
        {
            this.targetFileName = targetFileName;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (Disposed != null)
            {
                Disposed(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Saves the runtime information provided into a file
        /// </summary>
        /// <param name="information">the Runtime information that needs to be saved into a file</param>
        public void SaveRuntimeStatus(RuntimeInformation information)
        {
            JsonHelper.WriteObjectStrongTyped(information, Encoding.UTF8, targetFileName, true);
        }

        /// <summary>
        /// Loads the runtime status of the application from a file
        /// </summary>
        /// <returns>the Runtime status of the current application's plugins</returns>
        public RuntimeInformation LoadRuntimeStatus()
        {
            return JsonHelper.ReadObject<RuntimeInformation>(targetFileName, Encoding.UTF8, true);
        }

        /// <summary>
        /// Informs a client object about the disposal of this instance
        /// </summary>
        [field: NonSerialized]
        public event EventHandler Disposed;
    }*/
}
