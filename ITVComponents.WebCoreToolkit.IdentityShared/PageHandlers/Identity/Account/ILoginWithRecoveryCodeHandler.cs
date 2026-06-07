using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account
{
    public interface ILoginWithRecoveryCodeHandler:IPageHandlerInstance<LoginWithRecoveryCodeModel>
    {
        Task<UserQueryTicket> GetTwoFactorAuthenticationUserAsync();

        bool ReleaseUser(UserQueryTicket userTicket);

        string GetUserId(UserQueryTicket userTicket);

        Task<SignInResult> TwoFactorRecoveryCodeSignInAsync(string recoveryCode);
    }
}
