using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account
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
