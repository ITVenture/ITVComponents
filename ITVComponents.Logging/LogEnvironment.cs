using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;

namespace ITVComponents.Logging
{
    public static class LogEnvironment
    {
        /// <summary>
        /// holds a list of connected loggers for each active thread
        /// </summary>
        private static ConcurrentDictionary<object, List<ILogTarget>> zoneLoggers = new ConcurrentDictionary<object, List<ILogTarget>>();

        /// <summary>
        /// holds a list of local initialization tickets that will be used to track initialization of loggers
        /// </summary>
        private static ThreadLocal<object> localLogginZone = new ThreadLocal<object>();

        /// <summary>
        /// Holds a callback that is used in the current thread to hook log-entries for a temporary time
        /// </summary>
        private static ThreadLocal<TemporaryLoggingHook> currentThreadHook  = new ThreadLocal<TemporaryLoggingHook>();

        /// <summary>
        /// a list of loggers that are invoked when a service or a component needs to log events
        /// </summary>
        private static List<ILogTarget> logTargets = new List<ILogTarget>();

        /// <summary>
        /// Indicates whether any Debug Target is capable for processing debug messages
        /// </summary>
        private static bool debugListening = false;

        /// <summary>
        /// Log targets whose last call failed. A failing target is reported to <see cref="Trace"/> once per outage, not
        /// once per message, and again when it works again.
        /// </summary>
        private static readonly ConcurrentDictionary<object, byte> failingTargets = new ConcurrentDictionary<object, byte>();

        /// <summary>
        /// Sets a Log-hook for the current thread without having to implement a LogWriter
        /// </summary>
        /// <param name="hook">the hook callback that will receive all log-messages</param>
        /// <returns>an idisposable instance that will remove the hook on disposal</returns>
        public static IDisposable SetTemporaryLogHook(TemporaryLoggingHook hook)
        {
            if (currentThreadHook.IsValueCreated && currentThreadHook.Value != null)
            {
                throw new InvalidOperationException("There already is a hook installed on the current thread");
            }

            currentThreadHook.Value = hook;
            return new LoggingHookResource();
        }

        /// <summary>
        /// Registers an initialization ticket under which new loggers will be registered
        /// </summary>
        /// <param name="ticket">the ticket under which to register loggers that are create inside the current thread</param>
        public static void OpenRegistrationTicket(object ticket)
        {
            localLogginZone.Value = ticket;
            zoneLoggers.AddOrUpdate(ticket, new List<ILogTarget>(), (o, l) => l);
        }

        /// <summary>
        /// Removes the ticket from the list of regstered ticket-loggers
        /// </summary>
        /// <param name="ticket">the ticket that has possible loggers registered</param>
        public static void DisposeRegistrationTicket(object ticket)
        {
            if (TryGetLoggingZone() == ticket)
            {
                List<ILogTarget> loggers;
                try
                {
                    localLogginZone.Value = null;
                }
                catch (Exception ex)
                {
                    // Not through LogEvent: the logging infrastructure itself is the one failing here.
                    Trace.TraceError($"Failed to reset the logging zone of the current thread: {ex}");
                }

                if (zoneLoggers.TryRemove(ticket, out loggers))
                {
                    lock (loggers)
                    {
                        loggers.Clear();
                    }
                }
            }
        }

        /// <summary>
        /// Logs a debug-only message to a specific target
        /// </summary>
        /// <param name="loggerTicket">a unique object under which a set of loggers was registered</param>
        /// <param name="eventText">the event text to log</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">the logger-context that can be used to filter messages by context</param>
        public static void LogDebugEvent(object loggerTicket, string eventText, int severity, string context)
        {
            if (debugListening)
            {
                OnAllLoggers(true, loggerTicket, eventText, severity, context);
            }
        }

        /// <summary>
        /// Logs a debug-message to a specific target
        /// </summary>
        /// <param name="eventText">the event text to log</param>
        /// <param name="severity">the severity of the event</param>
        public static void LogDebugEvent(string eventText, LogSeverity severity)
        {
            LogDebugEvent(TryGetLoggingZone(), eventText, (int) severity, null);
        }

