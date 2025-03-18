namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public class EventNotificationMessage:IRequestMessage,IServerResponse
    {
        public string EventName { get; set; }
        public string Arguments { get; set; }
    }
}
