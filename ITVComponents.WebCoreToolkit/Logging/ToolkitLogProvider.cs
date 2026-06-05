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

        // Duplicate-event throttling: identical events (same level/category/title/message) arriving within the
        // configured window are suppressed and only counted; a single summary event is emitted afterwards. This
        // protects the log-backend from spam during error-loops without losing the information that it happened.
        private ConcurrentDictionary<string, ThrottleState> throttleStates = new ConcurrentDictionary<string, ThrottleState>();

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
                using (var lk = globalLogCfg.PauseLogging())
                {
                    lk.Exclusive(true, () =>
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

                                // Emit any pending throttle-summaries whose window has expired before flushing,
                                // so suppressed-counts reach the backend even if the event never recurs.
                                if (globalLogCfg.ThrottleDuplicates)
                                {
                                    FlushExpiredThrottleStates(DateTime.UtcNow, globalLogCfg.ThrottleWindow);
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
                    });
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
            if (!globalLogCfg.ThrottleDuplicates)
            {
                Enqueue(logLevel, category, title, message);
                return;
            }

            var window = globalLogCfg.ThrottleWindow;
            var now = DateTime.UtcNow;
            var key = BuildThrottleKey(logLevel, category, title, message);
            var st = throttleStates.GetOrAdd(key, _ => new ThrottleState());
            lock (st)
            {
                // Still inside the suppression window of a previously-emitted identical event: just count it.
                if (st.WindowStart != default && now - st.WindowStart < window)
                {
                    st.Suppressed++;
                    return;
                }

                // Window expired (or first occurrence): flush any pending suppressed-summary, then emit this one.
                if (st.Suppressed > 0)
                {
                    EnqueueSummary(st);
                    st.Suppressed = 0;
                }

                Enqueue(logLevel, category, title, message);
                st.WindowStart = now;
                st.LogLevel = logLevel;
                st.Category = category;
                st.Title = title;
                st.Message = message;
            }
        }

        /// <summary>
        /// Enqueues a single event for the next flush to the log-backend.
        /// </summary>
        private void Enqueue(LogLevel logLevel, string category, string title, string message)
        {
            events.Enqueue(new SystemEvent
            {
                LogLevel = logLevel,
                EventTime = DateTime.UtcNow,
                Category = category,
                Title = title,
                Message = message
            });
        }

        /// <summary>
        /// Emits a single summary event for the events that were suppressed during a throttle-window.
        /// </summary>
        private void EnqueueSummary(ThrottleState st)
        {
            Enqueue(st.LogLevel, st.Category, st.Title,
                $"{st.Message}\n[throttled: {st.Suppressed} further identical occurrence(s) suppressed]");
        }

        /// <summary>
        /// Builds a stable (per-process) key identifying an event for duplicate-detection.
        /// </summary>
        private static string BuildThrottleKey(LogLevel logLevel, string category, string title, string message)
        {
            return $"{(int)logLevel}|{category}|{title}|{message?.GetHashCode() ?? 0}";
        }

        /// <summary>
        /// Flushes suppressed-summaries for throttle-states whose window has expired and removes stale entries to
        /// keep the throttle-map bounded. Invoked from the periodic flush-timer.
        /// </summary>
        private void FlushExpiredThrottleStates(DateTime now, TimeSpan window)
        {
            foreach (var kv in throttleStates)
            {
                var st = kv.Value;
                lock (st)
                {
                    if (st.WindowStart == default || now - st.WindowStart < window)
                    {
                        continue;
                    }

                    if (st.Suppressed > 0)
                    {
                        EnqueueSummary(st);
                        st.Suppressed = 0;
                    }

                    // No pending occurrences and the window has long passed: drop to keep the map bounded.
                    if (now - st.WindowStart >= window + window)
                    {
                        throttleStates.TryRemove(kv.Key, out _);
                    }
                }
            }
        }

        private sealed class ThrottleState
        {
            public DateTime WindowStart;
            public long Suppressed;
            public LogLevel LogLevel;
            public string Category;
            public string Title;
            public string Message;
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
