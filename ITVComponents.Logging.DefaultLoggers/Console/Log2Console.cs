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
            : base(minSeverity, maxSeverity, enabled, true, false)
        {   
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(int minSeverity, int maxSeverity, bool enabled)
            : base(minSeverity, maxSeverity, enabled, true, false)
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
            : base(minSeverity, maxSeverity, contextFilter, enabled, true, false)
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
            : base(minSeverity, maxSeverity, contextFilter, enabled, true, false)
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
            : base(minSeverity, maxSeverity, null, enabled, debugEnabled, true, false)
        {   
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(int minSeverity, int maxSeverity, bool enabled, bool debugEnabled)
            : base(minSeverity, maxSeverity, null, enabled, debugEnabled, true, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(LogSeverity minSeverity, LogSeverity maxSeverity, bool enabled, bool debugEnabled, bool includeContext)
            : base(minSeverity, maxSeverity, null, enabled, debugEnabled, true, includeContext)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(int minSeverity, int maxSeverity, bool enabled, bool debugEnabled, bool includeContext)
            : base(minSeverity, maxSeverity, null, enabled, debugEnabled, true, includeContext)
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
            : base(minSeverity, maxSeverity, contextFilter, enabled, debugEnabled, true)
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
            : base(minSeverity, maxSeverity, contextFilter, enabled, debugEnabled, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="contextFilter">an expression that canbge used to filter Messages before they are processed</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(LogSeverity minSeverity, LogSeverity maxSeverity, string contextFilter, bool enabled, bool debugEnabled, bool includeContext)
            : base(minSeverity, maxSeverity, contextFilter, enabled, debugEnabled, true, includeContext)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Log2Console class
        /// </summary>
        /// <param name="minSeverity">the minimum severity of this logger</param>
        /// <param name="maxSeverity">the maximum severity of this logger</param>
        /// <param name="contextFilter">an expression that canbge used to filter Messages before they are processed</param>
        /// <param name="enabled">indicates whether the logger is active from beginning</param>
        public Log2Console(int minSeverity, int maxSeverity, string contextFilter, bool enabled, bool debugEnabled, bool includeContext)
            : base(minSeverity, maxSeverity, contextFilter, enabled, debugEnabled, true, includeContext)
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
            LogSeverity effective = LogEnvironment.GetClosestSeverity(severity);
            //System.Console.WriteLine("{0} -> {1}", context, eventText);
            System.Console.WriteLine($"{FormatLogLevel(effective),-12} - {DateTime.Now:g} - [{severity,-7}] - {context} - {eventText}");
        }

        private string FormatLogLevel(LogSeverity logLevel)
        {
            //Console.WriteLine("\x1b[38;5;161m{0}\x1b[0m", "Holdrio!");
            switch (logLevel)
            {
                case LogSeverity.Report:
                    return "\x1b[38;2;59;168;57mINFO\x1b[0m";
                case LogSeverity.Warning:
                    return "\x1b[38;2;191;191;23mWARN\x1b[0m";
                case LogSeverity.Error:
                    return "\x1b[38;2;252;3;3mERROR\x1b[0m";
                default:
                    return "UNKNOWN";
            }
        }
    }
}
