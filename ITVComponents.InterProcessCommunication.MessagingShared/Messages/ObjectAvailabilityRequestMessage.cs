using System;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    [Serializable]
    public class ObjectAvailabilityRequestMessage:AuthenticatedRequestMessage
    {
        public string UniqueName { get; set; }
    }
}
