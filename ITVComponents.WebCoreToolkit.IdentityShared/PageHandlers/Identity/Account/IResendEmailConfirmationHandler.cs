using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account
{
    public interface IResendEmailConfirmationHandler : IPageHandlerInstance<ResendEmailConfirmationModel>
    {
        Task<UserQueryTicket> FetchUser(string email);
        bool ReleaseUser(UserQueryTicket userTicket);
        Task<UserMailTokenData> GetMailToken(UserQueryTicket userTicket);
    }
}
