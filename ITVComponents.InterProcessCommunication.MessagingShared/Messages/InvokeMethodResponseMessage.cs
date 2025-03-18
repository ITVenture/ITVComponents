namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public class InvokeMethodResponseMessage:IServerResponse
    {
        public string Arguments { get; set; }
        public string Result { get; set; }
    }
}
