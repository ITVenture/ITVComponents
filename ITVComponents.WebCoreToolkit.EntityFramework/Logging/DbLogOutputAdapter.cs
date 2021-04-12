using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.Logging;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Logging
{
    internal class DbLogOutputAdapter:ILogOutputAdapter
    {
        private readonly SecurityContext db;
        private readonly IGlobalSettings<DbLoggingOptions> logSettings;

        public DbLogOutputAdapter(SecurityContext db, IGlobalSettings<DbLoggingOptions> logSettings)
        {
            this.db = db;
            this.logSettings = logSettings;
        }

        /// <summary>
        /// Gets a list of log-levels that is available for this logger
        /// </summary>
        /// <returns></returns>
        public int[] GetLogLevels()
        {
            List<int> l = new List<int>();
            var opt = logSettings.Value;
            if (opt.LogEnabled)
            {
                if (opt.LogCritical)
                {
                    l.Add((int)LogLevel.Critical);
                }

                if (opt.LogDebug)
                {
                    l.Add((int)LogLevel.Debug);
                }

                if (opt.LogError)
                {
                    l.Add((int)LogLevel.Error);
                }

                if (opt.LogInformation)
                {
                    l.Add((int)LogLevel.Information);
                }

                if (opt.LogTrace)
                {
                    l.Add((int)LogLevel.Trace);
                }

                if (opt.LogWarning)
                {
                    l.Add((int)LogLevel.Warning);
                }

                if (opt.LogNone)
                {
                    l.Add((int)LogLevel.None);
                }
            }

            return l.ToArray();
        }

        /// <summary>
        /// Populates a collected event to the target of this adapter
        /// </summary>
        /// <param name="eventData">the collected event-data record</param>
        public void PopulateEvent(SystemEvent eventData)
        {
            db.SystemLog.Add(eventData.ToViewModel<SystemEvent, Models.SystemEvent>());
        }

        /// <summary>
        /// Saves all populated changes to the target
        /// </summary>
        public void Flush()
        {
            db.SaveChanges();
        }

        /// <summary>
        /// Creates a list of LogFilters for a consuming Toolkit-Logger
        /// </summary>
        /// <returns>a dictionary that contains all configured log-filters</returns>
        public Dictionary<LogLevel, string[]> GetLogFilters()
        {
            var opt = logSettings.Value;
            var retVal = new Dictionary<LogLevel, string[]>();
            if (opt.LogEnabled)
            {
                if (opt.LogCritical && opt.CriticalFilters != null && opt.CriticalFilters.Length != 0)
                {
                    retVal.Add(LogLevel.Critical, opt.CriticalFilters);
                }

                if (opt.LogDebug && opt.DebugFilters != null && opt.DebugFilters.Length != 0)
                {
                    retVal.Add(LogLevel.Debug, opt.DebugFilters);
                }

                if (opt.LogError && opt.ErrorFilters != null && opt.ErrorFilters.Length != 0)
                {
                    retVal.Add(LogLevel.Error, opt.ErrorFilters);
                }

                if (opt.LogInformation && opt.InformationFilters != null && opt.InformationFilters.Length != 0)
                {
                    retVal.Add(LogLevel.Information, opt.InformationFilters);
                }

                if (opt.LogTrace && opt.TraceFilters != null && opt.TraceFilters.Length != 0)
                {
                    retVal.Add(LogLevel.Trace, opt.TraceFilters);
                }

                if (opt.LogWarning && opt.WarningFilters != null && opt.WarningFilters.Length != 0)
                {
                    retVal.Add(LogLevel.Warning, opt.WarningFilters);
                }
            }

            return retVal;
        }
    }
}
