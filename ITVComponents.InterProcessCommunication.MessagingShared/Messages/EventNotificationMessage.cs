namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public class EventNotificationMessage
    {
        public string EventName { get; set; }
        public object[] Arguments { get; set; }
    }
}
