using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(PersonalDataHandler))]
    internal class PersonalDataHandler<TUser>: IPersonalDataHandler where TUser : class
    {
        private readonly UserManager<TUser> userManager;

        private UserGuard<TUser> userGuard;

        public PersonalDataHandler(UserManager<TUser> userManager, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;

        public async Task<bool> UserExists(ClaimsPrincipal user)
        {
            var tick = await userGuard.FetchUser(user);
            try
            {
                return tick.UserExists;
            }
            finally
            {
                userGuard.ReleaseUser(tick);
            }
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return userManager.GetUserId(user);
        }
    }
}
