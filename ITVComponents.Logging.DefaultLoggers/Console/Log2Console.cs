using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Logging.DefaultLoggers.Console
{
    public class Log2Console:LogTarget
    {
        /// <summary>
        /// a locker object that is used to ensure that the color of the console output is always appropriate
        /// </summary>
        private object consoleLock = new object();

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(LogSeverity minSeverity, LogSeverity maxSeverity, bool enabled)
            : base(minSeverity, maxSeverity, enabled)
        {   
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(int minSeverity, int maxSeverity, bool enabled)
            : base(minSeverity, maxSeverity, enabled)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="contextFilter">an expression that canbge used to filter Messages before they are processed</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(LogSeverity minSeverity, LogSeverity maxSeverity, string contextFilter, bool enabled)
            : base(minSeverity, maxSeverity, contextFilter, enabled)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="contextFilter">an expression that canbge used to filter Messages before they are processed</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(int minSeverity, int maxSeverity, string contextFilter, bool enabled)
            : base(minSeverity, maxSeverity, contextFilter, enabled)
        {
        }

        //--

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(LogSeverity minSeverity, LogSeverity maxSeverity, bool enabled, bool debugEnabled)
            : base(minSeverity, maxSeverity, null, enabled, debugEnabled)
        {   
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(int minSeverity, int maxSeverity, bool enabled, bool debugEnabled)
            : base(minSeverity, maxSeverity, null, enabled, debugEnabled)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="contextFilter">an expression that canbge used to filter Messages before they are processed</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(LogSeverity minSeverity, LogSeverity maxSeverity, string contextFilter, bool enabled, bool debugEnabled)
            : base(minSeverity, maxSeverity, contextFilter, enabled, debugEnabled)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="contextFilter">an expression that canbge used to filter Messages before they are processed</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(int minSeverity, int maxSeverity, string contextFilter, bool enabled, bool debugEnabled)
            : base(minSeverity, maxSeverity, contextFilter, enabled, debugEnabled)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether this LogWriter is active
        /// </summary>
        protected override bool IsEnabled()
        {
            return Environment.UserInteractive; 
        }

        /// <summary>
        /// Logs an event to this Log-Target
        /// </summary>
        /// <param name="eventText">the event-text</param>
        /// <param name="severity">the severity of the event</param>
        /// <param name="context">provides additional information about the logging-context in which the message was generated</param>
        protected override void Log(string eventText, int severity, string context)
        {
            lock (consoleLock)
            {
                LogSeverity effective = LogEnvironment.GetClosestSeverity(severity);
                switch (effective)
                {
                    case LogSeverity.Error:
                        {
                            System.Console.ForegroundColor = ConsoleColor.Red;
                            break;
                        }
                    case LogSeverity.Warning:
                        {
                            System.Console.ForegroundColor = ConsoleColor.Yellow;
                            break;
                        }
                    case LogSeverity.Report:
                        {
                            System.Console.ForegroundColor = ConsoleColor.Gray;
                            break;
                        }
                }

                System.Console.WriteLine("{0} -> {1}", context, eventText);
                System.Console.ForegroundColor = ConsoleColor.Gray;
            }
        }
    }
}
