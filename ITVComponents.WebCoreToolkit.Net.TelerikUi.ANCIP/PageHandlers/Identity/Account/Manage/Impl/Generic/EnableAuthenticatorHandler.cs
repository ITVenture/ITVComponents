using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage.Impl.Generic
{
    [FallbackPageHandler(typeof(EnableAuthenticatorHandler))]
    internal class EnableAuthenticatorHandler<TUser>:IEnableAuthenticatorHandler where TUser : class
    {
        private readonly UserManager<TUser> userManager;
        private readonly IOptions<AuthenticatorOptions> onboardingOptions;
        private readonly UrlEncoder urlEncoder;
        private UserGuard<TUser> userGuard;

        public EnableAuthenticatorHandler(UserManager<TUser> userManager, IOptions<AuthenticatorOptions> onboardingOptions, UrlEncoder urlEncoder, UserGuard<TUser> userGuard)
        {
            this.userManager = userManager;
            this.onboardingOptions = onboardingOptions;
            this.urlEncoder = urlEncoder;
            this.userGuard = userGuard;
        }

        public bool UsePage => true;
        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return userGuard.FetchUser(user, AdditionalUserInfoToLoad.Email);
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return userManager.GetUserId(user);
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return userGuard.ReleaseUser(userTicket);
        }

        public async Task<AuthenticatorInitData> LoadSharedKeyAndQrData(UserQueryTicket userTicket)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                // Load the authenticator key & QR code URI to display on the form
                var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
                if (string.IsNullOrEmpty(unformattedKey))
                {
                    await userManager.ResetAuthenticatorKeyAsync(user);
                    unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
                }

                var sharedKey = AuthenticatorHelper.FormatKey(unformattedKey);

                var email = userTicket.Email;
                var authenticatorUri = AuthenticatorHelper.GenerateQrCodeUri(email, onboardingOptions.Value.ProductTag, unformattedKey, urlEncoder);
                return new AuthenticatorInitData
                {
                    AuthenticatorUri = authenticatorUri,
                    SharedKey = sharedKey,
                    Success = true
                };
            }

            return new AuthenticatorInitData();
        }

        public async Task<AuthenticatorVerificationData> VerifyAuthenticatorToken(UserQueryTicket userTicket, string verificationCode)
        {
            var retVal = new AuthenticatorVerificationData();
            if (userGuard.GetUser(userTicket, out var user))
            {
                retVal.Success = await userManager.VerifyTwoFactorTokenAsync(
                    user, userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);
                if (retVal.Success)
                {
                    await userManager.SetTwoFactorEnabledAsync(user, true);
                    retVal.UserId = await userManager.GetUserIdAsync(user);
                    retVal.RecoveryCodeCount = await userManager.CountRecoveryCodesAsync(user);
                }
            }

            return retVal;
        }

        public async Task<string[]> GenerateNewRecoveryCodes(UserQueryTicket userTicket, int count)
        {
            if (userGuard.GetUser(userTicket, out var user))
            {
                var retVal = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, count);
                return retVal.ToArray();
            }

            return Array.Empty<string>();
        }
    }
}
