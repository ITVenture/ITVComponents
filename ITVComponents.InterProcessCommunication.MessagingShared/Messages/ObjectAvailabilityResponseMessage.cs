using System;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    [Serializable]
    public class ObjectAvailabilityResponseMessage:IServerResponse
    {
        public bool Available { get; set; }
        public string Message { get; set; }
    }
}