        /// <summary>
        /// Logs a debug-message to a specific target
        /// </summary>
        /// <param name="eventText">the event text to log</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">the logger-context that can be used to filter messages by context</param>
        public static void LogDebugEvent(string eventText, LogSeverity severity, string context)
        {
            LogDebugEvent(TryGetLoggingZone(), eventText, (int)severity, context);
        }

        /// <summary>
        /// Logs a message to a specific target
        /// </summary>
        /// <param name="eventText">the event text to log</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">the logger-context that can be used to filter messages by context</param>
        public static void LogEvent(string eventText, int severity, string context)
        {
            LogEvent(null, eventText, severity, context);
        }

        /// <summary>
        /// Logs a message to a specific target
        /// </summary>
        /// <param name="loggerTicket">a unique object under which a set of loggers was registered</param>
        /// <param name="eventText">the event text to log</param>
        /// <param name="severity">the severity of the event</param>
        public static void LogEvent(string eventText, int severity)
        {
            LogEvent(TryGetLoggingZone(), eventText, severity, null);
        }

        /// <summary>
        /// Logs a message to a specific target
        /// </summary>
        /// <param name="eventText">the event text to log</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">the logger-context that can be used to filter messages by context</param>
        public static void LogEvent(string eventText, LogSeverity severity, string context)
        {
            LogEvent(TryGetLoggingZone(), eventText, (int) severity, context);
        }

        /// <summary>
        /// Logs a message to a specific target
        /// </summary>
        /// <param name="eventText">the event text to log</param>
        /// <param name="severity">the severity of the event</param>
        public static void LogEvent(string eventText, LogSeverity severity)
        {
            LogEvent(TryGetLoggingZone(), eventText, (int) severity, null);
        }

        /// <summary>
        /// Registers a logger in this logging environment
        /// </summary>
        /// <param name="target">the target logger on which to forward log events</param>
        public static void RegisterLogTarget(ILogTarget target)
        {
            List<ILogTarget> targetList = null;
            var logZone = TryGetLoggingZone();
            if (logZone == null)
            {
                targetList = logTargets;
            }
            else
            {
                if (!zoneLoggers.TryGetValue(logZone, out targetList))
                {
                    targetList = logTargets;
                }
            }

            lock (targetList)
            {
                targetList.Add(target);
                void DispHandler(object sender, EventArgs args)
                {
                    lock (targetList)
                    {
                        targetList.Remove(target);

                    }

                    ((ILogTarget)sender).Disposed -= DispHandler;
                };

                target.Disposed += DispHandler;
            }
        }

        /// <summary>
        /// Enables Debug-Messages in the Log-Environment
        /// </summary>
        public static void EnableDebugMessages()
        {
            debugListening = true;
        }

        /// <summary>
        /// Disables Debug-Messages in the Log-Environment
        /// </summary>
        public static void DisableDebugMessages()
        {
            debugListening = false;
        }

        /// <summary>
        /// Gets the closest severity for a specific severity level
        /// </summary>
        /// <param name="severity">the provided severity level</param>
        /// <returns>the severitylevel for which to get the equivalent default-level</returns>
        public static LogSeverity GetClosestSeverity(int severity)
        {
            if (severity < (int)LogSeverity.Warning)
                return LogSeverity.Report;
            if (severity < (int) LogSeverity.Error)
                return LogSeverity.Warning;
            return LogSeverity.Error;
        }

        private static string AutoLogContext(bool forDebug = false)
        {
            var mt = typeof(LogEnvironment);
            for (var i = !forDebug ? 3 : 2; i <= 7; i++)
            {
                var frame = new StackFrame(i, false);
                var mth = frame.GetMethod();
                if (mth != null)
                {
                    var tp = mth.DeclaringType;
                    if (tp != mt)
                    {
                        return $"{tp?.FullName}::{mth.Name}";
                    }
                }
            }

            return "--unknown--";
        }

        /// <summary>
        /// Logs a message to a specific target
        /// </summary>
        /// <param name="loggerTicket">a unique object under which a set of loggers was registered</param>
        /// <param name="eventText">the event text to log</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">the logger-context that can be used to filter messages by context</param>
        private static void LogEvent(object loggerTicket, string eventText, int severity, string context)
        {
            OnAllLoggers(false, loggerTicket, eventText, severity, context);
        }

