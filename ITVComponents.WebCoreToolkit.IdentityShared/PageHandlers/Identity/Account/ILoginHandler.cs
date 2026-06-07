using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account
{
    public interface ILoginHandler : IPageHandlerInstance<LoginModel>
    {
        Task<AuthenticationHandlerDefinition[]> FetchExternalProviders();

        Task<SignInResult> LoginUserWithPassword(string email, string password, bool rememberMe,
            bool lockoutOnFailure = false);

        UserRegistrationInfo RegistrationInfo { get; }
        ExternalLoginConfig ExternalLoginConfig { get; }
    }
}
