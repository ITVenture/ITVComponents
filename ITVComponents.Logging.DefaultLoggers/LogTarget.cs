using System;
using System.Collections.Generic;
using System.Threading;
using ITVComponents.Plugins;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;

namespace ITVComponents.Logging.DefaultLoggers
{
    public abstract class LogTarget:IPlugin, IDebugLogTarget
    {
        /// <summary>
        /// indicates whether this logTarget is enabled
        /// </summary>
        private bool enabled;

        private readonly bool useSynchronizedWriting;

        /// <summary>
        /// Synchronizes log-writes for all threads on this LogTarget.
        /// </summary>
        private object syncLock = new();

        /// <summary>
        /// Initializes a new instance of the LogTarget class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of the logger</param>
        /// <param name="maxSeverity">the maximum severity of the logger</param>
        /// <param name="contextFilter">A n Expression that can be used to filter the context of provided messages</param>
        /// <param name="initialStatus">the initial status of this logger</param>
        /// <param name="debugEnabled">indicates whether to log debug-messages</param>
        /// <param name="useSynchronizedWriting">indicates whether Log-actions should must explicitly be thread-save</param>
        protected LogTarget(int minSeverity, int maxSeverity, string contextFilter, bool initialStatus, bool debugEnabled, bool useSynchronizedWriting) : this()
        {
            MinSeverity = minSeverity;
            MaxSeverity = maxSeverity;
            enabled = initialStatus;
            this.useSynchronizedWriting = useSynchronizedWriting;
            ContextFilter = contextFilter;
            EnableDebugMessages = debugEnabled;
            if (debugEnabled)
            {
                LogEnvironment.EnableDebugMessages();
            }
        }

