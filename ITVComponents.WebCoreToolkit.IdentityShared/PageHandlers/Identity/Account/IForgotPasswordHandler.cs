using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account
{
    public interface IForgotPasswordHandler : IPageHandlerInstance<ForgotPasswordModel>
    {
        Task<UserQueryTicket> FetchUser(string inputEmail);
        bool ReleaseUser(UserQueryTicket userTicket);
        Task<string> GeneratePasswordResetToken(UserQueryTicket userTicket);
    }
}
