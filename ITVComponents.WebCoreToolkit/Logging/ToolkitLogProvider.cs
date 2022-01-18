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
        private readonly IServiceScopeFactory services;

        private readonly IGlobalLogConfiguration globalLogCfg;

        private Timer timer;

        private bool disposed;

        private ConcurrentQueue<SystemEvent> events = new ConcurrentQueue<SystemEvent>();

        private ConcurrentDictionary<string, CollectingLogger> availableLoggers = new ConcurrentDictionary<string, CollectingLogger>();

        //private int[] enabledLogLevels  = new int[] {2, 3, 4, 5};

        //private Dictionary<LogLevel, string[]> logFilters = new Dictionary<LogLevel, string[]>();

        public ToolkitLogProvider(IServiceScopeFactory services, IGlobalLogConfiguration globalLogCfg)
        {
            this.services = services;
            this.globalLogCfg = globalLogCfg;
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
                using (globalLogCfg.PauseLogging())
                {
                    using (var scope = services.CreateScope())
                    {
                        var adapter = scope.ServiceProvider.GetService<ILogOutputAdapter>();
                        if (adapter != null)
                        {
                            EnableDebugMessages = globalLogCfg.EnableDebugMessages;
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
                                        try
                                        {
                                            adapter.PopulateEvent(eventData);
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Console.WriteLine(ex.OutlineException());
                                        }
                                    }
                                }
                                finally
                                {
                                    adapter.Flush();
                                }
                            }
                        }
                        else
                        {
                            events.Clear();
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                System.Console.WriteLine(ex.OutlineException());
                events.Clear();
            }
            finally
            {
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
            availableLoggers.Clear();
            OnDisposed();
        }

        /// <summary>
        /// Creates a new <see cref="T:Microsoft.Extensions.Logging.ILogger" /> instance.
        /// </summary>
        /// <param name="categoryName">The category name for messages produced by the logger.</param>
        /// <returns>The instance of <see cref="T:Microsoft.Extensions.Logging.ILogger" /> that was created.</returns>
        public ILogger CreateLogger(string categoryName)
        {
            return availableLoggers.GetOrAdd(categoryName, s => new CollectingLogger(this, s, globalLogCfg));
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
            if (globalLogCfg.IsEnabled(loglevel, cat))
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
