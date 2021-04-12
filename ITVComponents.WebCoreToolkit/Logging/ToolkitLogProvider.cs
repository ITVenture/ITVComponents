using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Logging
{
    internal class ToolkitLogProvider:ILoggerProvider, ILogCollectorService, ILogTarget, IDebugLogTarget
    {
        private readonly IServiceProvider services;

        private Timer timer;

        private bool disposed;

        private ConcurrentQueue<SystemEvent> events = new ConcurrentQueue<SystemEvent>();

        private ConcurrentDictionary<string, CollectingLogger> availableLoggers = new ConcurrentDictionary<string, CollectingLogger>();

        private bool globalDisable = false;

        private int[] enabledLogLevels  = new int[] {2, 3, 4, 5};

        private Dictionary<LogLevel, string[]> logFilters = new Dictionary<LogLevel, string[]>();

        public ToolkitLogProvider(IServiceProvider services)
        {
            this.services = services;
            LogEnvironment.RegisterLogTarget(this);
            timer = new Timer(DumpEvents, null, 10000, Timeout.Infinite);
        }

        /// <summary>
        /// Indicates whether debug-messages are processed by this log-target
        /// </summary>
        public bool EnableDebugMessages { get; set; }


        /// <summary>
        /// Dumps all events to an outputAdapter instance if one is available
        /// </summary>
        /// <param name="state"></param>
        private void DumpEvents(object? state)
        {
            try
            {
                globalDisable = true;
                using (var scope = services.CreateScope())
                {
                    var adapter = scope.ServiceProvider.GetService<ILogOutputAdapter>();
                    if (adapter != null)
                    {
                        enabledLogLevels = adapter.GetLogLevels();
                        logFilters = adapter.GetLogFilters();
                        EnableDebugMessages = enabledLogLevels.Any(n => n == (int) LogLevel.Trace || n == (int) LogLevel.Debug);
                        if (EnableDebugMessages)
                        {
                            LogEnvironment.EnableDebugMessages();
                        }
                        else
                        {
                            LogEnvironment.DisableDebugMessages();
                        }

                        if (!events.IsEmpty)
                        {
                            try
                            {
                                while (events.TryDequeue(out var eventData))
                                {
                                    adapter.PopulateEvent(eventData);
                                }
                            }
                            finally
                            {
                                adapter.Flush();
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.OutlineException());
            }
            finally
            {
                globalDisable = false;
                if (!disposed)
                {
                    timer.Change(10000, Timeout.Infinite);
                }
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            disposed = true;
            timer.Dispose();
            OnDisposed();
        }

        /// <summary>
        /// Creates a new <see cref="T:Microsoft.Extensions.Logging.ILogger" /> instance.
        /// </summary>
        /// <param name="categoryName">The category name for messages produced by the logger.</param>
        /// <returns>The instance of <see cref="T:Microsoft.Extensions.Logging.ILogger" /> that was created.</returns>
        public ILogger CreateLogger(string categoryName)
        {
            return availableLoggers.GetOrAdd(categoryName, s => new CollectingLogger(this, s));
        }

        /// <summary>
        /// Checks if the given <paramref name="logLevel" /> is enabled.
        /// </summary>
        /// <param name="logLevel">level to be checked.</param>
        /// <param name="category">the category for which to check whether a message must be logged</param>
        /// <returns><c>true</c> if enabled.</returns>
        public bool IsEnabled(LogLevel logLevel, string category)
        {
            if (!globalDisable)
            {
                var tmp = enabledLogLevels;
                var cat = logFilters;
                var retVal = tmp.Any(i => i == (int) logLevel);
                if (retVal && cat != null && cat.ContainsKey(logLevel))
                {
                    var filter = cat[logLevel];
                    if (filter != null && filter.Length != 0)
                    {
                        try
                        {
                            retVal = filter.Any(s => Regex.IsMatch(category, s, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }

                return retVal;
            }

            return false;
        }

        /// <summary>
        /// /Adds an event to a queue that is periodically being flushed
        /// </summary>
        /// <param name="logLevel">the logLevel of the event</param>
        /// <param name="title">the event-title</param>
        /// <param name="message">a message that was generated by a module</param>
        public void AddEvent(LogLevel logLevel, string category, string title, string message)
        {
            events.Enqueue(new SystemEvent
            {
                LogLevel = logLevel,
                EventTime = DateTime.Now,
                Category = category,
                Title = title,
                Message = message
            });
        }

        /// <summary>
        /// Logs events that were generated by any ITVComponents internal component
        /// </summary>
        /// <param name="eventText">the event-text that was generated for logging</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">the log-category of the event</param>
        void ILogTarget.LogEvent(string eventText, int severity, string context)
        {
            var sv = LogEnvironment.GetClosestSeverity(severity);
            var loglevel = sv == LogSeverity.Report ? LogLevel.Information : sv == LogSeverity.Warning ? LogLevel.Warning : LogLevel.Error;
            var cat = context ?? "ITVComponents";
            if (IsEnabled(loglevel, cat))
            {
                AddEvent(loglevel, cat, "ITVComponents-Message", eventText);
            }
        }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Disposed;
    }
}