        /// <summary>
        /// Initializes a new instance of the LogTarget class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of the logger</param>
        /// <param name="maxSeverity">the maximum severity of the logger</param>
        /// <param name="initialStatus">the initial status of this logger</param>
        /// <param name="useSynchronizedWriting">indicates whether Log-actions should must explicitly be thread-save</param>
        protected LogTarget(int minSeverity, int maxSeverity, bool initialStatus, bool useSynchronizedWriting)
            : this(minSeverity, maxSeverity, null, initialStatus, false, useSynchronizedWriting)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LogTarget class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of the logger</param>
        /// <param name="maxSeverity">the maximum severity of the logger</param>
        /// <param name="contextFilter">A n Expression that can be used to filter the context of provided messages</param>
        /// <param name="initialStatus">the initial status of this logger</param>
        /// <param name="useSynchronizedWriting">indicates whether Log-actions should must explicitly be thread-save</param>
        protected LogTarget(LogSeverity minSeverity, LogSeverity maxSeverity, string contextFilter, bool initialStatus, bool useSynchronizedWriting)
            : this((int)minSeverity, (int)maxSeverity + 29, contextFilter, initialStatus, false, useSynchronizedWriting)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LogTarget class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of the logger</param>
        /// <param name="maxSeverity">the maximum severity of the logger</param>
        /// <param name="contextFilter">A n Expression that can be used to filter the context of provided messages</param>
        /// <param name="initialStatus">the initial status of this logger</param>
        /// <param name="debugEnabled">indicates whether to log debug-messages</param>
        /// <param name="useSynchronizedWriting">indicates whether Log-actions should must explicitly be thread-save</param>
        protected LogTarget(LogSeverity minSeverity, LogSeverity maxSeverity, string contextFilter, bool initialStatus, bool debugEnabled, bool useSynchronizedWriting)
            : this((int)minSeverity, (int)maxSeverity + 29, contextFilter, initialStatus, debugEnabled, useSynchronizedWriting)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LogTarget class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of the logger</param>
        /// <param name="maxSeverity">the maximum severity of the logger</param>
        /// <param name="initialStatus">the initial status of this logger</param>
        /// <param name="useSynchronizedWriting">indicates whether Log-actions should must explicitly be thread-save</param>
        protected LogTarget(LogSeverity minSeverity, LogSeverity maxSeverity, bool initialStatus, bool useSynchronizedWriting)
            : this((int) minSeverity, (int) maxSeverity + 29, null, initialStatus, false, useSynchronizedWriting)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LogTarget class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of the logger</param>
        /// <param name="maxSeverity">the maximum severity of the logger</param>
        /// <param name="contextFilter">A n Expression that can be used to filter the context of provided messages</param>
        /// <param name="initialStatus">the initial status of this logger</param>
        /// <param name="useSynchronizedWriting">indicates whether Log-actions should must explicitly be thread-save</param>
        protected LogTarget(int minSeverity, int maxSeverity, string contextFilter, bool initialStatus, bool useSynchronizedWriting) :
            this(minSeverity, maxSeverity, contextFilter, initialStatus, false, useSynchronizedWriting)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LogTarget class
        /// </summary>
        protected LogTarget()
        {
            LogEnvironment.RegisterLogTarget(this);
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this LogWriter is active
        /// </summary>
        public bool Enabled
        {
            get { return enabled && IsEnabled(); }
            set { enabled = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether Debug-Messages are processed by this LogTarget instance
        /// </summary>
        public bool EnableDebugMessages { get; set; }

        /// <summary>
        /// Gets or sets the minimal Severity to log with this logger
        /// </summary>
        public int MinSeverity { get; set; }

        /// <summary>
        /// Gets or sets the maximum severity to log with this logger
        /// </summary>
        public int MaxSeverity { get; set; }

        /// <summary>
        /// Gets or sets a filter that can be used to filter messages that were generated by a Log-source
        /// </summary>
        public string ContextFilter { get; set; }

        /// <summary>
        /// Logs an event into this Log-Target
        /// </summary>
        /// <param name="eventText">the event text</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">the logger-context that can be used to filter messages by context</param>
        public void LogEvent(string eventText, int severity, string context)
        {
            bool b = useSynchronizedWriting;
            if (b)
            {
                Monitor.Enter(syncLock);
            }

            try
            {
                if (Enabled && Loggable(severity) && Loggable(context))
                {
                    Log(eventText, severity, context);
                }
            }
            finally
            {
                if (b)
                {
                    Monitor.Exit(syncLock);
                }
            }
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Logs an event to this Log-Target
        /// </summary>
        /// <param name="eventText">the event-text</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">provides additional information about the logging-context in which the message was generated</param>
        protected abstract void Log(string eventText, int severity, string context);

        /// <summary>
        /// Can be used to provide further information about the Status of this logger
        /// </summary>
        /// <returns>a value indicating whether the logger is ready to perform loggin operations</returns>
        protected virtual bool IsEnabled()
        {
            return true;
        }

        /// <summary>
        /// Raises the disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Checks whether to log events of a specific severity
        /// </summary>
        /// <param name="severity">the severity that is provided by a message that is being logged</param>
        /// <returns>a value indicating whether to log the message with the specified severity</returns>
        private bool Loggable(int severity)
        {
            return ((MinSeverity <= severity || MinSeverity == -1) && (MaxSeverity >= severity || MaxSeverity == -1));
        }

        /// <summary>
        /// Indicates whether the current Log-string needs to be processed by this Logger instance
        /// </summary>
        /// <param name="contextString">the context string that was provided by the log-sender</param>
        /// <returns>a value indicating whether the current message is supposed to be processed using this logger</returns>
        private bool Loggable(string contextString)
        {
            bool retVal = false;
            bool isContextMessage = !string.IsNullOrEmpty(contextString);
            bool isContextLogger = !string.IsNullOrEmpty(ContextFilter);
            retVal = isContextMessage == isContextLogger;
            if (isContextMessage && retVal)
            {
                retVal = (bool) ExpressionParser.Parse(ContextFilter,
                    new Dictionary<string, object> {{"context", contextString}}, a => { DefaultCallbacks.PrepareDefaultCallbacks(a.Scope, a.ReplSession); });
            }

            return retVal;
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
