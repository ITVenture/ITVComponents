namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public class InvokeMethodResponseMessage
    {
        public object[] Arguments { get; set; }
        public object Result { get; set; }
    }
}
