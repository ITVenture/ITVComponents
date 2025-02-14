using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Impl
{
    internal class LoginHandler:ILoginHandler
    {
        public bool UsePage => false;

        public Task<AuthenticationHandlerDefinition[]> FetchExternalProviders()
        {
            return Task.FromResult(Array.Empty<AuthenticationHandlerDefinition>());
        }

        public Task<SignInResult> LoginUserWithPassword(string email, string password, bool rememberMe, bool lockoutOnFailure = false)
        {
            return Task.FromResult(SignInResult.Failed);
        }

        public UserRegistrationInfo RegistrationInfo => null;
        public ExternalLoginConfig ExternalLoginConfig => null;
    }
}
