namespace ITVComponents.InterProcessCommunication.MessagingShared.Security
{
    public interface IIdentityProvider
    {
        TransferIdentity CurrentIdentity{ get; }
    }
}
