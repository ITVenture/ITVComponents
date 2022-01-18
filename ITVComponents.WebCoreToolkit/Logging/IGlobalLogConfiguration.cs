using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Threading;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Logging
{
    public interface IGlobalLogConfiguration
    {
        /// <summary>
        /// Indicates whether debug-messages are processed by this log-Configuration
        /// </summary>
        public bool EnableDebugMessages { get; }

        /// <summary>
        /// Checks if the given <paramref name="logLevel" /> is enabled.
        /// </summary>
        /// <param name="logLevel">level to be checked.</param>
        /// <param name="category">the category for which to check whether a message must be logged</param>
        /// <param name="ignoreGlobalDisable">indicates whether to ignore the global disable-flat on the configuration</param>
        /// <returns><c>true</c> if enabled.</returns>
        public bool IsEnabled(LogLevel logLevel, string category, bool ignoreGlobalDisable = false);

        /// <summary>
        /// Configures the current state of logging on the given LogConfiguration instance
        /// </summary>
        /// <param name="logLevels">the log-levels to log</param>
        /// <param name="logFilters">the configured log-filters</param>
        void Configure(int[] logLevels, IDictionary<LogLevel, string[]> logFilters);

        /// <summary>
        /// Stops any logging activity during the life-time of the returned object
        /// </summary>
        /// <returns>an object that will re-enable logging on dispose</returns>
        IResourceLock PauseLogging();
    }
}
