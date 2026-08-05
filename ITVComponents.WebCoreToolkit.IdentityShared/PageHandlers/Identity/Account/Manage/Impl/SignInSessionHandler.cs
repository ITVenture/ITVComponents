using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl
{
    /// <summary>Fallback for hosts without a configured identity user type: everything is a no-op.</summary>
    internal class SignInSessionHandler : ISignInSessionHandler
    {
        public bool UsePage => false;

        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return Task.FromResult(new UserQueryTicket { UserExists = false });
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }

        public Task RefreshSignIn(UserQueryTicket userTicket)
        {
            return Task.CompletedTask;
        }

        public Task SignOut()
        {
            return Task.CompletedTask;
        }

        public Task ForgetTwoFactorClient()
        {
            return Task.CompletedTask;
        }
    }
}
