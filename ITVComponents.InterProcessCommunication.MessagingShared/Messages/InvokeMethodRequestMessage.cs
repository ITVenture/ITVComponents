namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public class InvokeMethodRequestMessage:AuthenticatedRequestMessage
    {
        public string TargetObject { get; set; }
        public string TargetMethod { get; set; }

        public string MethodArguments { get; set; }
        //public object[] MethodArguments { get; set; }
    }
}
