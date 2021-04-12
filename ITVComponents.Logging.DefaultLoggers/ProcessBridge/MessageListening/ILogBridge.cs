namespace ITVComponents.Logging.DefaultLoggers.ProcessBridge.MessageListening
{
    public interface ILogBridge:ILogProxy
    {
        /// <summary>
        /// Allows a client object to retrieve log messages from this proxy object
        /// </summary>
        event LogMessageEventHandler LogMessage;
    }
}
