using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl
{
    internal class EnableAuthenticatorHandler: IEnableAuthenticatorHandler
    {
        public bool UsePage => false;
        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return Task.FromResult(new UserQueryTicket { UserExists = false });
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return null;
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }

        public Task<AuthenticatorInitData> LoadSharedKeyAndQrData(UserQueryTicket userTicket)
        {
            return Task.FromResult(new AuthenticatorInitData { Success = false });
        }

        public Task<AuthenticatorVerificationData> VerifyAuthenticatorToken(UserQueryTicket userTicket, string verificationCode)
        {
            return Task.FromResult(new AuthenticatorVerificationData { Success = false });
        }

        public Task<string[]> GenerateNewRecoveryCodes(UserQueryTicket userTicket, int count)
        {
            return Task.FromResult(Array.Empty<string>());
        }
    }
}
