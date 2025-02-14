using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Impl.Generic
{
    [FallbackPageHandler(typeof(LogoutHandler))]
    internal class LogoutHandler<TUser>: ILogoutHandler where TUser : class
    {
        private readonly SignInManager<TUser> signInManager;

        public LogoutHandler(SignInManager<TUser> signInManager)
        {
            this.signInManager = signInManager;
        }

        public bool UsePage => true;

        public async Task SignOut()
        {
            await signInManager.SignOutAsync();
        }
    }
}
