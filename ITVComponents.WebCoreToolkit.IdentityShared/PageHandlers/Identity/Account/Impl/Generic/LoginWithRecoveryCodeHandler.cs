using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl.Generic
{
    [FallbackPageHandler(typeof(LoginWithRecoveryCodeHandler))]
    internal class LoginWithRecoveryCodeHandler<TUser>: ILoginWithRecoveryCodeHandler where TUser : class
    {
        private readonly SignInManager<TUser> signInManager;

        private UserGuard<TUser> userGuard;

        public LoginWithRecoveryCodeHandler(SignInManager<TUser> signInManager, UserGuard<TUser> userGuard)
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

        public Task<SignInResult> TwoFactorRecoveryCodeSignInAsync(string recoveryCode)
        {
            return signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);
        }
    }
}
