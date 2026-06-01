using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Impl
{
    internal class LoginWith2faHandler : ILoginWith2faHandler
    {
        public bool UsePage => false;
        public Task<UserQueryTicket> GetTwoFactorAuthenticationUserAsync()
        {
            return Task.FromResult(new UserQueryTicket
                { EmailConfirmed = false, UserExists = false, UserResultId = Guid.Empty });
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }

        public string GetUserId(UserQueryTicket userTicket)
        {
            return null;
        }

        public Task<SignInResult> TwoFactorAuthenticatorSignInAsync(string authenticatorCode, bool rememberMe, bool rememberMachine)
        {
            return Task.FromResult(SignInResult.Failed);
        }
    }
}
