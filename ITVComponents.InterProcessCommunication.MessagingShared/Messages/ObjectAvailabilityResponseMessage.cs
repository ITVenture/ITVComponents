using System;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    [Serializable]
    public class ObjectAvailabilityResponseMessage
    {
        public bool Available { get; set; }
        public string Message { get; set; }
    }
}
