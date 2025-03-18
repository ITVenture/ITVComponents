using System;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Helpers
{
    public abstract class UserGuard<TUser> where TUser:class
    {
        private readonly SignInManager<TUser> signInManager;
        private readonly UserManager<TUser> userManager;
        private ConcurrentDictionary<Guid, TUser> userBuffer = new ConcurrentDictionary<Guid, TUser>();

        public UserGuard(SignInManager<TUser> signInManager, UserManager<TUser> userManager)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
        }

        public async Task<UserQueryTicket> FetchUser(string userId, AdditionalUserInfoToLoad additionalInfo = AdditionalUserInfoToLoad.None)
        {
            var user = await userManager.FindByIdAsync(userId);
            return await BuildUserQueryTicket(user, additionalInfo);
        }
        public async Task<UserQueryTicket> FetchUser(ClaimsPrincipal user, AdditionalUserInfoToLoad additionalInfo = AdditionalUserInfoToLoad.None)
        {
            var userInstance = await userManager.GetUserAsync(user);
            return await BuildUserQueryTicket(userInstance, additionalInfo);
        }

        public async Task<UserQueryTicket> FetchUserByMail(string email, AdditionalUserInfoToLoad additionalInfo = AdditionalUserInfoToLoad.None)
        {
            var userInstance = await userManager.FindByEmailAsync(email);
            var tmp = await BuildUserQueryTicket(userInstance, additionalInfo);
            tmp.Email ??= email;
            return tmp;
        }

        public async Task<UserQueryTicket> GetTwoFactorUser(AdditionalUserInfoToLoad additionalInfo = AdditionalUserInfoToLoad.None)
        {
            if (signInManager == null)
            {
                throw new InvalidOperationException("SignInManager is required for this!");
            }

            var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
            return await BuildUserQueryTicket(user, additionalInfo);
        }

        public string GetUserId(UserQueryTicket userTicket)
        {
            if (GetUser(userTicket, out var user))
            {
                return GetUserId(user);
            }

            return null;
        }

        public bool ReleaseUser(UserQueryTicket ticket)
        {
            return userBuffer.TryRemove(ticket.UserResultId, out _);
        }

        public bool GetUser(UserQueryTicket ticket, out TUser user)
        {
            return userBuffer.TryGetValue(ticket.UserResultId, out user);
        }

        public abstract string GetUserId(TUser user);

        private async Task<UserQueryTicket> BuildUserQueryTicket(TUser userInstance, AdditionalUserInfoToLoad additionalInfo)
        {
            var userExists = userInstance != null;
            var ticketId = userExists ? Guid.NewGuid() : Guid.Empty;
            bool emailIsConfirmed = false;
            bool userHasPassword = false;
            bool twoFactorEnabled = false;
            string email = null;
            string userName = null;
            string phone = null;
            bool isAuthenticatorConfigured = false;
            bool machineRememberForTwoFactor = false;
            int recoveryCodesLeft = 0;
            if (userExists)
            {
                userBuffer.TryAdd(ticketId, userInstance);
                emailIsConfirmed = await userManager.IsEmailConfirmedAsync(userInstance);
                userHasPassword = await userManager.HasPasswordAsync(userInstance);
                twoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(userInstance);
                if ((additionalInfo & AdditionalUserInfoToLoad.Email) == AdditionalUserInfoToLoad.Email)
                {
                    email = await userManager.GetEmailAsync(userInstance);
                }

                if ((additionalInfo & AdditionalUserInfoToLoad.UserName) == AdditionalUserInfoToLoad.UserName)
                {
                    userName = await userManager.GetUserNameAsync(userInstance);
                }

                if ((additionalInfo & AdditionalUserInfoToLoad.Phone) == AdditionalUserInfoToLoad.Phone)
                {
                    phone = await userManager.GetPhoneNumberAsync(userInstance);
                }

                if ((additionalInfo & AdditionalUserInfoToLoad.TwoFactorConfiguration) ==
                    AdditionalUserInfoToLoad.TwoFactorConfiguration)
                {
                    isAuthenticatorConfigured = await userManager.GetAuthenticatorKeyAsync(userInstance) != null;
                    machineRememberForTwoFactor = await signInManager.IsTwoFactorClientRememberedAsync(userInstance);
                    recoveryCodesLeft = await userManager.CountRecoveryCodesAsync(userInstance);
                }
            }

            return new UserQueryTicket
            {
                UserExists = userExists,
                UserResultId = ticketId,
                EmailConfirmed = emailIsConfirmed,
                HasPassword = userHasPassword,
                TwoFactorEnabled = twoFactorEnabled,
                Email = email,
                UserName = userName,
                PhoneNumber = phone,
                IsAuthenticatorConfigured = isAuthenticatorConfigured,
                MachineRememberForTwoFactor = machineRememberForTwoFactor,
                RecoveryCodesLeft =   recoveryCodesLeft
            };
        }
    }

    [Flags]
    public enum AdditionalUserInfoToLoad
    {
        None=0,
        UserName=1,
        Email = 2,
        Phone = 4,
        TwoFactorConfiguration=8
    }
}
