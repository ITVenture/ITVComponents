using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins
{
    /// <summary>
    /// Implements a stoppable plugin (A Plugin that must be stopped before it can be disposed)
    /// </summary>
    public interface IStoppable
    {
        /// <summary>
        /// Stops Processes that are running inside the current Plugin
        /// </summary>
        void Stop();
    }
}
