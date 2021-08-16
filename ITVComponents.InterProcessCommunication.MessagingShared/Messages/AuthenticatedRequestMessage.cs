using ITVComponents.InterProcessCommunication.MessagingShared.Security;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public abstract class AuthenticatedRequestMessage
    {
        public TransferIdentity AuthenticatedUser { get; set; }
    }
}
