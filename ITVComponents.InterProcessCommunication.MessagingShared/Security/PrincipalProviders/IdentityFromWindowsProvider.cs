using System;
using System.Security.Principal;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Security.PrincipalProviders
{
    public class IdentityFromWindowsProvider:IIdentityProvider, IPlugin
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
        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }
        public TransferIdentity CurrentIdentity => CreateTransferId();

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        private TransferIdentity CreateTransferId()
        {
            var identity = WindowsIdentity.GetCurrent();
            var retVal = identity.ForTransfer(transferNameClaim, transferRoleClaim);
            return retVal;
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed() => Disposed?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
