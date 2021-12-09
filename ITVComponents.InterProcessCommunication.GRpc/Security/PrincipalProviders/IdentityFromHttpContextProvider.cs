using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Extensions;
using ITVComponents.InterProcessCommunication.Grpc.Security.Options;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.Grpc.Security.PrincipalProviders
{
    internal class IdentityFromHttpContextProvider : IIdentityProvider
    {
        private readonly IHierarchySettings<UserClaimRedirect> redirectSettings;
        private readonly IContextUserProvider userProvider;


        public IdentityFromHttpContextProvider(IHierarchySettings<UserClaimRedirect> redirectSettings, IContextUserProvider userProvider)
        {
            this.redirectSettings = redirectSettings;
            this.userProvider = userProvider;
        }

        public TransferIdentity CurrentIdentity => CreateTransferId();

        private TransferIdentity CreateTransferId()
        {
            var tmp = redirectSettings.Value;
            var identity = userProvider.User?.Identity as ClaimsIdentity;
            if (identity != null)
            {
                return identity.ForTransfer(tmp?.UserNameClaim, tmp?.UserRoleClaim);
            }

            return null;
        }
    }
}