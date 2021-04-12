using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents
{
    /// <summary>
    /// Handles critical Errors on the current process that are raised by a critical component
    /// </summary>
    public interface IProcessWatchDog
    {
        /// <summary>
        /// Rgisters this ProcessWatchDog instance for a specific critical component object
        /// </summary>
        /// <param name="targetComponent">the registered target object</param>
        void RegisterFor(ICriticalComponent targetComponent);
    }
}
