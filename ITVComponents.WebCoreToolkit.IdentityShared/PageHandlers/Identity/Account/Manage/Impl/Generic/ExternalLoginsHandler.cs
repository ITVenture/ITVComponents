using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.IdentityShared.Extensions;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(ExternalLoginsHandler))]
    internal class ExternalLoginsHandler<TUser>: IExternalLoginsHandler where TUser : class
    {
        private readonly UserManager<TUser> userManager;
        private readonly SignInManager<TUser> signInManager;
        private UserGuard<TUser> userGuard;
        private readonly IOptions<AuthenticationHandlerOptions> availableAuthenticators;

        public ExternalLoginsHandler(UserManager<TUser> userManager, SignInManager<TUser> signInManager, IOptions<AuthenticationHandlerOptions> availableAuthenticators, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.availableAuthenticators = availableAuthenticators;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;
        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return userGuard.FetchUser(user);
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return userManager.GetUserId(user);
        }

        public async Task<UserExternalLoginConfiguration> GetUserExternalLoginConfiguration(UserQueryTicket userTicket)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                var cfg = availableAuthenticators.Value;
                var currentLogins = await userManager.GetLoginsAsync(user);
                var otherLogins = await signInManager.GetAuthenticationHandlerDefinitions(cfg.LogoPattern, cfg.AuthenticationHandlers, auth => currentLogins.All(ul => auth.Name != ul.LoginProvider));
                return new UserExternalLoginConfiguration
                {
                    ExternalUserLogins = currentLogins,
                    AvailableLogins = otherLogins
                };
            }

            return new UserExternalLoginConfiguration
            {
                AvailableLogins = Array.Empty<AuthenticationHandlerDefinition>(),
                ExternalUserLogins = Array.Empty<UserLoginInfo>()
            };
        }

        public bool ReleaseUser(UserQueryTicket user)
        {
            return userGuard.ReleaseUser(user);
        }

        public async Task<UserExternalLoginStatus> RemoveExternalAuthentication(UserQueryTicket userTicket, string loginProvider, string providerKey, bool refreshSignIn = true)
        {
            var retVal = new UserExternalLoginStatus();
            if (userGuard.GetUser(userTicket, out var user))
            {
                var result = await userManager.RemoveLoginAsync(user, loginProvider, providerKey);
                retVal.Success = result.Succeeded;
                retVal.IdentityResult = result;
                if (retVal.Success)
                {
                    if (refreshSignIn)
                    {
                        await signInManager.RefreshSignInAsync(user);
                    }
                }
            }

            return retVal;
        }

        public AuthenticationProperties ConfigureExternalAuthenticationProperties(string provider, string redirectUrl,
            ClaimsPrincipal user)
        {
            return signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, userManager.GetUserId(user));
        }

        public async Task<UserExternalLoginStatus> AddExternalLogin(UserQueryTicket userTicket)
        {
            var retVal = new UserExternalLoginStatus();
            if (userGuard.GetUser(userTicket, out var user))
            {
                var userId = userGuard.GetUserId(user);
                var info = await signInManager.GetExternalLoginInfoAsync(userId);
                retVal.UserId = userId;
                retVal.Success = info != null;
                if (retVal.Success)
                {
                    retVal.IdentityResult = await userManager.AddLoginAsync(user, info);
                    retVal.Success = retVal.IdentityResult.Succeeded;
                }
            }

            return retVal;
        }
    }
}
