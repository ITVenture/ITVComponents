using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.Shared.WatchDogs
{
    public interface IWatchDogService
    {
        /// <summary>
        /// Manages all processes that have registered on this watchDog instance
        /// </summary>
        void ManageProcesses();
    }
}
