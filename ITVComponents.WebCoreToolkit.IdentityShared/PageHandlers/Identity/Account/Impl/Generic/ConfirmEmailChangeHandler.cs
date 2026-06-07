using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl.Generic
{
    [FallbackPageHandler(typeof(ConfirmEmailChangeHandler))]
    internal class ConfirmEmailChangeHandler<TUser>:IConfirmEmailChangeHandler where TUser:class

    {
        private readonly UserManager<TUser> userManager;
        private readonly SignInManager<TUser> signInManager;
        private UserGuard<TUser> userGuard;

        public ConfirmEmailChangeHandler(UserManager<TUser> userManager, SignInManager<TUser> signInManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.userGuard = userGuard;
        }

        public async Task<UserQueryTicket> FetchUser(string userId)
        {
            return await userGuard.FetchUser(userId);
        }

        public async Task<IdentityResult> ChangeEmailAddress(UserQueryTicket userTicket, string email, string code, bool changeUsernameAlso)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                var result = await userManager.ChangeEmailAsync(user, email, code);
                if (result.Succeeded)
                {
                    result = await userManager.SetUserNameAsync(user, email);
                }

                return result;
            }

            return IdentityResult.Failed();
        }

        public async Task RefreshSignIn(UserQueryTicket userTicket)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                await signInManager.RefreshSignInAsync(user);
            }
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }

        public string RedirectOnNoCode { get; } = "/Index";

        public bool UsePage => true;
    }
}
