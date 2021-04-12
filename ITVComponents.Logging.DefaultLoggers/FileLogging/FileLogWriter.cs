using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ITVComponents.Threading;

namespace ITVComponents.Logging.DefaultLoggers.FileLogging
{
    public class FileLogWriter:LogTarget, ILogDownloadProvider
    {
        /// <summary>
        /// the maximum number of archived files
        /// </summary>
        private int archiveCount;

        /// <summary>
        /// the maximum number of lines a logfile can contain
        /// </summary>
        private long maxLogSize;

        /// <summary>
        /// the current characterCount in the logFile
        /// </summary>
        private long currentSize = 0;

        /// <summary>
        /// the timeout (in seconds) after which the current stream is closed
        /// </summary>
        private int keeperTimeout = 600;

        /// <summary>
        /// Checks whether there is need to close the stream
        /// </summary>
        private Timer streamKeeper;

        /// <summary>
        /// the fileName of the Log-File
        /// </summary>
        private string logName;

        /// <summary>
        /// the period time (in seconds) with which the timer checks whether the logwriter can be considered abandoned
        /// </summary>
        private int keeperTickTime = 60;

        /// <summary>
        /// the last usage of the inner stream
        /// </summary>
        private DateTime lastUsage = DateTime.Now;

        /// <summary>
        /// the stream that is used to dump the log into a file
        /// </summary>
        private FileStream innerStream;

