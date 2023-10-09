using System;
using System.Security.Principal;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Security.PrincipalProviders
{
    public class IdentityFromWindowsProvider:IIdentityProvider
    {
        private readonly string transferNameClaim;
        private readonly string transferRoleClaim;

        public IdentityFromWindowsProvider()
        {
        }
        
        public IdentityFromWindowsProvider(string transferNameClaim, string transferRoleClaim)
        {
            this.transferNameClaim = transferNameClaim;
            this.transferRoleClaim = transferRoleClaim;
        }
        public TransferIdentity CurrentIdentity => CreateTransferId();

        private TransferIdentity CreateTransferId()
        {
            var identity = WindowsIdentity.GetCurrent();
            var retVal = identity.ForTransfer(transferNameClaim, transferRoleClaim);
            return retVal;
        }
    }
}
