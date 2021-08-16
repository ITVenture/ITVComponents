namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public class AbandonExtendedProxyRequestMessage:AuthenticatedRequestMessage
    {
        public string ObjectName { get; set; }
    }
}
