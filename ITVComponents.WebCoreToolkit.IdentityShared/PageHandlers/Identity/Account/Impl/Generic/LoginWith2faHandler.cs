using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl.Generic
{
    [FallbackPageHandler(typeof(LoginWith2faHandler))]
    internal class LoginWith2faHandler<TUser>:ILoginWith2faHandler where TUser : class
    {
        private readonly SignInManager<TUser> signInManager;

        private UserGuard<TUser>  userGuard;

        public LoginWith2faHandler(SignInManager<TUser> signInManager, UserManager<TUser> userManager, UserGuard<TUser> userGuard)
        {
            this.signInManager = signInManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;

        public async Task<UserQueryTicket> GetTwoFactorAuthenticationUserAsync()
        {
            return await userGuard.GetTwoFactorUser();
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }

        public string GetUserId(UserQueryTicket userTicket)
        {
            return userGuard.GetUserId(userTicket);
        }

        public Task<SignInResult> TwoFactorAuthenticatorSignInAsync(string authenticatorCode, bool rememberMe, bool rememberMachine)
        {
            return signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, rememberMe, rememberMachine);
        }
    }
}
