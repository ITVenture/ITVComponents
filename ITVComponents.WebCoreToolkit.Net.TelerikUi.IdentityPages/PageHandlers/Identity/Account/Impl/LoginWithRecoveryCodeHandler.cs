using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Impl
{
    internal class LoginWithRecoveryCodeHandler: ILoginWithRecoveryCodeHandler
    {
        public bool UsePage => false;
        public Task<UserQueryTicket> GetTwoFactorAuthenticationUserAsync()
        {
            return Task.FromResult(new UserQueryTicket
                { UserExists = false, EmailConfirmed = false, UserResultId = Guid.Empty });
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }

        public string GetUserId(UserQueryTicket userTicket)
        {
            return null;
        }

        public Task<SignInResult> TwoFactorRecoveryCodeSignInAsync(string recoveryCode)
        {
            return Task.FromResult(SignInResult.Failed);
        }
    }
}
