using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Logging
{
    /// <summary>
    /// LogTarget extension that can log debug-messages
    /// </summary>
    public interface IDebugLogTarget:ILogTarget
    {
        /// <summary>
        /// Indicates whether debug-messages are processed by this log-target
        /// </summary>
        bool EnableDebugMessages { get; set; }
    }
}
