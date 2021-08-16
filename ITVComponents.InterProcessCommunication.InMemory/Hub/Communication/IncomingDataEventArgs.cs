using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Communication
{
    /// <summary>
    /// Event-data of delivered data through a Memorymapped file
    /// </summary>
    public class IncomingDataEventArgs
    {
        /// <summary>
        /// The data that was transmitted
        /// </summary>
        public string Data { get; set; }

        public DataTransferContext Context { get; set; }
    }
}
