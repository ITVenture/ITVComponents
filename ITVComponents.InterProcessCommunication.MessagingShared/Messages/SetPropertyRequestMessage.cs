namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public class SetPropertyRequestMessage:InvokeMethodRequestMessage
    {
        public object Value { get;set; }
    }
}
