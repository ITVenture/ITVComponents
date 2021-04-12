using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Extensions;
using ITVComponents.InterProcessCommunication.Grpc.Security.Options;
using ITVComponents.WebCoreToolkit.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.Grpc.Security.PrincipalProviders
{
    internal class IdentityFromHttpContextProvider:IIdentityProvider
    {
        private readonly IHttpContextAccessor httpContext;

        public IdentityFromHttpContextProvider(IHttpContextAccessor httpContext)
        {
            this.httpContext = httpContext;
        }

        public TransferIdentity CurrentIdentity => CreateTransferId();

        private TransferIdentity CreateTransferId()
        {
            var tmp = httpContext.HttpContext.RequestServices.GetService<IScopedSettings<UserClaimRedirect>>()?.Value;
            if (tmp == null || string.IsNullOrEmpty(tmp.UserNameClaim) && string.IsNullOrEmpty(tmp.UserRoleClaim))
            {
                tmp = httpContext.HttpContext.RequestServices.GetService<IGlobalSettings<UserClaimRedirect>>()?.Value;
            }
            var identity = httpContext.HttpContext.User?.Identity as ClaimsIdentity;
            if (identity != null)
            {
                return identity.ForTransfer(tmp?.UserNameClaim, tmp?.UserRoleClaim);
            }

            return null;
        }
    }
}
