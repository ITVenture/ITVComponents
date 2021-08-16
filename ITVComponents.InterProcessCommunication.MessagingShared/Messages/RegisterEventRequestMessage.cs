namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public class RegisterEventRequestMessage:AuthenticatedRequestMessage
    {
        public string TargetObject { get; set; }
        public string EventName { get; set; }
        public string RespondChannel { get; set; }
    }
}
