using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Logging
{
    /// <summary>
    /// Logger interface that will populate collected log entries to a specific target
    /// </summary>
    public interface ILogOutputAdapter
    {
        /// <summary>
        /// Gets a list of log-levels that is available for this logger
        /// </summary>
        /// <returns></returns>
        int[] GetLogLevels();

        /// <summary>
        /// Populates a collected event to the target of this adapter
        /// </summary>
        /// <param name="eventData">the collected event-data record</param>
        void PopulateEvent(SystemEvent eventData);

        /// <summary>
        /// Saves all populated changes to the target
        /// </summary>
        void Flush();

        /// <summary>
        /// Creates a list of LogFilters for a consuming Toolkit-Logger
        /// </summary>
        /// <returns>a dictionary that contains all configured log-filters</returns>
        Dictionary<LogLevel, string[]> GetLogFilters();
    }
}
