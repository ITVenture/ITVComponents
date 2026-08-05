using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(SignInSessionHandler))]
    internal class SignInSessionHandler<TUser> : ISignInSessionHandler where TUser : class
    {
        private readonly SignInManager<TUser> signInManager;
        private readonly UserGuard<TUser> userGuard;

        public SignInSessionHandler(SignInManager<TUser> signInManager, UserGuard<TUser> userGuard)
        {
            this.signInManager = signInManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;

        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return userGuard.FetchUser(user, AdditionalUserInfoToLoad.None);
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }

        public async Task RefreshSignIn(UserQueryTicket userTicket)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                await signInManager.RefreshSignInAsync(user);
            }
        }

        public Task SignOut()
        {
            return signInManager.SignOutAsync();
        }

        public Task ForgetTwoFactorClient()
        {
            return signInManager.ForgetTwoFactorClientAsync();
        }
    }
}
