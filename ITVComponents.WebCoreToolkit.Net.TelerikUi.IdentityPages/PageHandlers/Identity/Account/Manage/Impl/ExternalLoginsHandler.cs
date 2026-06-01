using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Manage.Impl
{
    internal class ExternalLoginsHandler: IExternalLoginsHandler
    {
        public bool UsePage => false;
        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return Task.FromResult(new UserQueryTicket { UserExists = false });
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return null;
        }

        public Task<UserExternalLoginConfiguration> GetUserExternalLoginConfiguration(UserQueryTicket user)
        {
            return Task.FromResult(new UserExternalLoginConfiguration
            {
                AvailableLogins = Array.Empty<AuthenticationHandlerDefinition>(),
                ExternalUserLogins = Array.Empty<UserLoginInfo>()
            });
        }

        public bool ReleaseUser(UserQueryTicket user)
        {
            return true;
        }

        public Task<UserExternalLoginStatus> RemoveExternalAuthentication(UserQueryTicket userTicket, string loginProvider, string providerKey)
        {
            return Task.FromResult(new UserExternalLoginStatus { Success = false });
        }

        public AuthenticationProperties ConfigureExternalAuthenticationProperties(string provider, string redirectUrl,
            ClaimsPrincipal user)
        {
            return null;
        }

        public Task<UserExternalLoginStatus> AddExternalLogin(UserQueryTicket userTicket)
        {
            return Task.FromResult(new UserExternalLoginStatus { Success = false });
        }
    }
}
