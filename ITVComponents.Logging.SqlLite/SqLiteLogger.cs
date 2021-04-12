using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.SqLite;
using ITVComponents.Logging.DefaultLoggers;
using ITVComponents.Logging.SqlLite.SqLite;
using ITVComponents.Plugins;

namespace ITVComponents.Logging.SqlLite
{
    public class SqLiteLogger:LogTarget
    {
        private string logName;

        private string currentName;

        private IDbWrapper database;

        private ManualResetEvent waitForStartup = new ManualResetEvent(false);

        private ManualResetEvent stopEvent = new ManualResetEvent(false);

        private ManualResetEvent stoppedEvent = new ManualResetEvent(false);

        private ManualResetEvent workEvent = new ManualResetEvent(false);

        private ConcurrentQueue<LogPortion> logQueue = new ConcurrentQueue<LogPortion>();

        private LogDbInitializer initializer = new LogDbInitializer();

        private Thread writerThread;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the minimal severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        /// <param name="contextFilter">an expression that will be used to filter logMessages before they are logged</param>
        public SqLiteLogger(string logName, bool initialLogStatus, int minSeverity,
                             int maxSeverity, string contextFilter)
            : base(minSeverity, maxSeverity, contextFilter, initialLogStatus)
        {
            this.logName = logName;
            InitWriter();
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the minimal severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        public SqLiteLogger(string logName, bool initialLogStatus, int minSeverity,
                             int maxSeverity)
            : base(minSeverity, maxSeverity, initialLogStatus)
        {
            this.logName = logName;
            InitWriter();
        }


        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the initial severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        public SqLiteLogger(string logName, bool initialLogStatus,
                             LogSeverity minSeverity, LogSeverity maxSeverity)
            : base(minSeverity, maxSeverity, initialLogStatus)
        {
            this.logName = logName;
            InitWriter();
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="severity">the initial severity of this logger</param>
        public SqLiteLogger(string logName, bool initialLogStatus, int severity)
            : this(logName, initialLogStatus, severity, -1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="severity">the initial severity of this logger</param>
        public SqLiteLogger(string logName, bool initialLogStatus,
                             LogSeverity severity)
            : this(logName, initialLogStatus, (int)severity, -1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the minimal severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        /// <param name="contextFilter">an expression that will be used to filter logMessages before they are logged</param>
        /// <param name="debugEnabled">indicates whether to log debug-messages</param>
        public SqLiteLogger(string logName, bool initialLogStatus, int minSeverity,
                             int maxSeverity, string contextFilter, bool debugEnabled)
            : base(minSeverity, maxSeverity, contextFilter, initialLogStatus, debugEnabled)
        {
            this.logName = logName;
            InitWriter();
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the minimal severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        /// <param name="debugEnabled">indicates whether to log debug-messages</param>
        public SqLiteLogger(string logName, bool initialLogStatus, int minSeverity,
                             int maxSeverity, bool debugEnabled)
            : base(minSeverity, maxSeverity, null, initialLogStatus,debugEnabled)
        {
            this.logName = logName;
            InitWriter();
        }


        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the initial severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        /// <param name="debugEnabled">indicates whether to log debug-messages</param>
        public SqLiteLogger(string logName, bool initialLogStatus,
                             LogSeverity minSeverity, LogSeverity maxSeverity, bool debugEnabled)
            : base(minSeverity, maxSeverity, null, initialLogStatus, debugEnabled)
        {
            this.logName = logName;
            InitWriter();
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="severity">the initial severity of this logger</param>
        /// <param name="debugEnabled">indicates whether to log debug-messages</param>
        public SqLiteLogger(string logName, bool initialLogStatus, int severity, bool debugEnabled)
            : this(logName, initialLogStatus, severity, -1, debugEnabled)
        {
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="severity">the initial severity of this logger</param>
        /// <param name="debugEnabled">indicates whether to log debug-messages</param>
        public SqLiteLogger(string logName, bool initialLogStatus,
                             LogSeverity severity, bool debugEnabled)
            : this(logName, initialLogStatus, (int)severity, -1, debugEnabled)
        {
        }

        /// <summary>
        /// Logs an event to this Log-Target
        /// </summary>
        /// <param name="eventText">the event-text</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">provides additional information about the logging-context in which the message was generated</param>
        protected override void Log(string eventText, int severity, string context)
        {
            var portion = new LogPortion
            {
                Context = context,
                EventText = eventText,
                Severity = severity,
                EventTime = DateTime.Now
            };

            logQueue.Enqueue(portion);
            workEvent.Set();
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public override void Dispose()
        {
            base.Dispose();
            stopEvent.Set();
            stoppedEvent.WaitOne();
        }

        /// <summary>
        /// Checks whether the log should be re-initialized before loggint events in it
        /// </summary>
        private void CheckDb()
        {
            DateTime now = DateTime.Now;
            var desiredName = initializer.GetPreciseDatabaseName(logName);
            bool reOpen = string.IsNullOrEmpty(currentName) || currentName != desiredName;
            if (reOpen)
            {
                if (database != null)
                {
                    database.Dispose();
                    database = null;
                }

                database = new SqLiteWrapper(logName, 0, initializer);
                currentName = desiredName;
            }
        }

        /// <summary>
        /// Writes a single log item
        /// </summary>
        /// <param name="item"></param>
        private void WriteItem(LogPortion item)
        {
            CheckDb();
            database.ExecuteCommand(
                "Insert into Log (EventTime, Severity, EventContext, EventText) values ($eventTime, $severity, $eventContext, $eventText)",
                database.GetParameter("eventTime", item.EventTime),
                database.GetParameter("severity", item.Severity),
                database.GetParameter("eventContext", item.Context),
                database.GetParameter("eventText", item.EventText));
        }

        /// <summary>
        /// Writes events
        /// </summary>
        private void WriterLoop()
        {
            bool initial = true;
            while (!stopEvent.WaitOne(50))
            {
                if (initial)
                {
                    waitForStartup.Set();
                    initial = false;
                }

                workEvent.WaitOne(15000);
                try
                {
                    LogPortion logItem;
                    while (logQueue.TryDequeue(out logItem))
                    {
                        WriteItem(logItem);
                    }
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogDebugEvent($"Error during Log-write process: {ex}",LogSeverity.Error);
                }
                finally
                {
                    workEvent.Reset();
                }
            }

            stoppedEvent.Set();
        }

        /// <summary>
        /// Initializes the writer loop
        /// </summary>
        private void InitWriter()
        {
            writerThread = new Thread(WriterLoop);
            writerThread.Start();
            waitForStartup.WaitOne();
        }
    }
}
