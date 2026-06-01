using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Impl
{
    internal class ExternalLoginHandler:IExternalLoginHandler
    {
        public bool UsePage => false;
        public AuthenticationProperties ConfigureExternalAuthenticationProperties(string provider, string redirectUrl)
        {
            return null;
        }

        public Task<UserExternalLoginStatus> PerformExternalLogin()
        {
            return Task.FromResult(new UserExternalLoginStatus(){AuthResult = SignInResult.Failed});
        }
    }
}
