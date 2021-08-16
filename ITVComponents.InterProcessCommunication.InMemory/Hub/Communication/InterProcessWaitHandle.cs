using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Communication
{
    /// <summary>
    /// Wait-Handle to synchronize InterProcess actions for the communication through a MemoryMapped File
    /// </summary>
    public class InterProcessWaitHandle:IDisposable
    {
        /// <summary>
        /// Indicates whether the mutex is currently owned by this handle
        /// </summary>
        private ThreadLocal<bool> currentlyOwned = new ThreadLocal<bool>(()=>false);

        /// <summary>
        /// the inner mutex that is handled by this InterProcessWaitHandle instance
        /// </summary>
        private Mutex innerMux;

        /// <summary>
        /// Initializes a new instance of the InterProcessWaitHandle class
        /// </summary>
        /// <param name="name">the name of the underlaying mutex</param>
        public InterProcessWaitHandle(string name)
        {
            var ctl = new MutexSecurity();
            var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow);
            ctl.AddAccessRule(allowEveryoneRule);
            innerMux = MutexAcl.Create(false, name, out bool isNew, ctl);
            if (!isNew)
            {
                LogEnvironment.LogDebugEvent("opened existing mutex...", LogSeverity.Report);
                //currentlyOwned.Value = false;
            }
            else
            {
                //currentlyOwned = false;
            }
        }

        /// <summary>
        /// Releases the inner mutex so that a waiting processhandle gets signalled
        /// </summary>
        public void Pulse()
        {
            if (currentlyOwned.Value)
            {
                innerMux.ReleaseMutex();
                currentlyOwned.Value = false;
            }
        }

        /// <summary>
        /// Waits until the timeout for the mutex to respond
        /// </summary>
        /// <param name="timeOut">the maximum timeout to wait for the mutex</param>
        /// <returns>a value indicating whether the mutex is now owned by this instance</returns>
        public bool WaitOne(int timeOut)
        {
            if (currentlyOwned.Value)
            {
                Pulse();
            }

            try
            {
                return currentlyOwned.Value = innerMux.WaitOne(timeOut);
            }
            catch (AbandonedMutexException)
            {
                LogEnvironment.LogDebugEvent("mutex abandoned", LogSeverity.Report);
                return currentlyOwned.Value = true;
            }
        }

        /// <summary>
        /// Waits for the inner mutex without a timeout
        /// </summary>
        /// <returns>indicates whether the mutex is now owned by this instance</returns>
        public bool WaitOne()
        {
            if (currentlyOwned.Value)
            {
                Pulse();
            }

            try
            {
                return currentlyOwned.Value = innerMux.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                LogEnvironment.LogDebugEvent("mutex abandoned", LogSeverity.Report);
                return currentlyOwned.Value = true;
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            innerMux?.Dispose();
            innerMux = null;
        }
    }
}
