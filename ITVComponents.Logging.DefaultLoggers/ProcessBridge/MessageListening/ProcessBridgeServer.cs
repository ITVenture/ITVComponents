namespace ITVComponents.Logging.DefaultLoggers.ProcessBridge.MessageListening
{
    public class ProcessBridgeServer:LogTarget
    {
        public ProcessBridgeServer(int minSeverity, int maxSeverity, string contextFilter, bool initialStatus)
            : base(minSeverity, maxSeverity, contextFilter, initialStatus, true)
        {
        }

        public ProcessBridgeServer(LogSeverity minSeverity, LogSeverity maxSeverity, string contextFilter, bool initialStatus)
            : base(minSeverity, maxSeverity, contextFilter, initialStatus, true)
        {
        }

        public ProcessBridgeServer(int minSeverity, int maxSeverity, string contextFilter, bool initialStatus, bool debugEnabled)
            : base(minSeverity, maxSeverity, contextFilter, initialStatus, debugEnabled, true)
        {
        }

        public ProcessBridgeServer(LogSeverity minSeverity, LogSeverity maxSeverity, string contextFilter, bool initialStatus, bool debugEnabled)
            : base(minSeverity, maxSeverity, contextFilter, initialStatus, debugEnabled, true)
        {
        }

        protected override void Log(string eventText, int severity, string context)
        {
            OnLogMessage(new LogMessageEventArgs(eventText, severity, context));
        }

        /// <summary>
        /// Raises the LogMessage event
        /// </summary>
        /// <param name="e">the data for the LogMessage event</param>
        protected virtual void OnLogMessage(LogMessageEventArgs e)
        {
            LogMessageEventHandler handler = LogMessage;
            if (handler != null) handler(this, e);
        }

        /// <summary>
        /// Provides client objects information about issued messages
        /// </summary>
        public event LogMessageEventHandler LogMessage;
    }
}
