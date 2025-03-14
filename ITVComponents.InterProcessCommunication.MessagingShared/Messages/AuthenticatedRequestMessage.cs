using ITVComponents.InterProcessCommunication.MessagingShared.Security;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages
{
    public abstract class AuthenticatedRequestMessage: IRequestMessage
    {
        public TransferIdentity AuthenticatedUser { get; set; }
    }
}