        /// <summary>
        /// synch object that is used to log one event per line
        /// </summary>
        private object syncher = new object();

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="archiveCount">the number of archived logs</param>
        /// <param name="maxLogSize">the maximum size of logs</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the minimal severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        /// <param name="contextFilter">an expression that will be used to filter logMessages before they are logged</param>
        public FileLogWriter(string logName, int archiveCount, int maxLogSize, bool initialLogStatus, int minSeverity,
                             int maxSeverity, string contextFilter)
            : this(logName, archiveCount, maxLogSize, initialLogStatus, minSeverity, maxSeverity, contextFilter, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="archiveCount">the number of archived logs</param>
        /// <param name="maxLogSize">the maximum size of logs</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the minimal severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        public FileLogWriter(string logName, int archiveCount, int maxLogSize, bool initialLogStatus, int minSeverity,
                             int maxSeverity)
            : this(logName,archiveCount,maxLogSize,initialLogStatus, minSeverity, maxSeverity,false)
        {
        }


        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="archiveCount">the number of archived logs</param>
        /// <param name="maxLogSize">the maximum size of logs</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the initial severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        public FileLogWriter(string logName, int archiveCount, int maxLogSize, bool initialLogStatus,
                             LogSeverity minSeverity, LogSeverity maxSeverity)
            : this(logName,archiveCount,maxLogSize, initialLogStatus, minSeverity,maxSeverity, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="archiveCount">the number of archived logs</param>
        /// <param name="maxLogSize">the maximum size of logs</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="severity">the initial severity of this logger</param>
        public FileLogWriter(string logName, int archiveCount, int maxLogSize, bool initialLogStatus, int severity)
            : this(logName, archiveCount, maxLogSize, initialLogStatus, severity, -1, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="archiveCount">the number of archived logs</param>
        /// <param name="maxLogSize">the maximum size of logs</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="severity">the initial severity of this logger</param>
        public FileLogWriter(string logName, int archiveCount, int maxLogSize, bool initialLogStatus,
                             LogSeverity severity)
            : this(logName, archiveCount, maxLogSize, initialLogStatus, (int) severity, -1, false)
        {
        }

        //--

                /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="archiveCount">the number of archived logs</param>
        /// <param name="maxLogSize">the maximum size of logs</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the minimal severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        /// <param name="contextFilter">an expression that will be used to filter logMessages before they are logged</param>
        public FileLogWriter(string logName, int archiveCount, int maxLogSize, bool initialLogStatus, int minSeverity,
                             int maxSeverity, string contextFilter, bool debugEnabled)
            : base(minSeverity, maxSeverity, contextFilter, initialLogStatus, debugEnabled)
        {
            InitializeLog(logName, archiveCount, maxLogSize);
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="archiveCount">the number of archived logs</param>
        /// <param name="maxLogSize">the maximum size of logs</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the minimal severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        public FileLogWriter(string logName, int archiveCount, int maxLogSize, bool initialLogStatus, int minSeverity,
                             int maxSeverity, bool debugEnabled)
            : base(minSeverity, maxSeverity, null, initialLogStatus, debugEnabled)
        {
            InitializeLog(logName, archiveCount, maxLogSize);
        }


        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="archiveCount">the number of archived logs</param>
        /// <param name="maxLogSize">the maximum size of logs</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="minSeverity">the initial severity of this logger</param>
        /// <param name="maxSeverity">the maximal severity of this logger</param>
        public FileLogWriter(string logName, int archiveCount, int maxLogSize, bool initialLogStatus,
                             LogSeverity minSeverity, LogSeverity maxSeverity, bool debugEnabled)
            : base(minSeverity, maxSeverity,null, initialLogStatus,debugEnabled)
        {
            InitializeLog(logName, archiveCount, maxLogSize);
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="archiveCount">the number of archived logs</param>
        /// <param name="maxLogSize">the maximum size of logs</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="severity">the initial severity of this logger</param>
        public FileLogWriter(string logName, int archiveCount, int maxLogSize, bool initialLogStatus, int severity, bool debugEnabled)
            : this(logName, archiveCount, maxLogSize, initialLogStatus, severity, -1, debugEnabled)
        {
        }

        /// <summary>
        /// Initializes a new instance of the FileLogWriter class
        /// </summary>
        /// <param name="logName">the full-qualified location of the logfile</param>
        /// <param name="archiveCount">the number of archived logs</param>
        /// <param name="maxLogSize">the maximum size of logs</param>
        /// <param name="initialLogStatus">the initial enabled state of this logger</param>
        /// <param name="severity">the initial severity of this logger</param>
        public FileLogWriter(string logName, int archiveCount, int maxLogSize, bool initialLogStatus,
                             LogSeverity severity, bool debugEnabled)
            : this(logName, archiveCount, maxLogSize, initialLogStatus, (int) severity, -1, debugEnabled)
        {
        }

        /// <summary>
        /// Downloads the current log
        /// </summary>
        /// <returns>a byte-array containing the current log</returns>
        public byte[] DownloadCurrentLog()
        {
            if (File.Exists(logName))
            {
                return File.ReadAllBytes(logName);
            }

            return null;
        }

        /// <summary>
        /// Downloads a hist-file
        /// </summary>
        /// <param name="name">the name of the hist-file</param>
        /// <returns>a byte-array containing the requested log</returns>
        public byte[] DownloadHistory(string name)
        {
            var file = GetHistNamesRaw().FirstOrDefault(n => File.Exists(n) && Path.GetFileName(n).Equals(name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(file))
            {
                return File.ReadAllBytes(file);
            }

            return null;
        }

        /// <summary>
        /// Gets the hist-names that are currently available
        /// </summary>
        /// <returns>a list of log-names that are available for download</returns>
        public string[] GetHistNames()
        {
            return (from t in GetHistNamesRaw() where File.Exists(t) select Path.GetFileName(t)).ToArray();
        }

        /// <summary>
        /// Logs an event to this Log-Target
        /// </summary>
        /// <param name="eventText">the event-text</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">provides additional information about the logging-context in which the message was generated</param>
        protected override void Log(string eventText, int severity, string context)
        {
            lastUsage = DateTime.Now;
            string eventMessage = string.Format(@"{0:dd.MM.yyyy HH:mm:ss} {1:000} {2,10} {3} {4}
", DateTime.Now, severity, LogEnvironment.GetClosestSeverity(severity),context,
                                                eventText);
            lock (syncher)
            {
                if (currentSize > maxLogSize)
                {
                    try
                    {
                        SwitchLog();
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine(ex);
                    }
                }

                if (innerStream == null)
                {
                    OpenStream();
                }

                WriteMessage(eventMessage);
            }
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public override void Dispose()
        {
            ManualResetEvent resetter = new ManualResetEvent(false);
            streamKeeper.Dispose(resetter);
            resetter.WaitOne();
            if (innerStream != null)
            {
                innerStream.Flush();
                innerStream.Dispose();
            }

            base.Dispose();
        }

        /// <summary>
        /// Writes a message into the Stream
        /// </summary>
        /// <param name="eventMessage">the message to log</param>
        private void WriteMessage(string eventMessage)
        {
            byte[] msg = Encoding.Default.GetBytes(eventMessage);
            currentSize += msg.Length;
            innerStream.Write(msg, 0, msg.Length);
            innerStream.Flush();
        }

        /// <summary>
        /// Checks whether this logwriter stream can be considered abandoned and closes the underlying filesystem stream when required
        /// </summary>
        /// <param name="state">the object state of the timer</param>
        private void Abandon(object state)
        {
            streamKeeper.Change(Timeout.Infinite, Timeout.Infinite);
            state.LocalOwner(state.ToString());
            try
            {
                lock (innerStream)
                {
                    if (innerStream != null)
                    {
                        DateTime now = DateTime.Now;
                        if (now.Subtract(lastUsage).TotalSeconds > keeperTimeout)
                        {
                            innerStream.Flush();
                            innerStream.Dispose();
                            innerStream = null;
                        }
                    }
                }
            }
            finally
            {
                streamKeeper.Change(keeperTickTime * 1000, keeperTickTime * 1000);
                state.LocalOwner(null);
            }
        }

        /// <summary>
        /// Initializes this logger
        /// </summary>
        /// <param name="logName">the full qualified path to the logfile</param>
        /// <param name="archiveCount">the maximum number of archive files</param>
        /// <param name="maxLogSize">the maximum size of a longfile </param>
        private void InitializeLog(string logName, int archiveCount, int maxLogSize)
        {
            streamKeeper = new Timer(Abandon, string.Format("::{0}::", GetHashCode()), Timeout.Infinite,
                                     Timeout.Infinite);
            this.logName = logName;
            this.archiveCount = archiveCount;
            this.maxLogSize = maxLogSize;
            OpenStream();
            WriteMessage("--Log initialized--\r\n");
        }

        /// <summary>
        /// Switches to the next logfile
        /// </summary>
        private void SwitchLog()
        {
            if (innerStream != null)
            {
                innerStream.Flush();
                innerStream.Dispose();
            }

            try
            {
                var fileNames = GetHistNamesRaw();

                for (int i = fileNames.Length - 1; i >= 0; i--)
                {
                    if (File.Exists(fileNames[i]))
                    {
                        File.Delete(fileNames[i]);
                    }

                    if (i > 0 && File.Exists(fileNames[i - 1]))
                    {
                        File.Move(fileNames[i - 1], fileNames[i]);
                    }
                }

                File.Move(logName, fileNames[0]);
            }
            finally
            {
                OpenStream();
            }
        }

        /// <summary>
        /// Gets a list of possible hist-names for this logger
        /// </summary>
        /// <returns>a list of calculated history-files</returns>
        private string[] GetHistNamesRaw()
        {
            string[] fileNames = new string[archiveCount];
            for (int i = 0; i < fileNames.Length; i++)
            {
                string basePath = logName.Substring(0, logName.LastIndexOf("."));
                string ending = logName.Substring(logName.LastIndexOf(".") + 1);
                fileNames[i] = string.Format(@"{0}_{1}.{2}", basePath, i, ending);
            }

            return fileNames;
        }

        /// <summary>
        /// Opens the target stream
        /// </summary>
        private void OpenStream()
        {
            this.innerStream = new FileStream(logName, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                                                      FileShare.Read, 10000, FileOptions.WriteThrough);
            this.innerStream.Seek(0, SeekOrigin.End);
            currentSize = innerStream.Position;
        }
    }
}
