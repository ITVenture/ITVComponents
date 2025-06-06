using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Impl.Generic
{
    [FallbackPageHandler(typeof(LoginHandler))]
    internal class LoginHandler<TUser>:ILoginHandler where TUser:class
    {
        private readonly SignInManager<TUser> signInManager;
        private readonly IOptions<AuthenticationHandlerOptions> availableAuthenticators;
        private readonly IOptions<LoginOptions> loginOptions;

        public LoginHandler(SignInManager<TUser> signInManager,
            IOptions<AuthenticationHandlerOptions> availableAuthenticators,
            IOptions<LoginOptions> loginOptions)
        {
            this.signInManager = signInManager;
            this.availableAuthenticators = availableAuthenticators;
            this.loginOptions = loginOptions;
            RegistrationInfo = loginOptions.Value.RegistrationPage;
            ExternalLoginConfig = loginOptions.Value.ExternalLoginPage;
        }

        public async Task<AuthenticationHandlerDefinition[]> FetchExternalProviders()
        {
            var optionsValue = availableAuthenticators.Value;
            var logoPattern = optionsValue.LogoPattern;
            return await signInManager.GetAuthenticationHandlerDefinitions(logoPattern, optionsValue.AuthenticationHandlers);
        }
        public async Task<SignInResult> LoginUserWithPassword(string email, string password, bool rememberMe, bool lockoutOnFailure=false)
        {
            return await signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: lockoutOnFailure);
        }

        public UserRegistrationInfo RegistrationInfo { get; } /*new UserRegistrationInfo
        {
            AllowRegister = true,
            Area = "Identity",
            Action = "Index",
            Controller = "Registration",
            ControllerLink = true
        };*/

        public ExternalLoginConfig ExternalLoginConfig { get; } /*= new ExternalLoginConfig()
        {
            UseExternalLogins = true,
            Area="Identity",
            Page = "Account/ExternalLogin",
            //Controller="Registration",
            //Action= "LoginExternal",
            PostToController = false
        };*/

        public bool UsePage => true;
    }
}
