using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Threading;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Logging
{
    internal class GlobalLogConfiguration:IGlobalLogConfiguration
    {
        private int globalDisable = 0;
        private int[] enabledLogLevels = new int[]{(int)LogLevel.Information,(int)LogLevel.Warning, (int)LogLevel.Error, (int)LogLevel.Critical};
        private ConcurrentDictionary<LogLevel, string[]> logFilters = new ConcurrentDictionary<LogLevel, string[]>();

        private object modLock = new object();

        /// <summary>
        /// Indicates whether debug-messages are processed by this log-Configuration
        /// </summary>
        public bool EnableDebugMessages
        {
            get
            {
                lock (modLock)
                {
                    return enabledLogLevels.Any(n => n is (int)LogLevel.Trace or (int)LogLevel.Debug);
                }
            }
        }

        /// <summary>
        /// Checks if the given <paramref name="logLevel" /> is enabled.
        /// </summary>
        /// <param name="logLevel">level to be checked.</param>
        /// <param name="category">the category for which to check whether a message must be logged</param>
        /// <param name="ignoreGlobalDisable">indicates whether to ignore the global disable-flat on the configuration</param>
        /// <returns><c>true</c> if enabled.</returns>
        public bool IsEnabled(LogLevel logLevel, string category, bool ignoreGlobalDisable = false)
        {
            int[] tmp;
            string[] cat;
            bool globalDisabled;
            lock (modLock)
            {
                tmp = enabledLogLevels;
                logFilters.TryGetValue(logLevel, out cat);
                globalDisabled = globalDisable != 0;
            }
            if (!globalDisabled || ignoreGlobalDisable)
            {
                var retVal = tmp.Any(i => i == (int)logLevel);
                if (retVal && cat != null && cat.Length != 0 && !string.IsNullOrEmpty(category))
                {
                    var filter = cat;

                    try
                    {
                        retVal = filter.Any(s => Regex.IsMatch(category, s,
                            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase |
                            RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline));
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine(ex.Message);
                    }
                }

                return retVal;
            }

            return false;
        }

        

        public void Configure(int[] logLevels, IDictionary<LogLevel, string[]> logFilters)
        {
            lock (modLock)
            {
                enabledLogLevels = logLevels;
                this.logFilters.Clear();
                foreach (var i in logFilters)
                {
                    this.logFilters.TryAdd(i.Key, i.Value);
                }
            }
        }

        public IResourceLock PauseLogging()
        {
            return new GlobalDisableLock(this);
        }

        private class GlobalDisableLock : IResourceLock
        {
            private readonly GlobalLogConfiguration parent;
            private bool disposed = false;

            public GlobalDisableLock(GlobalLogConfiguration parent)
            {
                lock (parent.modLock)
                {
                    this.parent = parent;
                    parent.globalDisable++;
                }
            }
            public void Dispose()
            {
                lock (parent.modLock)
                {
                    if (!disposed)
                    {
                        parent.globalDisable--;
                        disposed = true;
                    }
                }
            }

            public IResourceLock InnerLock { get; }
            public void Exclusive(bool autoLock, Action action)
            {
                action();
            }

            public T Exclusive<T>(bool autoLock, Func<T> action)
            {
                return action();
            }

            public void SynchronizeContext()
            {
            }

            public void LeaveSynchronizeContext()
            {
            }

            public IDisposable PauseExclusive()
            {
                return new ExclusivePauseHelper(() => null);
            }
        }
    }
}
