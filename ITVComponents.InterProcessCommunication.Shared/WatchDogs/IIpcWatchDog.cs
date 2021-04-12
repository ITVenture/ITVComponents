using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Shared.WatchDogs
{
    public interface IIpcWatchDog
    {
        /// <summary>
        /// Sets the Status of a specific process on a machine
        /// </summary>
        /// <param name="machine">the machine on which the process is being executed</param>
        /// <param name="processName">the name of the process</param>
        /// <param name="processId">the id of the process</param>
        /// <param name="rebootRequired">indicates whether this process requires a reboot</param>
        /// <returns>indicates whether the healt-status was accepted by the watch-dog</returns>
        bool SetProcessStatus(string machine, string processName, int processId, bool rebootRequired);
    }
}
