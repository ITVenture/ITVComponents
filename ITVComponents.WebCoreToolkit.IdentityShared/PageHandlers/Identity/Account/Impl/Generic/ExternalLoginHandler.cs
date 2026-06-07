using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl.Generic
{
    [FallbackPageHandler(typeof(ExternalLoginHandler))]
    internal class ExternalLoginHandler<TUser>: IExternalLoginHandler where TUser:class
    {
        private readonly SignInManager<TUser> signInManager;

        public ExternalLoginHandler(SignInManager<TUser> signInManager)
        {
            this.signInManager = signInManager;
        }

        public bool UsePage => true;
        public AuthenticationProperties ConfigureExternalAuthenticationProperties(string provider, string redirectUrl)
        {
            return signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        }

        public async Task<UserExternalLoginStatus> PerformExternalLogin()
        {
            UserExternalLoginStatus retVal = new UserExternalLoginStatus{Success=true};
            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                retVal.Success = false;
                retVal.ErrorOnLoadExternalData = true;
                //return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
                return retVal;
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey,
                isPersistent: false, bypassTwoFactor: true);
            retVal.Success = result.Succeeded;
            retVal.AuthResult = result;
            retVal.LoginUserName = info.Principal.Identity.Name;
            retVal.ProviderName = info.LoginProvider;
            return retVal;
        }
    }
}