        private static void OnAllLoggers(bool isDebugMessage, object loggerTicket, string eventText, int severity, string context)
        {
            Debug.WriteLine(eventText);
            try
            {
                ILogTarget[] targets;
                lock (logTargets)
                {
                    targets = logTargets.ToArray();
                }
                if (string.IsNullOrEmpty(context))
                {
                    context = AutoLogContext(isDebugMessage);
                }

                // Each target on its own: one that throws must not take the message from the targets behind it.
                foreach (var n in targets.Where(l => !isDebugMessage || ((l as IDebugLogTarget)?.EnableDebugMessages ?? false)))
                {
                    LogToTarget(n, eventText, severity, context);
                }

                if (loggerTicket != null)
                {
                    List<ILogTarget> loggers;
                    if (zoneLoggers.TryGetValue(loggerTicket, out loggers))
                    {
                        lock (loggers)
                        {
                            targets = loggers.ToArray();
                        }

                        foreach (var n in targets.Where(l => !isDebugMessage || ((l as IDebugLogTarget)?.EnableDebugMessages ?? false)))
                        {
                            LogToTarget(n, eventText, severity, context);
                        }
                    }
                }

                if (currentThreadHook.IsValueCreated && currentThreadHook.Value != null)
                {
                    var hook = currentThreadHook.Value;
                    try
                    {
                        hook(isDebugMessage, eventText, GetClosestSeverity(severity), context);
                        ReportTargetRecovered(hook, "the temporary logging hook");
                    }
                    catch (Exception ex)
                    {
                        ReportTargetFailure(hook, "the temporary logging hook", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                // Not through LogEvent - that would end up here again.
                Trace.TraceError($"Failed to dispatch a log message to the log targets: {ex}");
            }
        }

        /// <summary>
        /// Hands a message to one log target. A target that throws is reported to <see cref="Trace"/> (once per outage)
        /// and does not keep the message from the other targets.
        /// </summary>
        private static void LogToTarget(ILogTarget target, string eventText, int severity, string context)
        {
            try
            {
                target.LogEvent(eventText, severity, context);
                ReportTargetRecovered(target, target.GetType().FullName);
            }
            catch (Exception ex)
            {
                ReportTargetFailure(target, target.GetType().FullName, ex);
            }
        }

        private static void ReportTargetFailure(object target, string name, Exception ex)
        {
            if (failingTargets.TryAdd(target, 0))
            {
                Trace.TraceError($"Log target {name} failed; messages to it are lost until it works again. The other targets keep logging: {ex}");
            }
        }

        private static void ReportTargetRecovered(object target, string name)
        {
            if (!failingTargets.IsEmpty && failingTargets.TryRemove(target, out _))
            {
                Trace.TraceInformation($"Log target {name} works again.");
            }
        }

        private static object TryGetLoggingZone()
        {
            try
            {
                if (localLogginZone.IsValueCreated && localLogginZone.Value != null)
                {
                    return localLogginZone.Value;
                }
            }
            catch (Exception ex)
            {
                // Not through LogEvent: it asks for the logging zone itself.
                Trace.TraceError($"Failed to read the logging zone of the current thread: {ex}");
            }

            return null;
        }

        /// <summary>
        /// helper class that is used to control local logging-hooks
        /// </summary>
        private class LoggingHookResource : IDisposable
        {
            /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
            /// <filterpriority>2</filterpriority>
            public void Dispose()
            {
                currentThreadHook.Value = null;
            }
        }
    }

    /// <summary>
    /// Callback that can be used to hook the logEnvironment LogEvent method temporarily
    /// </summary>
    /// <param name="isDebug">indicates whether the following message is for debug purposes only</param>
    /// <param name="eventText">the event-text that is being logged</param>
    /// <param name="severity">the log-severity of the event</param>
    /// <param name="context">the logging-context</param>
    public delegate void TemporaryLoggingHook(bool isDebug, string eventText, LogSeverity severity, string context);

    public enum LogSeverity
    {
        /// <summary>
        /// Default messages. All Messages that have a severity higher than 0
        /// </summary>
        Report = 0,

        /// <summary>
        /// Warning messages. All Messages that have a severity higher than 30
        /// </summary>
        Warning = 30,

        /// <summary>
        /// Error messages. All Messages that have a severity higher than 60
        /// </summary>
        Error = 60
    }
}
